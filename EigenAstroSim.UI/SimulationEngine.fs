namespace EigenAstroSim.UI

open System
open System.Reactive.Subjects
open System.Reactive.Linq
open FSharp.Control.Reactive
open EigenAstroSim.Domain
open EigenAstroSim.Domain.Types
open EigenAstroSim.Domain.StarField
open EigenAstroSim.Domain.StarFieldGenerator
open EigenAstroSim.Domain.MountSimulation
open EigenAstroSim.Domain.ImageGeneration
open EigenAstroSim.Domain.VirtualSensorSimulation

// Component-specific message types
type MountMsg =
    | UpdateMount of MountState
    | UpdateDetailedMount of DetailedMountState
    | SlewTo of ra:float * dec:float
    | Nudge of raRate:float * decRate:float * duration:float
    | SetTrackingRate of rate:float
    | SetPolarAlignmentError of degrees:float
    | SetPeriodicError of amplitude:float * period:float
    | SimulateCableSnag of raAmount:float * decAmount:float

type CameraMsg =
    | UpdateCamera of CameraState
    | StartExposure of duration:float
    | StopExposure
    | SetContinuousCapture of isEnabled:bool

type RotatorMsg =
    | UpdateRotator of RotatorState
    | SetRotatorPosition of angle:float

type AtmosphereMsg =
    | UpdateAtmosphere of AtmosphericState
    | SetSeeingCondition of arcseconds:float
    | SetCloudCoverage of percentage:float
    | SetTransparency of percentage:float

type SimulationMsg =
    | AdvanceTime of seconds:float
    | GenerateSatelliteTrail

// Unified message type that wraps component messages
type Msg =
    | MountCommand of MountMsg
    | CameraCommand of CameraMsg
    | RotatorCommand of RotatorMsg
    | AtmosphereCommand of AtmosphereMsg
    | SimulationCommand of SimulationMsg

