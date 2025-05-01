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
open EigenAstroSim.UI.Services

// Message types for the simulation engine
type Msg =
    | UpdateMount of MountState
    | UpdateDetailedMount of DetailedMountState
    | UpdateCamera of CameraState
    | UpdateRotator of RotatorState
    | UpdateAtmosphere of AtmosphericState
    | StartExposure of duration:float
    | StopExposure
    | SlewTo of ra:float * dec:float
    | Nudge of raRate:float * decRate:float * duration:float
    | SetTrackingRate of rate:float
    | SetRotatorPosition of angle:float
    | SetSeeingCondition of arcseconds:float
    | SetCloudCoverage of percentage:float
    | SetTransparency of percentage:float
    | SetPolarAlignmentError of degrees:float
    | SetPeriodicError of amplitude:float * period:float
    | GenerateSatelliteTrail
    | AdvanceTime of seconds:float
    | SimulateCableSnag of raAmount:float * decAmount:float

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
    
    // Detailed mount state
    let mutable detailedMountState = createDefaultDetailedMountState state.Mount
    
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
    
        // Process messages and update state
    let processMessage (mb: MailboxProcessor<Msg>) msg =
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
        
        | UpdateCamera newCameraState ->
            state <- { state with Camera = newCameraState }
            cameraStateChanged.OnNext(newCameraState)
        
        | UpdateRotator newRotatorState ->
            state <- { state with Rotator = newRotatorState }
            rotatorStateChanged.OnNext(newRotatorState)
        
        | UpdateAtmosphere newAtmosphereState ->
            state <- { state with Atmosphere = newAtmosphereState }
            atmosphereStateChanged.OnNext(newAtmosphereState)
        
        | StartExposure duration ->
            // Update camera state to indicate exposure in progress
            let newCamera = { state.Camera with IsExposing = true; ExposureTime = duration }
            state <- { state with Camera = newCamera }
            cameraStateChanged.OnNext(newCamera)
            
            // Cancel any existing timer
            match exposureTimer with
            | Some timer -> timer.Dispose()
            | None -> ()
            
            // Create a new timer for exposure completion
            let timer = new System.Timers.Timer(duration * 1000.0)
            timer.AutoReset <- false
            timer.Elapsed.Add(fun _ -> 
                // Generate the image
                let image = generateImage state
                imageGenerated.OnNext(image)
                
                // Update camera state to indicate exposure completed
                let updatedCamera = { state.Camera with IsExposing = false }
                mb.Post(UpdateCamera updatedCamera)
                
                // Dispose the timer
                timer.Dispose()
                exposureTimer <- None
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
        
        | SetRotatorPosition angle ->
            Logger.logf "Processing SetRotatorPosition: {0}" [|angle|]
            let newRotatorState = { state.Rotator with Position = angle; IsMoving = true }
            state <- { state with Rotator = newRotatorState }
            
            // Simulate rotator movement (immediate for now)
            let finalRotatorState = { newRotatorState with IsMoving = false }
            state <- { state with Rotator = finalRotatorState }
            Logger.logf "Sending RotatorStateChanged notification with Position={0}" [|finalRotatorState.Position|]
            rotatorStateChanged.OnNext(finalRotatorState)
        
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
        
        | GenerateSatelliteTrail ->
            state <- { state with HasSatelliteTrail = true }
            
            // Generate the image with satellite trail
            let image = generateImage state
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
            
            // If we're exposing, periodically update the image for live view
            if state.Camera.IsExposing then
                // For live view, generate an image with a scaled exposure time
                let liveExposureTime = min state.Camera.ExposureTime 0.1
                let liveViewState = { state with Camera = { state.Camera with ExposureTime = liveExposureTime } }
                let image = generateImage liveViewState
                imageGenerated.OnNext(image)
        
        | SimulateCableSnag(raAmount, decAmount) ->
            let newDetailedMountState = simulateCableSnag detailedMountState raAmount decAmount
            detailedMountState <- newDetailedMountState
            state <- { state with Mount = newDetailedMountState.BaseState }
            
            mountStateChanged.OnNext(newDetailedMountState.BaseState)
            detailedMountStateChanged.OnNext(newDetailedMountState)


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
        Logger.log(sprintf "SimulationEngine received message: %A" msg)  // Note the sprintf
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