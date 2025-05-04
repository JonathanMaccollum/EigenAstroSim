namespace EigenAstroSim.UI

open System
open System.Reactive.Linq
open System.Reactive.Disposables
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
    let logger = Logger.getLogger<ReactiveCommand<'T>>()
    let canExecuteSubject = new BehaviorSubject<bool>(true)
    let canExecuteEvent = new Event<EventHandler, EventArgs>()
    
    do
        canExecute.Subscribe(fun value -> 
            logger.Infof "CanExecute changed to: %A" value
            canExecuteSubject.OnNext(value)
            canExecuteEvent.Trigger(null, EventArgs.Empty)
        ) |> ignore
    
    interface ICommand with
        [<CLIEvent>]
        member _.CanExecuteChanged = canExecuteEvent.Publish
        
        member _.CanExecute(parameter) = 
            let result = canExecuteSubject.Value
            logger.Infof "CanExecute check returned: %b for parameter: %s" 
                result (if isNull parameter then "null" else parameter.ToString())
            result
        
        member _.Execute(parameter) =
            logger.Infof "Command execution started with parameter: %s of type %s" 
                (if isNull parameter then "null" else parameter.ToString())
                (if isNull parameter then "null" else parameter.GetType().Name)
            
            try
                // Check if this is a unit command - handle this first!
                if typeof<'T> = typeof<unit> then
                    logger.Info "Executing unit command (no parameters needed)"
                    execute Unchecked.defaultof<'T>
                    
                // If parameter is null but command expects a value, handle based on type
                else if isNull parameter then
                    match typeof<'T>.Name with
                    | "String" -> 
                        logger.Info "Converting null to empty string for string parameter"
                        execute ("" :> obj :?> 'T)
                    | "Boolean" -> 
                        logger.Info "Converting null to false for boolean parameter"
                        execute (false :> obj :?> 'T)
                    | "Int32" -> 
                        logger.Info "Converting null to 0 for int parameter"
                        execute (0 :> obj :?> 'T)
                    | "Double" | "Single" -> 
                        logger.Info "Converting null to 0.0 for float parameter"
                        execute (0.0 :> obj :?> 'T)
                    | _ ->
                        logger.Infof "Cannot convert null to target type %A" typeof<'T>.Name
                
                // Parameter matches target type exactly
                else if parameter :? 'T then
                    logger.Info "Parameter matched target type directly"
                    execute (parameter :?> 'T)
                
                // Handle string conversions for common types
                else if parameter :? string then
                    let strParam = parameter :?> string
                    
                    match typeof<'T>.Name with
                    | "Double" | "Single" -> 
                        logger.Infof "Converting string to float: %A" strParam
                        try
                            execute (float strParam :> obj :?> 'T)
                        with ex -> 
                            logger.ErrorException ex "converting string to float"
                    
                    | "Int32" -> 
                        logger.Infof "Converting string to int: %A" strParam
                        try
                            execute (int strParam :> obj :?> 'T)
                        with ex -> 
                            logger.ErrorException ex "converting string to int"
                    
                    | "Boolean" -> 
                        logger.Infof "Converting string to bool: %A" [|strParam|]
                        try
                            execute (Boolean.Parse(strParam) :> obj :?> 'T)
                        with ex -> 
                            logger.ErrorException ex "converting string to bool"
                            
                    | "Tuple`2" when typeof<'T>.GenericTypeArguments.Length = 2 ->
                        // Handle tuple parameters from comma-separated strings (for RA,Dec coordinates)
                        logger.Infof "Attempting to convert string to tuple: %A" [|strParam|]
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
                                    logger.Infof "Unsupported tuple conversion: %s, %s" 
                                        (arg1Type.Name) (arg2Type.Name)
                            else
                                logger.Info "String doesn't contain exactly 2 parts for tuple conversion"
                        with ex ->
                            logger.ErrorException ex "converting string to tuple"
                    
                    | _ -> 
                        logger.Infof "Cannot convert string to target type %s" typeof<'T>.Name
            with ex ->
                logger.ErrorException ex "executing command"

// Command factory
module ReactiveCommand =
    // Simple command creation without parameters (unit commands)
    let createSimple (action: unit -> unit) =
        ReactiveCommand<unit>(Observable.Return(true), (fun _ -> action()))
    
    // Command creation with typed parameter
    let create (execute: 'T -> unit) =
        ReactiveCommand<'T>(Observable.Return(true), execute)

// MountViewModel with updated commands
type MountViewModel(simulationEngine: SimulationEngine) =
    inherit ViewModelBase()
    let logger = Logger.getLogger<MountViewModel>()

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
    let isCableSnagActive = ReactiveProperty<bool>(false)
    let cableSnagButtonText = ReactiveProperty<string>("Enable Cable Snag")
    
    // Slew speed options (degrees per second)
    let slewSpeedOptions = [| 0.5; 1.0; 2.0; 4.0; 8.0; 16.0; 32.0; 64.0; |]
    let selectedSlewSpeed = ReactiveProperty<float>()
    let cleanup = new CompositeDisposable()
    // In the MountViewModel constructor, add:
    do
        polarAlignmentError
            .AsObservable()
            .Skip(1)  // Skip the initial value to avoid immediately overwriting
            .DistinctUntilChanged()
            .Subscribe(fun error -> 
                simulationEngine.SetPolarAlignmentError error)
            |> cleanup.Add
        periodicErrorAmplitude
            .AsObservable()
            .Skip(1)
            .DistinctUntilChanged()
            .Subscribe(fun amplitude -> 
                simulationEngine.SetPeriodicError(amplitude, periodicErrorPeriod.Value))
            |> cleanup.Add
        periodicErrorPeriod
            .AsObservable()
            .Skip(1)
            .DistinctUntilChanged()
            .Subscribe(fun period -> 
                simulationEngine.SetPeriodicError(periodicErrorAmplitude.Value, period))
            |> cleanup.Add

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
            |> cleanup.Add
        Observable.Interval(TimeSpan.FromSeconds(0.1))
            .WithLatestFrom(isCableSnagActive.AsObservable())
            .Where(fun struct (_, isActive) -> isActive)
            .Subscribe(fun _ ->
                let raAmount = 0.0002 // Smaller continuous effect
                let decAmount = 0.0001
                simulationEngine.SimulateCableSnag(raAmount, decAmount)
            ) |> cleanup.Add
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
            |> cleanup.Add
    
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
    member _.IsCableSnagActive = isCableSnagActive
    member _.CableSnagButtonText = cableSnagButtonText

    member _.NudgeNorthCommand = ReactiveCommand.createSimple (fun () -> 
        logger.Infof "Executing North nudge with speed: %f" selectedSlewSpeed.Value
        simulationEngine.Nudge(0.0, selectedSlewSpeed.Value, 0.1))
    
    member _.NudgeSouthCommand = ReactiveCommand.createSimple (fun () -> 
        logger.Infof "Executing South nudge with speed: %f" selectedSlewSpeed.Value
        simulationEngine.Nudge(0.0, -selectedSlewSpeed.Value, 0.1))
    
    member _.NudgeEastCommand = ReactiveCommand.createSimple (fun () -> 
        logger.Infof "Executing East nudge with speed: %f" selectedSlewSpeed.Value
        simulationEngine.Nudge(-selectedSlewSpeed.Value, 0.0, 0.1))
    
    member _.NudgeWestCommand = ReactiveCommand.createSimple (fun () -> 
        logger.Infof "Executing West nudge with speed: %f" selectedSlewSpeed.Value
        simulationEngine.Nudge(selectedSlewSpeed.Value, 0.0, 0.1))
    
    member _.SetTrackingCommand = ReactiveCommand.create (fun (isOn: bool) -> 
        logger.Infof "Setting tracking to: %b" isOn
        let rate = if isOn then siderealRate else 0.0
        simulationEngine.SetTrackingRate rate)
    
    member _.SetPolarErrorCommand = ReactiveCommand.create (fun (error: float) -> 
        logger.Infof "Setting polar alignment error: %f degrees" error
        simulationEngine.SetPolarAlignmentError error)
    
    member _.SetPeriodicErrorCommand = ReactiveCommand.create (fun (amplitude, period) -> 
        logger.Infof "Setting periodic error: amplitude=%f, period=%f" amplitude period
        simulationEngine.SetPeriodicError(amplitude, period))
    
    // Specifically for tuple parameters
    member _.SlewToCommand = ReactiveCommand.create (fun (raDecTuple: obj) -> 
        match raDecTuple with
        | :? Tuple<float, float> as tuple -> 
            let (ra, dec) = tuple
            logger.Infof "Executing SlewTo: RA=%f, Dec=%A" ra dec
            simulationEngine.SlewTo(ra, dec)
        | _ -> 
            logger.Info "Invalid parameter type for SlewToCommand")
    
    // For text input coordinates
    member _.SlewToCoordinatesCommand = ReactiveCommand.create (fun (coordText: string) -> 
        logger.Infof "Executing SlewToCoordinates with input: %s" coordText
        try
            // Parse coordinates in format "RA,Dec" 
            let parts = coordText.Split(',')
            if parts.Length = 2 then
                let ra = float (parts.[0].Trim())
                let dec = float (parts.[1].Trim())
                simulationEngine.SlewTo(ra, dec)
            else
                logger.Info "Invalid coordinate format: expected RA,Dec"
        with ex -> 
            logger.ErrorException ex "parsing coordinates")
    
    member _.SetSlewSpeedCommand = ReactiveCommand.create (fun (speed: float) -> 
        logger.Infof "Setting slew speed to: %f" speed
        selectedSlewSpeed.Value <- speed
        let newMountState = { simulationEngine.CurrentState.Mount with SlewRate = speed }
        simulationEngine.UpdateMount newMountState)
    member _.ToggleCableSnagCommand = ReactiveCommand.createSimple (fun () -> 
        isCableSnagActive.Value <- not isCableSnagActive.Value
        
        if isCableSnagActive.Value then
            cableSnagButtonText.Value <- "Remove Cable Snag"
            logger.Info "Activating cable snag effect"
            let raAmount = 0.002
            let decAmount = 0.001
            simulationEngine.SimulateCableSnag(raAmount, decAmount)
        else
            cableSnagButtonText.Value <- "Enable Cable Snag"
            logger.Info "Removing cable snag effect"
    )

    override this.Dispose(disposing) =
        if disposing then
            cleanup.Dispose()


// Updated CameraViewModel with continuous capture using the SimulationEngine
type CameraViewModel(simulationEngine: SimulationEngine) =
    inherit ViewModelBase()
    let logger = Logger.getLogger<CameraViewModel>()    
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
    let cleanup = new CompositeDisposable()
    
    do
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
            |> cleanup.Add

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
        
        exposureTime.AsObservable()
            .Skip(1) // Skip initial value
            .DistinctUntilChanged()
            .Subscribe(fun time ->
                // Update the camera state with the new exposure time
                let newCamera = { simulationEngine.CurrentState.Camera with ExposureTime = time }
                simulationEngine.UpdateCamera newCamera
                logger.Infof "Exposure time updated to: %.1f seconds" time) 
            |> cleanup.Add
        binning
            .AsObservable()
            .Skip(1)
            .DistinctUntilChanged()
            .Subscribe(fun bin ->
                if availableBinning |> Array.contains bin then
                    logger.Infof "Binning changed to: %d" bin
                    let newCamera = { simulationEngine.CurrentState.Camera with Binning = bin }
                    simulationEngine.UpdateCamera newCamera
                else
                    logger.Infof "Invalid binning value: %d" bin) 
            |> cleanup.Add
        readNoise.AsObservable()
            .Skip(1) // Skip initial value
            .DistinctUntilChanged()
            .Subscribe(fun x ->
                let newCamera = { simulationEngine.CurrentState.Camera with ReadNoise = x }
                simulationEngine.UpdateCamera newCamera
                logger.Infof "Read noise updated to: %.1f" x) 
            |> cleanup.Add
        darkCurrent.AsObservable()
            .Skip(1) // Skip initial value
            .DistinctUntilChanged()
            .Subscribe(fun x ->
                let newCamera = { simulationEngine.CurrentState.Camera with DarkCurrent = x }
                simulationEngine.UpdateCamera newCamera
                logger.Infof "Dark current updated to: %.1f" x) 
            |> cleanup.Add

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
                logger.Info "Starting continuous capture mode"
                simulationEngine.SetContinuousCapture true
            else
                captureButtonText.Value <- "Start Capturing"
                logger.Info "Stopping continuous capture mode"
                simulationEngine.SetContinuousCapture false
                
                // Stop current exposure if one is in progress
                if isExposing.Value then
                    simulationEngine.StopExposure()
        )
    
    member _.SetExposureTimeCommand = 
        ReactiveCommand.create (fun (time: float) -> 
            logger.Infof "Setting exposure time to: %f seconds" time
            let newCamera = { simulationEngine.CurrentState.Camera with ExposureTime = time }
            simulationEngine.UpdateCamera newCamera)
    
    member _.SetBinningCommand = 
        ReactiveCommand.create (fun (bin: int) -> 
            logger.Infof "Setting binning to: %i" bin
            if availableBinning |> Array.contains bin then
                let newCamera = { simulationEngine.CurrentState.Camera with Binning = bin }
                simulationEngine.UpdateCamera newCamera)
    
    member _.SetReadNoiseCommand = 
        ReactiveCommand.create (fun (noise: float) -> 
            logger.Infof "Setting read noise to: %f" noise
            let newCamera = { simulationEngine.CurrentState.Camera with ReadNoise = noise }
            simulationEngine.UpdateCamera newCamera)
    
    member _.SetDarkCurrentCommand = 
        ReactiveCommand.create (fun (dark: float) -> 
            logger.Infof "Setting dark current to: %f" dark
            let newCamera = { simulationEngine.CurrentState.Camera with DarkCurrent = dark }
            simulationEngine.UpdateCamera newCamera)
    
    // Clean up subscriptions
    override this.Dispose(disposing) =
        if disposing then
            cleanup.Dispose()
            
            // Ensure capturing is stopped
            if isCapturing.Value then
                simulationEngine.SetContinuousCapture false
                if isExposing.Value then
                    simulationEngine.StopExposure()

// Updated AtmosphereViewModel with unit command support
type AtmosphereViewModel(simulationEngine: SimulationEngine) =
    inherit ViewModelBase()
    let logger = Logger.getLogger<AtmosphereViewModel>()

    // Observable state
    let seeingCondition = ReactiveProperty<float>()
    let cloudCoverage = ReactiveProperty<float>()
    let transparency = ReactiveProperty<float>()
    let cleanup = new CompositeDisposable()
    do
        seeingCondition
            .AsObservable()
            .Skip(1)  // Skip the initial value to avoid immediately overwriting
            .DistinctUntilChanged()
            .Subscribe(fun x -> 
                simulationEngine.SetSeeingCondition x)
            |> cleanup.Add
        cloudCoverage
            .AsObservable()
            .Skip(1)  // Skip the initial value to avoid immediately overwriting
            .DistinctUntilChanged()
            .Subscribe(fun x -> 
                simulationEngine.SetCloudCoverage x)
            |> cleanup.Add
        transparency
            .AsObservable()
            .Skip(1)  // Skip the initial value to avoid immediately overwriting
            .DistinctUntilChanged()
            .Subscribe(fun x -> 
                simulationEngine.SetTransparency x)
            |> cleanup.Add

        simulationEngine.AtmosphereStateChanged
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(fun state ->
                seeingCondition.Value <- state.SeeingCondition
                cloudCoverage.Value <- state.CloudCoverage
                transparency.Value <- state.Transparency)
        |> cleanup.Add
    
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
        logger.Infof "Setting seeing condition to: %f arcseconds" seeing
        simulationEngine.SetSeeingCondition seeing)
    
    member _.SetCloudCoverageCommand = ReactiveCommand.create (fun (clouds: float) -> 
        logger.Infof "Setting cloud coverage to: %f pct" (clouds * 100.0)
        simulationEngine.SetCloudCoverage clouds)
    
    member _.SetTransparencyCommand = ReactiveCommand.create (fun (trans: float) -> 
        logger.Infof "Setting transparency to: %f pct" (trans * 100.0)
        simulationEngine.SetTransparency trans)
    
    // Weather preset commands - updated to use createSimple for unit commands
    member _.SetClearNightCommand = ReactiveCommand.createSimple (fun () -> 
        logger.Info "Setting 'Clear Night' atmospheric preset"
        simulationEngine.SetSeeingCondition 1.2
        simulationEngine.SetCloudCoverage 0.0
        simulationEngine.SetTransparency 0.95)
    
    member _.SetAverageSeeingCommand = ReactiveCommand.createSimple (fun () -> 
        logger.Info "Setting 'Average Seeing' atmospheric preset"
        simulationEngine.SetSeeingCondition 2.5
        simulationEngine.SetTransparency 0.8)
    
    member _.SetPartlyCloudyCommand = ReactiveCommand.createSimple (fun () -> 
        logger.Info "Setting 'Partly Cloudy' atmospheric preset"
        simulationEngine.SetCloudCoverage 0.4
        simulationEngine.SetTransparency 0.7)
    
    member _.SetVeryCloudyCommand = ReactiveCommand.createSimple (fun () -> 
        logger.Info "Setting 'Very Cloudy' atmospheric preset"
        simulationEngine.SetCloudCoverage 0.8
        simulationEngine.SetTransparency 0.4)
    
    override _.Dispose(disposing) =
        if disposing then
            cleanup.Dispose()

// Updated RotatorViewModel with unit command support
type RotatorViewModel(simulationEngine: SimulationEngine) =
    inherit ViewModelBase()
    let logger = Logger.getLogger<RotatorViewModel>()    
    // Observable state
    let position = ReactiveProperty<float>()
    let isMoving = ReactiveProperty<bool>()
    let cleanup = new CompositeDisposable()

    do
        simulationEngine.RotatorStateChanged
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(fun state ->
                logger.Infof "RotatorViewModel received state update: Position=%f, IsMoving=%b" 
                    state.Position state.IsMoving
                position.Value <- state.Position
                isMoving.Value <- state.IsMoving)
            |> cleanup.Add
        position.AsObservable()
            .Skip(1)  // Skip the initial value to avoid immediately overwriting
            .DistinctUntilChanged()
            .Subscribe(fun x -> 
                simulationEngine.SetRotatorPosition x)
            |> cleanup.Add

        let currentRotatorState = simulationEngine.CurrentState.Rotator
        position.Value <- currentRotatorState.Position
        isMoving.Value <- currentRotatorState.IsMoving
    
    // Properties
    member _.Position = position
    member _.IsMoving = isMoving
    
    // Commands
    member _.SetPositionCommand = ReactiveCommand.create (fun (angle: float) -> 
        logger.Infof "Setting rotator position to: %f degrees" angle
        simulationEngine.SetRotatorPosition angle)
    
    // Add preset angle commands - updated to use createSimple
    member _.SetAngle0Command = ReactiveCommand.createSimple (fun () -> 
        logger.Info "Setting rotator to 0 degrees"
        simulationEngine.SetRotatorPosition 0.0)
    
    member _.SetAngle90Command = ReactiveCommand.createSimple (fun () -> 
        logger.Info "Setting rotator to 90 degrees"
        simulationEngine.SetRotatorPosition 90.0)
    
    member _.SetAngle180Command = ReactiveCommand.createSimple (fun () -> 
        logger.Info "Setting rotator to 180 degrees"
        simulationEngine.SetRotatorPosition 180.0)
    
    member _.SetAngle270Command = ReactiveCommand.createSimple (fun () -> 
        logger.Info "Setting rotator to 270 degrees"
        simulationEngine.SetRotatorPosition 270.0)
    
    override _.Dispose(disposing) =
        if disposing then
            cleanup.Dispose()

type StarFieldViewModel(simulationEngine: SimulationEngine) =
    inherit ViewModelBase()
    let logger = Logger.getLogger<StarFieldViewModel>()    
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
        logger.Info "Generating satellite trail"
        simulationEngine.GenerateSatelliteTrail())
    
    override _.Dispose(disposing) =
        if disposing then
            imageSubscription.Dispose()
            cameraSubscription.Dispose()

// Main View Model with all the sub-view models
type MainViewModel(simulationEngine: SimulationEngine) =
    inherit ViewModelBase()
    let logger = Logger.getLogger<MainViewModel>()    
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
        logger.Info "Generating satellite trail from main view model"
        simulationEngine.GenerateSatelliteTrail())
    
    override this.Dispose(disposing) =
        if disposing then
            (mountViewModel :> IDisposable).Dispose()
            (cameraViewModel :> IDisposable).Dispose()
            (atmosphereViewModel :> IDisposable).Dispose()
            (rotatorViewModel :> IDisposable).Dispose()
            (starFieldViewModel :> IDisposable).Dispose()