// Core simulation engine
type SimulationEngine() =
    // Internal state
    let mutable state = {
        StarField = createEmpty 0.0 0.0
        Mount = {
            RA = 0.0
            Dec = 0.0
            TrackingRate = siderealRate
            PolarAlignmentError = 0.0
            PeriodicErrorAmplitude = 0.0
            PeriodicErrorPeriod = 0.0
            PeriodicErrorHarmonics = []
            IsSlewing = false
            SlewRate = 1.0
            FocalLength = 400.0
        }
        Camera = {
            Width = 800
            Height = 600
            PixelSize = 5.2
            ExposureTime = 1.0
            Binning = 1
            IsExposing = false
            ReadNoise = 5.0
            DarkCurrent = 0.1
        }
        Rotator = {
            Position = 0.0
            IsMoving = false
        }
        Atmosphere = {
            SeeingCondition = 1.5
            CloudCoverage = 0.0
            Transparency = 1.0
        }
        TimeScale = 1.0
        CurrentTime = DateTime.Now
        HasSatelliteTrail = false
    }
    // Flag for continuous capture mode
    let mutable continuousCaptureEnabled = false
    
    // Detailed mount state
    let mutable detailedMountState = createDefaultDetailedMountState state.Mount
    let mutable currentImageGenerator : IImageGenerator = SimpleImageGenerator() :> IImageGenerator
    
    // The random generator for star field and other random elements
    let random = Random()
    
    // Observable subjects for state changes
    let mountStateChanged = new System.Reactive.Subjects.Subject<MountState>()
    let detailedMountStateChanged = new System.Reactive.Subjects.Subject<DetailedMountState>()
    let cameraStateChanged = new System.Reactive.Subjects.Subject<CameraState>()
    let rotatorStateChanged = new System.Reactive.Subjects.Subject<RotatorState>()
    let atmosphereStateChanged = new System.Reactive.Subjects.Subject<AtmosphericState>()
    let imageGenerated = new System.Reactive.Subjects.Subject<(float[])>()
    let starFieldChanged = new System.Reactive.Subjects.Subject<StarFieldState>()
    
    // Timer for auto-exposures
    let mutable exposureTimer = Option<Timers.Timer>.None
    let logger = Logger.getLogger<SimulationEngine>()
    
    // Component-specific message handlers
    let processMountMessage (mb: MailboxProcessor<Msg>) (msg: MountMsg) =
        match msg with
        | UpdateMount newMountState ->
            state <- { state with Mount = newMountState }
            detailedMountState <- { detailedMountState with BaseState = newMountState }
            mountStateChanged.OnNext(newMountState)
            detailedMountStateChanged.OnNext(detailedMountState)
        
        | UpdateDetailedMount newDetailedMountState ->
            detailedMountState <- newDetailedMountState
            state <- { state with Mount = newDetailedMountState.BaseState }
            mountStateChanged.OnNext(newDetailedMountState.BaseState)
            detailedMountStateChanged.OnNext(newDetailedMountState)
        
        | SlewTo(targetRa, targetDec) ->
            // Update starfield if necessary
            let fovWidth, fovHeight = calculateFOV state.Camera state.Mount.FocalLength
            let maxDimension = max fovWidth fovHeight
            let updatedStarField = expandStarField state.StarField targetRa targetDec (maxDimension * 1.5) 12.0 random
            state <- { state with StarField = updatedStarField }
            starFieldChanged.OnNext(updatedStarField)
            
            // Begin slew in the mount model
            let newDetailedMountState = beginSlew detailedMountState targetRa targetDec
            detailedMountState <- newDetailedMountState
            state <- { state with Mount = newDetailedMountState.BaseState }
            
            mountStateChanged.OnNext(newDetailedMountState.BaseState)
            detailedMountStateChanged.OnNext(newDetailedMountState)
        
        | Nudge(raRate, decRate, duration) ->
            // Process the pulse guide in the mount model
            let newDetailedMountState = processPulseGuide detailedMountState raRate decRate duration
            detailedMountState <- newDetailedMountState
            state <- { state with Mount = newDetailedMountState.BaseState }
            
            mountStateChanged.OnNext(newDetailedMountState.BaseState)
            detailedMountStateChanged.OnNext(newDetailedMountState)
        
        | SetTrackingRate rate ->
            let newMountState = { state.Mount with TrackingRate = rate }
            state <- { state with Mount = newMountState }
            
            // Update the tracking mode in the detailed mount state
            let newTrackingMode = 
                if rate <= 0.0 then TrackingMode.Off
                elif abs (rate - siderealRate) < 0.0001 then TrackingMode.Sidereal
                else TrackingMode.Custom rate
                
            let newDetailedMountState = { detailedMountState with 
                                            BaseState = newMountState
                                            TrackingMode = newTrackingMode }
            detailedMountState <- newDetailedMountState
            
            mountStateChanged.OnNext(newMountState)
            detailedMountStateChanged.OnNext(newDetailedMountState)
        
        | SetPolarAlignmentError degrees ->
            let newMountState = { state.Mount with PolarAlignmentError = degrees }
            state <- { state with Mount = newMountState }
            detailedMountState <- { detailedMountState with BaseState = newMountState }
            
            mountStateChanged.OnNext(newMountState)
            detailedMountStateChanged.OnNext(detailedMountState)
        
        | SetPeriodicError(amplitude, period) ->
            let newMountState = { 
                state.Mount with 
                    PeriodicErrorAmplitude = amplitude
                    PeriodicErrorPeriod = period 
            }
            state <- { state with Mount = newMountState }
            detailedMountState <- { detailedMountState with BaseState = newMountState }
            
            mountStateChanged.OnNext(newMountState)
            detailedMountStateChanged.OnNext(detailedMountState)
            
        | SimulateCableSnag(raAmount, decAmount) ->
            let newDetailedMountState = simulateCableSnag detailedMountState raAmount decAmount
            detailedMountState <- newDetailedMountState
            state <- { state with Mount = newDetailedMountState.BaseState }
            
            mountStateChanged.OnNext(newDetailedMountState.BaseState)
            detailedMountStateChanged.OnNext(newDetailedMountState)
    
    let processCameraMessage (mb: MailboxProcessor<Msg>) (msg: CameraMsg) =
        match msg with
        | UpdateCamera newCameraState ->
            state <- { state with Camera = newCameraState }
            cameraStateChanged.OnNext(newCameraState)
        
        | SetContinuousCapture isEnabled ->
            logger.Infof "Setting continuous capture mode: %b" isEnabled
            continuousCaptureEnabled <- isEnabled
            
            // If enabling continuous capture and no exposure is in progress, start one
            if isEnabled && not state.Camera.IsExposing then
                mb.Post(CameraCommand(StartExposure state.Camera.ExposureTime))
                
        | StartExposure duration ->
            let newCamera = { state.Camera with IsExposing = true; ExposureTime = duration }
            state <- { state with Camera = newCamera }
            cameraStateChanged.OnNext(newCamera)
            match exposureTimer with
            | Some timer -> timer.Dispose()
            | None -> ()
            
            let timer = new System.Timers.Timer(duration * 1000.0)
            timer.AutoReset <- false
            timer.Elapsed.Add(fun _ -> 
                let image = currentImageGenerator.GenerateImage state
                imageGenerated.OnNext(image)
                
                // Update camera state to indicate exposure completed
                // IMPORTANT: Preserve the exposure time rather than resetting it
                let updatedCamera = { state.Camera with IsExposing = false }
                mb.Post(CameraCommand(UpdateCamera updatedCamera))
                
                timer.Dispose()
                exposureTimer <- None
                
                if continuousCaptureEnabled then
                    logger.Info "Continuous capture: starting next exposure"
                    mb.Post(CameraCommand(StartExposure state.Camera.ExposureTime))
            )
            
            // Start the timer
            timer.Start()
            exposureTimer <- Some timer
        
        | StopExposure ->
            // Cancel any existing timer
            match exposureTimer with
            | Some timer -> 
                timer.Dispose()
                exposureTimer <- None
            | None -> ()
            
            // Update camera state
            let newCamera = { state.Camera with IsExposing = false }
            state <- { state with Camera = newCamera }
            cameraStateChanged.OnNext(newCamera)
    
    let processRotatorMessage (msg: RotatorMsg) =
        match msg with
        | UpdateRotator newRotatorState ->
            state <- { state with Rotator = newRotatorState }
            rotatorStateChanged.OnNext(newRotatorState)
        
        | SetRotatorPosition angle ->
            logger.Infof "Processing SetRotatorPosition: %f" angle
            let newRotatorState = { state.Rotator with Position = angle; IsMoving = true }
            state <- { state with Rotator = newRotatorState }
            
            // Simulate rotator movement (immediate for now)
            let finalRotatorState = { newRotatorState with IsMoving = false }
            state <- { state with Rotator = finalRotatorState }
            logger.Infof "Sending RotatorStateChanged notification with Position=%f" finalRotatorState.Position
            rotatorStateChanged.OnNext(finalRotatorState)
    
    let processAtmosphereMessage (msg: AtmosphereMsg) =
        match msg with
        | UpdateAtmosphere newAtmosphereState ->
            state <- { state with Atmosphere = newAtmosphereState }
            atmosphereStateChanged.OnNext(newAtmosphereState)
        
        | SetSeeingCondition arcseconds ->
            let newAtmosphereState = { state.Atmosphere with SeeingCondition = arcseconds }
            state <- { state with Atmosphere = newAtmosphereState }
            atmosphereStateChanged.OnNext(newAtmosphereState)
        
        | SetCloudCoverage percentage ->
            let newAtmosphereState = { state.Atmosphere with CloudCoverage = percentage }
            state <- { state with Atmosphere = newAtmosphereState }
            atmosphereStateChanged.OnNext(newAtmosphereState)
        
        | SetTransparency percentage ->
            let newAtmosphereState = { state.Atmosphere with Transparency = percentage }
            state <- { state with Atmosphere = newAtmosphereState }
            atmosphereStateChanged.OnNext(newAtmosphereState)
    
    let processSimulationMessage (msg: SimulationMsg) =
        match msg with
        | GenerateSatelliteTrail ->
            state <- { state with HasSatelliteTrail = true }
            
            // Generate the image with satellite trail
            let image = currentImageGenerator.GenerateImage state
            imageGenerated.OnNext(image)
            
            // Reset the flag after generating one image
            state <- { state with HasSatelliteTrail = false }
        
        | AdvanceTime seconds ->
            // Update the current time
            let newTime = state.CurrentTime.AddSeconds(seconds)
            state <- { state with CurrentTime = newTime }
            
            // Update the mount state for tracking
            let newDetailedMountState = updateMountForTime detailedMountState newTime
            detailedMountState <- newDetailedMountState
            
            // Update the periodic error and polar alignment error
            let newDetailedMountState = applyPolarAlignmentError detailedMountState seconds
            detailedMountState <- newDetailedMountState
            
            // Update the slew if in progress
            match detailedMountState.SlewStatus with
            | Idle -> ()
            | Slewing _ -> 
                let updatedState = updateSlew detailedMountState seconds
                detailedMountState <- updatedState
            
            state <- { state with Mount = detailedMountState.BaseState }
            
            mountStateChanged.OnNext(detailedMountState.BaseState)
            detailedMountStateChanged.OnNext(detailedMountState)
    
    // Unified message processor
    let processMessage (mb: MailboxProcessor<Msg>) msg =
        logger.Infof "Processing message: %A" msg
        match msg with
        | MountCommand mountMsg -> 
            processMountMessage mb mountMsg
        | CameraCommand cameraMsg -> 
            processCameraMessage mb cameraMsg
        | RotatorCommand rotatorMsg -> 
            processRotatorMessage rotatorMsg
        | AtmosphereCommand atmosphereMsg -> 
            processAtmosphereMessage atmosphereMsg
        | SimulationCommand simulationMsg -> 
            processSimulationMessage simulationMsg

    // Create mailbox, passing itself to processMessage
    let mutable mailboxRef = None

    // Create the mailbox processor
    let mailbox = 
        let mb = MailboxProcessor.Start(fun inbox -> 
            let rec loop() = async {
                let! msg = inbox.Receive()
                processMessage (Option.get mailboxRef) msg
                return! loop()
            }
            loop())
        mailboxRef <- Some mb  // Set the reference after creation
        mb
    
    // Initialize with a small star field
    do
        let fovWidth, fovHeight = calculateFOV state.Camera state.Mount.FocalLength
        let maxDimension = max fovWidth fovHeight
        let initialStarField = expandStarField state.StarField state.Mount.RA state.Mount.Dec (maxDimension * 1.5) 12.0 random
        state <- { state with StarField = initialStarField }
        starFieldChanged.OnNext(initialStarField)
    
    // Public interface
    member _.PostMessage(msg) = 
        logger.Infof "SimulationEngine received message: %A" msg
        mailbox.Post(msg)
    member _.MountStateChanged = mountStateChanged.AsObservable()
    member _.DetailedMountStateChanged = detailedMountStateChanged.AsObservable()
    member _.CameraStateChanged = cameraStateChanged.AsObservable()
    member _.RotatorStateChanged = rotatorStateChanged.AsObservable()
    member _.AtmosphereStateChanged = atmosphereStateChanged.AsObservable()
    member _.ImageGenerated = imageGenerated.AsObservable()
    member _.StarFieldChanged = starFieldChanged.AsObservable()
    member _.CurrentState = state
    member _.CurrentDetailedMountState = detailedMountState
    member _.IsContinuousCaptureEnabled = continuousCaptureEnabled
    member _.CurrentImageGenerator = currentImageGenerator
    member _.SetImageGenerationMode(useHighFidelity: bool) =
        currentImageGenerator <- 
            if useHighFidelity then
                EnhancedVirtualAstrophotographySensor() :> IImageGenerator
            else
                SimpleImageGenerator() :> IImageGenerator


