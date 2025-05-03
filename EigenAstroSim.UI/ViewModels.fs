namespace EigenAstroSim.UI

open System
open System.Reactive.Linq
open System.Collections.ObjectModel
open System.Collections.Generic
open System.Windows.Input
open System.Windows.Threading
open System.ComponentModel
open System.Threading
open System.Reactive.Subjects
open System.Reactive.Concurrency
open FSharp.Control.Reactive
open FSharp.Control.Reactive.Observable
open EigenAstroSim.Domain
open EigenAstroSim.Domain.Types
open EigenAstroSim.Domain.MountSimulation
open EigenAstroSim.UI.Services

// Base class for view models
type ViewModelBase() =
    let propertyChanged = Event<PropertyChangedEventHandler, PropertyChangedEventArgs>()
    
    interface INotifyPropertyChanged with
        [<CLIEvent>]
        member this.PropertyChanged = propertyChanged.Publish
    
    member this.OnPropertyChanged(propertyName) =
        propertyChanged.Trigger(this, PropertyChangedEventArgs(propertyName))
    
    interface IDisposable with
        member this.Dispose() =
            this.Dispose(true)
            GC.SuppressFinalize(this)
    
    abstract Dispose : bool -> unit
    default _.Dispose(_) = ()

// Reactive property implementation
type ReactiveProperty<'T>(?initialValue: 'T) =
    let mutable value = 
        match initialValue with
        | Some v -> v
        | None -> Unchecked.defaultof<'T>
        
    let changed = new System.Reactive.Subjects.Subject<'T>()
    let propertyChanged = Event<PropertyChangedEventHandler, PropertyChangedEventArgs>()
    
    interface INotifyPropertyChanged with
        [<CLIEvent>]
        member this.PropertyChanged = propertyChanged.Publish
    
    member this.Value
        with get() = value
        and set(v) =
            if not (EqualityComparer<'T>.Default.Equals(value, v)) then
                value <- v
                changed.OnNext(v)
                propertyChanged.Trigger(this, PropertyChangedEventArgs("Value"))
    
    member this.AsObservable() = changed.AsObservable()

// Command implementation
type ReactiveCommand<'T>(canExecute: IObservable<bool>, execute: 'T -> unit) =
    let canExecuteSubject = new BehaviorSubject<bool>(true)
    let canExecuteEvent = new Event<EventHandler, EventArgs>()
    
    do
        canExecute.Subscribe(fun value -> 
            Logger.logf "CanExecute changed to: {0}" [|value|]
            canExecuteSubject.OnNext(value)
            canExecuteEvent.Trigger(null, EventArgs.Empty)
        ) |> ignore
    
    interface ICommand with
        [<CLIEvent>]
        member _.CanExecuteChanged = canExecuteEvent.Publish
        
        member _.CanExecute(parameter) = 
            let result = canExecuteSubject.Value
            Logger.logf "CanExecute check returned: {0} for parameter: {1}" 
                [|result; if isNull parameter then "null" else parameter.ToString()|]
            result
        
        member _.Execute(parameter) =
            Logger.logf "Command execution started with parameter: {0} of type {1}" 
                [|if isNull parameter then "null" else parameter.ToString();
                  if isNull parameter then "null" else parameter.GetType().Name|]
            
            try
                // Check if this is a unit command - handle this first!
                if typeof<'T> = typeof<unit> then
                    Logger.log "Executing unit command (no parameters needed)"
                    execute Unchecked.defaultof<'T>
                    
                // If parameter is null but command expects a value, handle based on type
                else if isNull parameter then
                    match typeof<'T>.Name with
                    | "String" -> 
                        Logger.log "Converting null to empty string for string parameter"
                        execute ("" :> obj :?> 'T)
                    | "Boolean" -> 
                        Logger.log "Converting null to false for boolean parameter"
                        execute (false :> obj :?> 'T)
                    | "Int32" -> 
                        Logger.log "Converting null to 0 for int parameter"
                        execute (0 :> obj :?> 'T)
                    | "Double" | "Single" -> 
                        Logger.log "Converting null to 0.0 for float parameter"
                        execute (0.0 :> obj :?> 'T)
                    | _ ->
                        Logger.logf "Cannot convert null to target type {0}" [|typeof<'T>.Name|]
                
                // Parameter matches target type exactly
                else if parameter :? 'T then
                    Logger.log "Parameter matched target type directly"
                    execute (parameter :?> 'T)
                
                // Handle string conversions for common types
                else if parameter :? string then
                    let strParam = parameter :?> string
                    
                    match typeof<'T>.Name with
                    | "Double" | "Single" -> 
                        Logger.logf "Converting string to float: {0}" [|strParam|]
                        try
                            execute (float strParam :> obj :?> 'T)
                        with ex -> 
                            Logger.logException ex (Some "converting string to float")
                    
                    | "Int32" -> 
                        Logger.logf "Converting string to int: {0}" [|strParam|]
                        try
                            execute (int strParam :> obj :?> 'T)
                        with ex -> 
                            Logger.logException ex (Some "converting string to int")
                    
                    | "Boolean" -> 
                        Logger.logf "Converting string to bool: {0}" [|strParam|]
                        try
                            execute (Boolean.Parse(strParam) :> obj :?> 'T)
                        with ex -> 
                            Logger.logException ex (Some "converting string to bool")
                            
                    | "Tuple`2" when typeof<'T>.GenericTypeArguments.Length = 2 ->
                        // Handle tuple parameters from comma-separated strings (for RA,Dec coordinates)
                        Logger.logf "Attempting to convert string to tuple: {0}" [|strParam|]
                        try
                            let parts = strParam.Split(',')
                            if parts.Length = 2 then
                                let arg1Type = typeof<'T>.GenericTypeArguments.[0]
                                let arg2Type = typeof<'T>.GenericTypeArguments.[1]
                                
                                // Handle most common tuple type: float * float for coordinates
                                if arg1Type = typeof<float> && arg2Type = typeof<float> then
                                    let x = float (parts.[0].Trim())
                                    let y = float (parts.[1].Trim())
                                    let tuple = Tuple<float, float>(x, y)
                                    execute (tuple :> obj :?> 'T)
                                else
                                    Logger.logf "Unsupported tuple conversion: {0}, {1}" 
                                        [|arg1Type.Name; arg2Type.Name|]
                            else
                                Logger.log "String doesn't contain exactly 2 parts for tuple conversion"
                        with ex ->
                            Logger.logException ex (Some "converting string to tuple")
                    
                    | _ -> 
                        Logger.logf "Cannot convert string to target type {0}" [|typeof<'T>.Name|]
            with ex ->
                Logger.logException ex (Some "executing command")

// Command factory
module ReactiveCommand =
    // Simple command creation without parameters (unit commands)
    let createSimple (action: unit -> unit) =
        ReactiveCommand<unit>(Observable.Return(true), (fun _ -> action()))
    
    // Command creation with typed parameter
    let create (execute: 'T -> unit) =
        ReactiveCommand<'T>(Observable.Return(true), execute)
    
    // Command creation with canExecute observable
    let createWithCanExecute (canExecute: IObservable<bool>) (execute: 'T -> unit) =
        ReactiveCommand<'T>(canExecute, execute)
    
    // Command creation from observable-returning function
    let createFromObservable (canExecute: IObservable<bool>) (execute: 'T -> IObservable<unit>) =
        ReactiveCommand<'T>(canExecute, (fun param -> execute param |> Observable.subscribe ignore |> ignore))

// MountViewModel with updated commands
type MountViewModel(simulationEngine: SimulationEngine) =
    inherit ViewModelBase()
    
    // Observable state
    let ra = ReactiveProperty<float>()
    let dec = ReactiveProperty<float>()
    let isTracking = ReactiveProperty<bool>()
    let isSlewing = ReactiveProperty<bool>()
    let polarAlignmentError = ReactiveProperty<float>()
    let periodicErrorAmplitude = ReactiveProperty<float>()
    let periodicErrorPeriod = ReactiveProperty<float>()
    let slewRate = ReactiveProperty<float>()
    let focalLength = ReactiveProperty<float>()
    let targetRa = ReactiveProperty<float>()
    let targetDec = ReactiveProperty<float>()
    let coordinatesText = ReactiveProperty<string>()
    
    // Slew speed options (degrees per second)
    let slewSpeedOptions = [| 0.1; 0.5; 1.0; 2.0; 4.0; 8.0; 16.0; 32.0 |]
    let selectedSlewSpeed = ReactiveProperty<float>()
    
    // Subscribe first, then initialize - prevent race conditions
    let mountSubscription = 
        simulationEngine.MountStateChanged
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(fun state ->
                ra.Value <- state.RA
                dec.Value <- state.Dec
                isTracking.Value <- state.TrackingRate > 0.0
                isSlewing.Value <- state.IsSlewing
                polarAlignmentError.Value <- state.PolarAlignmentError
                periodicErrorAmplitude.Value <- state.PeriodicErrorAmplitude
                periodicErrorPeriod.Value <- state.PeriodicErrorPeriod
                slewRate.Value <- state.SlewRate
                focalLength.Value <- state.FocalLength)
    
    // Subscribe to detailed mount state changes for additional info
    let detailedMountSubscription =
        (simulationEngine.DetailedMountStateChanged : IObservable<DetailedMountState>)
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(fun state ->
                // Additional detailed mount state updates if needed
                isSlewing.Value <- 
                    match state.SlewStatus with
                    | Idle -> false
                    | Slewing(ra, dec, _) -> 
                        targetRa.Value <- ra
                        targetDec.Value <- dec
                        true)
    
    // Initialize with current state - after subscriptions
    do
        let currentMountState = simulationEngine.CurrentState.Mount
        ra.Value <- currentMountState.RA
        dec.Value <- currentMountState.Dec
        isTracking.Value <- currentMountState.TrackingRate > 0.0
        isSlewing.Value <- currentMountState.IsSlewing
        polarAlignmentError.Value <- currentMountState.PolarAlignmentError
        periodicErrorAmplitude.Value <- currentMountState.PeriodicErrorAmplitude
        periodicErrorPeriod.Value <- currentMountState.PeriodicErrorPeriod
        slewRate.Value <- currentMountState.SlewRate
        focalLength.Value <- currentMountState.FocalLength
        
        // Default slew speed
        selectedSlewSpeed.Value <- 1.0
        
        // Initial target coordinates
        targetRa.Value <- currentMountState.RA
        targetDec.Value <- currentMountState.Dec
    
    // Properties
    member _.RA = ra
    member _.Dec = dec
    member _.IsTracking = isTracking
    member _.IsSlewing = isSlewing
    member _.PolarAlignmentError = polarAlignmentError
    member _.PeriodicErrorAmplitude = periodicErrorAmplitude
    member _.PeriodicErrorPeriod = periodicErrorPeriod
    member _.SlewRate = slewRate
    member _.FocalLength = focalLength
    member _.TargetRA = targetRa
    member _.TargetDec = targetDec
    member _.SlewSpeedOptions = slewSpeedOptions
    member _.SelectedSlewSpeed = selectedSlewSpeed
    //member _.CoordinatesText = coordinatesText
    
    // Commands - updated to use createSimple for unit commands
    member _.NudgeNorthCommand = ReactiveCommand.createSimple (fun () -> 
        Logger.logf "Executing North nudge with speed: {0}" [|selectedSlewSpeed.Value|]
        simulationEngine.PostMessage(Nudge(0.0, selectedSlewSpeed.Value, 0.1)))
    
    member _.NudgeSouthCommand = ReactiveCommand.createSimple (fun () -> 
        Logger.logf "Executing South nudge with speed: {0}" [|selectedSlewSpeed.Value|]
        simulationEngine.PostMessage(Nudge(0.0, -selectedSlewSpeed.Value, 0.1)))
    
    member _.NudgeEastCommand = ReactiveCommand.createSimple (fun () -> 
        Logger.logf "Executing East nudge with speed: {0}" [|selectedSlewSpeed.Value|]
        simulationEngine.PostMessage(Nudge(-selectedSlewSpeed.Value, 0.0, 0.1)))
    
    member _.NudgeWestCommand = ReactiveCommand.createSimple (fun () -> 
        Logger.logf "Executing West nudge with speed: {0}" [|selectedSlewSpeed.Value|]
        simulationEngine.PostMessage(Nudge(selectedSlewSpeed.Value, 0.0, 0.1)))
    
    member _.SetTrackingCommand = ReactiveCommand.create (fun (isOn: bool) -> 
        Logger.logf "Setting tracking to: {0}" [|isOn|]
        let rate = if isOn then siderealRate else 0.0
        simulationEngine.PostMessage(SetTrackingRate rate))
    
    member _.SetPolarErrorCommand = ReactiveCommand.create (fun (error: float) -> 
        Logger.logf "Setting polar alignment error: {0} degrees" [|error|]
        simulationEngine.PostMessage(SetPolarAlignmentError error))
    
    member _.SetPeriodicErrorCommand = ReactiveCommand.create (fun (amplitude, period) -> 
        Logger.logf "Setting periodic error: amplitude={0}, period={1}" [|amplitude; period|]
        simulationEngine.PostMessage(SetPeriodicError(amplitude, period)))
    
    // Specifically for tuple parameters
    member _.SlewToCommand = ReactiveCommand.create (fun (raDecTuple: obj) -> 
        match raDecTuple with
        | :? Tuple<float, float> as tuple -> 
            let (ra, dec) = tuple
            Logger.logf "Executing SlewTo: RA={0}, Dec={1}" [|ra; dec|]
            simulationEngine.PostMessage(SlewTo(ra, dec))
        | _ -> 
            Logger.log "Invalid parameter type for SlewToCommand")
    
    // For text input coordinates
    member _.SlewToCoordinatesCommand = ReactiveCommand.create (fun (coordText: string) -> 
        Logger.logf "Executing SlewToCoordinates with input: {0}" [|coordText|]
        try
            // Parse coordinates in format "RA,Dec" 
            let parts = coordText.Split(',')
            if parts.Length = 2 then
                let ra = float (parts.[0].Trim())
                let dec = float (parts.[1].Trim())
                simulationEngine.PostMessage(SlewTo(ra, dec))
            else
                Logger.log "Invalid coordinate format: expected RA,Dec"
        with ex -> 
            Logger.logException ex (Some "parsing coordinates"))
    
    member _.SetSlewSpeedCommand = ReactiveCommand.create (fun (speed: float) -> 
        Logger.logf "Setting slew speed to: {0}" [|speed|]
        selectedSlewSpeed.Value <- speed
        let newMountState = { simulationEngine.CurrentState.Mount with SlewRate = speed }
        simulationEngine.PostMessage(UpdateMount newMountState))
    
    member _.SimulateCableSnagCommand = ReactiveCommand.createSimple (fun () -> 
        Logger.log "Simulating cable snag"
        let raAmount = 0.002
        let decAmount = 0.001
        simulationEngine.PostMessage(SimulateCableSnag(raAmount, decAmount)))
    
    override this.Dispose(disposing) =
        if disposing then
            mountSubscription.Dispose()
            detailedMountSubscription.Dispose()


// Updated CameraViewModel with continuous capture using the SimulationEngine
type CameraViewModel(simulationEngine: SimulationEngine) =
    inherit ViewModelBase()
    
    // Observable state
    let width = ReactiveProperty<int>()
    let height = ReactiveProperty<int>()
    let pixelSize = ReactiveProperty<float>()
    let exposureTime = ReactiveProperty<float>()
    let binning = ReactiveProperty<int>()
    let isExposing = ReactiveProperty<bool>()
    let readNoise = ReactiveProperty<float>()
    let darkCurrent = ReactiveProperty<float>()
    
    // New properties for continuous capture mode
    let isCapturing = ReactiveProperty<bool>(false)
    let captureButtonText = ReactiveProperty<string>("Start Capturing")
    
    // Available binning options
    let availableBinning = [| 1; 2; 4 |]
    
    // Subscribe to simulation state changes first
    let subscription = 
        simulationEngine.CameraStateChanged
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(fun state ->
                width.Value <- state.Width
                height.Value <- state.Height
                pixelSize.Value <- state.PixelSize
                if not isCapturing.Value || (Math.Abs(exposureTime.Value - state.ExposureTime) < 0.001) then
                    exposureTime.Value <- state.ExposureTime
                binning.Value <- state.Binning
                isExposing.Value <- state.IsExposing
                readNoise.Value <- state.ReadNoise
                darkCurrent.Value <- state.DarkCurrent)
    
    // Initialize after subscription setup
    do
        let currentCameraState = simulationEngine.CurrentState.Camera
        width.Value <- currentCameraState.Width
        height.Value <- currentCameraState.Height
        pixelSize.Value <- currentCameraState.PixelSize
        exposureTime.Value <- currentCameraState.ExposureTime
        binning.Value <- currentCameraState.Binning
        isExposing.Value <- currentCameraState.IsExposing
        readNoise.Value <- currentCameraState.ReadNoise
        darkCurrent.Value <- currentCameraState.DarkCurrent
        
        // Initialize capture button state
        isCapturing.Value <- simulationEngine.IsContinuousCaptureEnabled
        captureButtonText.Value <- if isCapturing.Value then "Stop Capturing" else "Start Capturing"
        
        // Subscribe to exposure time changes from the UI
        exposureTime.AsObservable()
            .Skip(1) // Skip initial value
            .Throttle(TimeSpan.FromMilliseconds(100.0)) // Debounce rapid changes
            .Subscribe(fun time ->
                // Update the camera state with the new exposure time
                let newCamera = { simulationEngine.CurrentState.Camera with ExposureTime = time }
                simulationEngine.PostMessage(UpdateCamera newCamera)
                
                // Log the change
                Logger.logf "Exposure time updated to: %.1f seconds" [|time|]
            ) |> ignore
    
    // Properties
    member _.Width = width
    member _.Height = height
    member _.PixelSize = pixelSize
    member _.ExposureTime = exposureTime
    member _.Binning = binning
    member _.IsExposing = isExposing
    member _.ReadNoise = readNoise
    member _.DarkCurrent = darkCurrent
    member _.AvailableBinning = availableBinning
    member _.IsCapturing = isCapturing
    member _.CaptureButtonText = captureButtonText
    
    member _.ToggleCaptureCommand = 
        ReactiveCommand.create (fun () -> 
            isCapturing.Value <- not isCapturing.Value
            
            if isCapturing.Value then
                captureButtonText.Value <- "Stop Capturing"
                Logger.log "Starting continuous capture mode"
                simulationEngine.PostMessage(SetContinuousCapture true)
            else
                captureButtonText.Value <- "Start Capturing"
                Logger.log "Stopping continuous capture mode"
                simulationEngine.PostMessage(SetContinuousCapture false)
                
                // Stop current exposure if one is in progress
                if isExposing.Value then
                    simulationEngine.PostMessage(StopExposure)
        )
    
    member _.SetExposureTimeCommand = 
        ReactiveCommand.create (fun (time: float) -> 
            Logger.logf "Setting exposure time to: {0} seconds" [|time|]
            let newCamera = { simulationEngine.CurrentState.Camera with ExposureTime = time }
            simulationEngine.PostMessage(UpdateCamera newCamera))
    
    member _.SetBinningCommand = 
        ReactiveCommand.create (fun (bin: int) -> 
            Logger.logf "Setting binning to: {0}" [|bin|]
            if availableBinning |> Array.contains bin then
                let newCamera = { simulationEngine.CurrentState.Camera with Binning = bin }
                simulationEngine.PostMessage(UpdateCamera newCamera))
    
    member _.SetReadNoiseCommand = 
        ReactiveCommand.create (fun (noise: float) -> 
            Logger.logf "Setting read noise to: {0}" [|noise|]
            let newCamera = { simulationEngine.CurrentState.Camera with ReadNoise = noise }
            simulationEngine.PostMessage(UpdateCamera newCamera))
    
    member _.SetDarkCurrentCommand = 
        ReactiveCommand.create (fun (dark: float) -> 
            Logger.logf "Setting dark current to: {0}" [|dark|]
            let newCamera = { simulationEngine.CurrentState.Camera with DarkCurrent = dark }
            simulationEngine.PostMessage(UpdateCamera newCamera))
    
    // Clean up subscriptions
    override this.Dispose(disposing) =
        if disposing then
            subscription.Dispose()
            
            // Ensure capturing is stopped
            if isCapturing.Value then
                simulationEngine.PostMessage(SetContinuousCapture false)
                if isExposing.Value then
                    simulationEngine.PostMessage(StopExposure)

// Updated AtmosphereViewModel with unit command support
type AtmosphereViewModel(simulationEngine: SimulationEngine) =
    inherit ViewModelBase()
    
    // Observable state
    let seeingCondition = ReactiveProperty<float>()
    let cloudCoverage = ReactiveProperty<float>()
    let transparency = ReactiveProperty<float>()
    
    // Subscribe to simulation state changes first
    let subscription = 
        simulationEngine.AtmosphereStateChanged
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(fun state ->
                seeingCondition.Value <- state.SeeingCondition
                cloudCoverage.Value <- state.CloudCoverage
                transparency.Value <- state.Transparency)
    
    // Initialize with current state after subscription setup
    do
        let currentAtmosphereState = simulationEngine.CurrentState.Atmosphere
        seeingCondition.Value <- currentAtmosphereState.SeeingCondition
        cloudCoverage.Value <- currentAtmosphereState.CloudCoverage
        transparency.Value <- currentAtmosphereState.Transparency
    
    // Properties
    member _.SeeingCondition = seeingCondition
    member _.CloudCoverage = cloudCoverage
    member _.Transparency = transparency
    
    // Commands
    member _.SetSeeingCommand = ReactiveCommand.create (fun (seeing: float) -> 
        Logger.logf "Setting seeing condition to: {0} arcseconds" [|seeing|]
        simulationEngine.PostMessage(SetSeeingCondition seeing))
    
    member _.SetCloudCoverageCommand = ReactiveCommand.create (fun (clouds: float) -> 
        Logger.logf "Setting cloud coverage to: {0}%" [|clouds * 100.0|]
        simulationEngine.PostMessage(SetCloudCoverage clouds))
    
    member _.SetTransparencyCommand = ReactiveCommand.create (fun (trans: float) -> 
        Logger.logf "Setting transparency to: {0}%" [|trans * 100.0|]
        simulationEngine.PostMessage(SetTransparency trans))
    
    // Weather preset commands - updated to use createSimple for unit commands
    member _.SetClearNightCommand = ReactiveCommand.createSimple (fun () -> 
        Logger.log "Setting 'Clear Night' atmospheric preset"
        simulationEngine.PostMessage(SetSeeingCondition 1.2)
        simulationEngine.PostMessage(SetCloudCoverage 0.0)
        simulationEngine.PostMessage(SetTransparency 0.95))
    
    member _.SetAverageSeeingCommand = ReactiveCommand.createSimple (fun () -> 
        Logger.log "Setting 'Average Seeing' atmospheric preset"
        simulationEngine.PostMessage(SetSeeingCondition 2.5)
        simulationEngine.PostMessage(SetTransparency 0.8))
    
    member _.SetPartlyCloudyCommand = ReactiveCommand.createSimple (fun () -> 
        Logger.log "Setting 'Partly Cloudy' atmospheric preset"
        simulationEngine.PostMessage(SetCloudCoverage 0.4)
        simulationEngine.PostMessage(SetTransparency 0.7))
    
    member _.SetVeryCloudyCommand = ReactiveCommand.createSimple (fun () -> 
        Logger.log "Setting 'Very Cloudy' atmospheric preset"
        simulationEngine.PostMessage(SetCloudCoverage 0.8)
        simulationEngine.PostMessage(SetTransparency 0.4))
    
    override _.Dispose(disposing) =
        if disposing then
            subscription.Dispose()

// Updated RotatorViewModel with unit command support
type RotatorViewModel(simulationEngine: SimulationEngine) =
    inherit ViewModelBase()
    
    // Observable state
    let position = ReactiveProperty<float>()
    let isMoving = ReactiveProperty<bool>()
    
    // Subscribe to simulation state changes first
    let subscription = 
        simulationEngine.RotatorStateChanged
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(fun state ->
                Logger.logf "RotatorViewModel received state update: Position={0}, IsMoving={1}" 
                    [|state.Position; state.IsMoving|]
                position.Value <- state.Position
                isMoving.Value <- state.IsMoving)
    
    // Initialize after subscription setup
    do
        let currentRotatorState = simulationEngine.CurrentState.Rotator
        position.Value <- currentRotatorState.Position
        isMoving.Value <- currentRotatorState.IsMoving
    
    // Properties
    member _.Position = position
    member _.IsMoving = isMoving
    
    // Commands
    member _.SetPositionCommand = ReactiveCommand.create (fun (angle: float) -> 
        Logger.logf "Setting rotator position to: {0} degrees" [|angle|]
        simulationEngine.PostMessage(SetRotatorPosition angle))
    
    // Add preset angle commands - updated to use createSimple
    member _.SetAngle0Command = ReactiveCommand.createSimple (fun () -> 
        Logger.log "Setting rotator to 0 degrees"
        simulationEngine.PostMessage(SetRotatorPosition 0.0))
    
    member _.SetAngle90Command = ReactiveCommand.createSimple (fun () -> 
        Logger.log "Setting rotator to 90 degrees"
        simulationEngine.PostMessage(SetRotatorPosition 90.0))
    
    member _.SetAngle180Command = ReactiveCommand.createSimple (fun () -> 
        Logger.log "Setting rotator to 180 degrees"
        simulationEngine.PostMessage(SetRotatorPosition 180.0))
    
    member _.SetAngle270Command = ReactiveCommand.createSimple (fun () -> 
        Logger.log "Setting rotator to 270 degrees"
        simulationEngine.PostMessage(SetRotatorPosition 270.0))
    
    override _.Dispose(disposing) =
        if disposing then
            subscription.Dispose()

type StarFieldViewModel(simulationEngine: SimulationEngine) =
    inherit ViewModelBase()
    
    // Observable state
    let currentImage = ReactiveProperty<float[]>()
    let hasImage = ReactiveProperty<bool>()
    
    // Stretch parameters with adjusted defaults
    let logarithmicStretch = ReactiveProperty<double>(0)
    let blackPoint = ReactiveProperty<double>(0.0)
    let whitePoint = ReactiveProperty<double>(100.0)
    let imageWidth = ReactiveProperty<int>(800)
    let imageHeight = ReactiveProperty<int>(600)
    
    // Subscribe to camera state to get correct dimensions
    let cameraSubscription = 
        simulationEngine.CameraStateChanged
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(fun cameraState ->
                imageWidth.Value <- cameraState.Width
                imageHeight.Value <- cameraState.Height)
    
    // Subscribe to new images
    let imageSubscription = 
        simulationEngine.ImageGenerated
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(fun image ->
                currentImage.Value <- image
                hasImage.Value <- true)

    // Properties
    member _.CurrentImage = currentImage
    member _.HasImage = hasImage
    member _.LogarithmicStretch = logarithmicStretch
    member _.BlackPoint = blackPoint
    member _.WhitePoint = whitePoint
    member _.ImageWidth = imageWidth
    member _.ImageHeight = imageHeight
    
    // Commands
    member _.GenerateSatelliteTrailCommand = ReactiveCommand.createSimple (fun () -> 
        Logger.log "Generating satellite trail"
        simulationEngine.PostMessage(GenerateSatelliteTrail))
    
    override _.Dispose(disposing) =
        if disposing then
            imageSubscription.Dispose()
            cameraSubscription.Dispose()

// Main View Model with all the sub-view models
type MainViewModel(simulationEngine: SimulationEngine) =
    inherit ViewModelBase()
    
    // Child ViewModels
    let mountViewModel = new MountViewModel(simulationEngine)
    let cameraViewModel = new CameraViewModel(simulationEngine)
    let atmosphereViewModel = new AtmosphereViewModel(simulationEngine)
    let rotatorViewModel = new RotatorViewModel(simulationEngine)
    let starFieldViewModel = new StarFieldViewModel(simulationEngine)
    
    // Properties
    member _.Mount = mountViewModel
    member _.Camera = cameraViewModel
    member _.Atmosphere = atmosphereViewModel
    member _.Rotator = rotatorViewModel
    member _.StarField = starFieldViewModel
    
    // Commands - updated to use createSimple
    member _.GenerateSatelliteTrailCommand = ReactiveCommand.createSimple (fun () -> 
        Logger.log "Generating satellite trail from main view model"
        simulationEngine.PostMessage(GenerateSatelliteTrail))
    
    override this.Dispose(disposing) =
        if disposing then
            (mountViewModel :> IDisposable).Dispose()
            (cameraViewModel :> IDisposable).Dispose()
            (atmosphereViewModel :> IDisposable).Dispose()
            (rotatorViewModel :> IDisposable).Dispose()
            (starFieldViewModel :> IDisposable).Dispose()