// Extension methods to simplify API usage for client code
[<AutoOpen>]
module SimulationEngineExtensions =
    type SimulationEngine with
        // Mount commands
        member this.UpdateMount(mountState) = 
            this.PostMessage(MountCommand(UpdateMount mountState))
        member this.UpdateDetailedMount(detailedMountState) = 
            this.PostMessage(MountCommand(UpdateDetailedMount detailedMountState))
        member this.SlewTo(ra, dec) = 
            this.PostMessage(MountCommand(SlewTo(ra, dec)))
        member this.Nudge(raRate, decRate, duration) = 
            this.PostMessage(MountCommand(Nudge(raRate, decRate, duration)))
        member this.SetTrackingRate(rate) = 
            this.PostMessage(MountCommand(SetTrackingRate rate))
        member this.SetPolarAlignmentError(degrees) = 
            this.PostMessage(MountCommand(SetPolarAlignmentError degrees))
        member this.SetPeriodicError(amplitude, period) = 
            this.PostMessage(MountCommand(SetPeriodicError(amplitude, period)))
        member this.SimulateCableSnag(raAmount, decAmount) = 
            this.PostMessage(MountCommand(SimulateCableSnag(raAmount, decAmount)))
            
        // Camera commands
        member this.UpdateCamera(cameraState) = 
            this.PostMessage(CameraCommand(UpdateCamera cameraState))
        member this.StartExposure(duration) = 
            this.PostMessage(CameraCommand(StartExposure duration))
        member this.StopExposure() = 
            this.PostMessage(CameraCommand(StopExposure))
        member this.SetContinuousCapture(isEnabled) = 
            this.PostMessage(CameraCommand(SetContinuousCapture isEnabled))
            
        // Rotator commands
        member this.UpdateRotator(rotatorState) = 
            this.PostMessage(RotatorCommand(UpdateRotator rotatorState))
        member this.SetRotatorPosition(angle) = 
            this.PostMessage(RotatorCommand(SetRotatorPosition angle))
            
        // Atmosphere commands
        member this.UpdateAtmosphere(atmosphereState) = 
            this.PostMessage(AtmosphereCommand(UpdateAtmosphere atmosphereState))
        member this.SetSeeingCondition(arcseconds) = 
            this.PostMessage(AtmosphereCommand(SetSeeingCondition arcseconds))
        member this.SetCloudCoverage(percentage) = 
            this.PostMessage(AtmosphereCommand(SetCloudCoverage percentage))
        member this.SetTransparency(percentage) = 
            this.PostMessage(AtmosphereCommand(SetTransparency percentage))
            
        // Simulation commands
        member this.AdvanceTime(seconds) = 
            this.PostMessage(SimulationCommand(AdvanceTime seconds))
        member this.GenerateSatelliteTrail() = 
            this.PostMessage(SimulationCommand(GenerateSatelliteTrail))