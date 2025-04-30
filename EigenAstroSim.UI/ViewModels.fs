namespace EigenAstroSim.UI

open System
open System.Reactive.Linq
open System.Collections.ObjectModel
open System.Collections.Generic
open System.Windows.Input
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
type ReactiveProperty<'T>() =
    let mutable value = Unchecked.defaultof<'T>
    let changed = new System.Reactive.Subjects.Subject<'T>()
    
    member this.Value
        with get() = value
        and set(v) =
            if not (EqualityComparer<'T>.Default.Equals(value, v)) then
                value <- v
                changed.OnNext(v)
    
    member this.AsObservable() = changed.AsObservable()


// Command implementation
type ReactiveCommand<'T>(canExecute: IObservable<bool>, execute: 'T -> unit) =
    let canExecuteSubject = new BehaviorSubject<bool>(true)
    let canExecuteEvent = new Event<EventHandler, EventArgs>()
    
    do
        canExecute.Subscribe(fun value -> 
            canExecuteSubject.OnNext(value)
            canExecuteEvent.Trigger(null, EventArgs.Empty)
        ) |> ignore
    
    interface ICommand with
        [<CLIEvent>]
        member _.CanExecuteChanged = canExecuteEvent.Publish
        
        member _.CanExecute(_) = canExecuteSubject.Value
        
        member _.Execute(parameter) =
            match parameter with
            | :? 'T as typedParam -> execute typedParam
            | :? string as strParam when typeof<'T> = typeof<float> -> 
                try
                    execute (float strParam :> obj :?> 'T)
                with _ -> ()
            | :? string as strParam when typeof<'T> = typeof<int> -> 
                try
                    execute (int strParam :> obj :?> 'T)
                with _ -> ()
            | :? string as strParam when typeof<'T> = typeof<bool> -> 
                try
                    execute (Boolean.Parse(strParam) :> obj :?> 'T)
                with _ -> ()
            | _ when typeof<'T> = typeof<unit> -> execute Unchecked.defaultof<'T>
            | _ -> 
                System.Diagnostics.Debug.WriteLine(
                    sprintf "Cannot convert parameter of type %s to target type %s" 
                        (if isNull parameter then "null" else parameter.GetType().Name) 
                        typeof<'T>.Name)

// Command factory
module ReactiveCommand =
    let create (execute: 'T -> unit) =
        ReactiveCommand<'T>(Observable.Return(true), execute)
        
    let createWithCanExecute (canExecute: IObservable<bool>) (execute: 'T -> unit) =
        ReactiveCommand<'T>(canExecute, execute)
        
    let createFromObservable (canExecute: IObservable<bool>) (execute: 'T -> IObservable<unit>) =
        ReactiveCommand<'T>(canExecute, (fun param -> execute param |> Observable.subscribe ignore |> ignore))

// Updated MountViewModel with fixed initialization order and SlewToCoordinates command
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
    
    // Slew speed options (degrees per second)
    let slewSpeedOptions = [| 0.1; 0.5; 1.0; 3.0; 5.0 |]
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
    
    // Commands
    member _.NudgeNorthCommand = ReactiveCommand.create (fun _ -> 
        simulationEngine.PostMessage(Nudge(0.0, selectedSlewSpeed.Value, 0.1)))
    
    member _.NudgeSouthCommand = ReactiveCommand.create (fun _ -> 
        simulationEngine.PostMessage(Nudge(0.0, -selectedSlewSpeed.Value, 0.1)))
    
    member _.NudgeEastCommand = ReactiveCommand.create (fun _ -> 
        simulationEngine.PostMessage(Nudge(-selectedSlewSpeed.Value, 0.0, 0.1)))
    
    member _.NudgeWestCommand = ReactiveCommand.create (fun _ -> 
        simulationEngine.PostMessage(Nudge(selectedSlewSpeed.Value, 0.0, 0.1)))
    
    member _.SetTrackingCommand = ReactiveCommand.create (fun isOn -> 
        let rate = if isOn then siderealRate else 0.0
        simulationEngine.PostMessage(SetTrackingRate rate))
    
    member _.SetPolarErrorCommand = ReactiveCommand.create (fun (error: float) -> 
        simulationEngine.PostMessage(SetPolarAlignmentError error))
    
    member _.SetPeriodicErrorCommand = ReactiveCommand.create (fun (amplitude, period) -> 
        simulationEngine.PostMessage(SetPeriodicError(amplitude, period)))
    
    member _.SlewToCommand = ReactiveCommand.create (fun (raDecTuple: obj) -> 
        // Handle tuple parameter for RA/Dec
        match raDecTuple with
        | :? System.Tuple<float, float> as tuple -> 
            let ra, dec = tuple
            simulationEngine.PostMessage(SlewTo(ra, dec))
        | _ -> ())
    
    // Add a new command that can handle a string coordinate input
    member _.SlewToCoordinatesCommand = ReactiveCommand.create (fun (coordText: string) -> 
        try
            // Parse coordinates in format "RA,Dec" 
            let parts = coordText.Split(',')
            if parts.Length = 2 then
                let ra = float (parts.[0].Trim())
                let dec = float (parts.[1].Trim())
                simulationEngine.PostMessage(SlewTo(ra, dec))
        with ex -> 
            System.Diagnostics.Debug.WriteLine($"Error parsing coordinates: {ex.Message}"))
    
    member _.SetSlewSpeedCommand = ReactiveCommand.create (fun (speed: float) -> 
        selectedSlewSpeed.Value <- speed
        let newMountState = { simulationEngine.CurrentState.Mount with SlewRate = speed }
        simulationEngine.PostMessage(UpdateMount newMountState))
    
    member _.SimulateCableSnagCommand = ReactiveCommand.create (fun _ -> 
        let raAmount = 0.002
        let decAmount = 0.001
        simulationEngine.PostMessage(SimulateCableSnag(raAmount, decAmount)))
    
    override this.Dispose(disposing) =
        if disposing then
            mountSubscription.Dispose()
            detailedMountSubscription.Dispose()

// And similar changes for other ViewModels (updating scheduler and ReactiveProperty usage)...

// Updated CameraViewModel with fixed initialization order
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
    
    // Commands
    member _.StartExposureCommand = 
        let canExecute = isExposing.AsObservable() |> Observable.map not
        ReactiveCommand.createWithCanExecute canExecute (fun _ -> 
            simulationEngine.PostMessage(StartExposure exposureTime.Value))
    
    member _.StopExposureCommand = 
        let canExecute = isExposing.AsObservable()
        ReactiveCommand.createWithCanExecute canExecute (fun _ -> 
            simulationEngine.PostMessage(StopExposure))
    
    member _.SetExposureTimeCommand = ReactiveCommand.create (fun (time: obj) -> 
        match time with
        | :? float as floatTime -> 
            let newCamera = { simulationEngine.CurrentState.Camera with ExposureTime = floatTime }
            simulationEngine.PostMessage(UpdateCamera newCamera)
        | :? string as strTime ->
            try
                let floatTime = float strTime
                let newCamera = { simulationEngine.CurrentState.Camera with ExposureTime = floatTime }
                simulationEngine.PostMessage(UpdateCamera newCamera)
            with ex ->
                System.Diagnostics.Debug.WriteLine($"Error converting exposure time: {ex.Message}")
        | _ -> 
            System.Diagnostics.Debug.WriteLine("Unexpected exposure time parameter type"))
    
    member _.SetBinningCommand = ReactiveCommand.create (fun (bin: obj) -> 
        match bin with
        | :? int as intBin -> 
            if availableBinning |> Array.contains intBin then
                let newCamera = { simulationEngine.CurrentState.Camera with Binning = intBin }
                simulationEngine.PostMessage(UpdateCamera newCamera)
        | :? string as strBin ->
            try
                let intBin = int strBin
                if availableBinning |> Array.contains intBin then
                    let newCamera = { simulationEngine.CurrentState.Camera with Binning = intBin }
                    simulationEngine.PostMessage(UpdateCamera newCamera)
            with ex ->
                System.Diagnostics.Debug.WriteLine($"Error converting binning: {ex.Message}")
        | _ -> 
            System.Diagnostics.Debug.WriteLine("Unexpected binning parameter type"))
    
    member _.SetReadNoiseCommand = ReactiveCommand.create (fun (noise: obj) -> 
        match noise with
        | :? float as floatNoise -> 
            let newCamera = { simulationEngine.CurrentState.Camera with ReadNoise = floatNoise }
            simulationEngine.PostMessage(UpdateCamera newCamera)
        | :? string as strNoise ->
            try
                let floatNoise = float strNoise
                let newCamera = { simulationEngine.CurrentState.Camera with ReadNoise = floatNoise }
                simulationEngine.PostMessage(UpdateCamera newCamera)
            with ex ->
                System.Diagnostics.Debug.WriteLine($"Error converting read noise: {ex.Message}")
        | _ -> 
            System.Diagnostics.Debug.WriteLine("Unexpected read noise parameter type"))
    
    member _.SetDarkCurrentCommand = ReactiveCommand.create (fun (dark: obj) -> 
        match dark with
        | :? float as floatDark -> 
            let newCamera = { simulationEngine.CurrentState.Camera with DarkCurrent = floatDark }
            simulationEngine.PostMessage(UpdateCamera newCamera)
        | :? string as strDark ->
            try
                let floatDark = float strDark
                let newCamera = { simulationEngine.CurrentState.Camera with DarkCurrent = floatDark }
                simulationEngine.PostMessage(UpdateCamera newCamera)
            with ex ->
                System.Diagnostics.Debug.WriteLine($"Error converting dark current: {ex.Message}")
        | _ -> 
            System.Diagnostics.Debug.WriteLine("Unexpected dark current parameter type"))
    
    override _.Dispose(disposing) =
        if disposing then
            subscription.Dispose()

// Updated AtmosphereViewModel with fixed initialization order and improved command parameters
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
    member _.SetSeeingCommand = ReactiveCommand.create (fun (seeing: obj) -> 
        match seeing with
        | :? float as floatVal -> 
            simulationEngine.PostMessage(SetSeeingCondition floatVal)
        | :? string as strVal ->
            try
                let floatVal = float strVal
                simulationEngine.PostMessage(SetSeeingCondition floatVal)
            with ex ->
                System.Diagnostics.Debug.WriteLine($"Error converting seeing condition: {ex.Message}")
        | _ -> 
            System.Diagnostics.Debug.WriteLine("Unexpected seeing parameter type"))
    
    member _.SetCloudCoverageCommand = ReactiveCommand.create (fun (clouds: obj) -> 
        match clouds with
        | :? float as floatVal -> 
            simulationEngine.PostMessage(SetCloudCoverage floatVal)
        | :? string as strVal ->
            try
                let floatVal = float strVal
                simulationEngine.PostMessage(SetCloudCoverage floatVal)
            with ex ->
                System.Diagnostics.Debug.WriteLine($"Error converting cloud coverage: {ex.Message}")
        | _ -> 
            System.Diagnostics.Debug.WriteLine("Unexpected cloud coverage parameter type"))
    
    member _.SetTransparencyCommand = ReactiveCommand.create (fun (trans: obj) -> 
        match trans with
        | :? float as floatVal -> 
            simulationEngine.PostMessage(SetTransparency floatVal)
        | :? string as strVal ->
            try
                let floatVal = float strVal
                simulationEngine.PostMessage(SetTransparency floatVal)
            with ex ->
                System.Diagnostics.Debug.WriteLine($"Error converting transparency: {ex.Message}")
        | _ -> 
            System.Diagnostics.Debug.WriteLine("Unexpected transparency parameter type"))
    
    // Weather preset commands
    member _.SetClearNightCommand = ReactiveCommand.create (fun _ -> 
        simulationEngine.PostMessage(SetSeeingCondition 1.2)
        simulationEngine.PostMessage(SetCloudCoverage 0.0)
        simulationEngine.PostMessage(SetTransparency 0.95))
    
    member _.SetAverageSeeingCommand = ReactiveCommand.create (fun _ -> 
        simulationEngine.PostMessage(SetSeeingCondition 2.5)
        simulationEngine.PostMessage(SetTransparency 0.8))
    
    member _.SetPartlyCloudyCommand = ReactiveCommand.create (fun _ -> 
        simulationEngine.PostMessage(SetCloudCoverage 0.4)
        simulationEngine.PostMessage(SetTransparency 0.7))
    
    member _.SetVeryCloudyCommand = ReactiveCommand.create (fun _ -> 
        simulationEngine.PostMessage(SetCloudCoverage 0.8)
        simulationEngine.PostMessage(SetTransparency 0.4))
    
    override _.Dispose(disposing) =
        if disposing then
            subscription.Dispose()

// Updated RotatorViewModel with fixed initialization order
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
    member _.SetPositionCommand = ReactiveCommand.create (fun (angle: obj) -> 
        match angle with
        | :? float as floatAngle -> 
            simulationEngine.PostMessage(SetRotatorPosition floatAngle)
        | :? string as strAngle ->
            try
                let floatAngle = float strAngle
                simulationEngine.PostMessage(SetRotatorPosition floatAngle)
            with ex ->
                System.Diagnostics.Debug.WriteLine($"Error converting angle: {ex.Message}")
        | _ -> 
            System.Diagnostics.Debug.WriteLine("Unexpected angle parameter type"))
    
    // Add preset angle commands
    member _.SetAngle0Command = ReactiveCommand.create (fun _ -> 
        simulationEngine.PostMessage(SetRotatorPosition 0.0))
    
    member _.SetAngle90Command = ReactiveCommand.create (fun _ -> 
        simulationEngine.PostMessage(SetRotatorPosition 90.0))
    
    member _.SetAngle180Command = ReactiveCommand.create (fun _ -> 
        simulationEngine.PostMessage(SetRotatorPosition 180.0))
    
    member _.SetAngle270Command = ReactiveCommand.create (fun _ -> 
        simulationEngine.PostMessage(SetRotatorPosition 270.0))
    
    override _.Dispose(disposing) =
        if disposing then
            subscription.Dispose()

// Star Field View Model
type StarFieldViewModel(simulationEngine: SimulationEngine) =
    inherit ViewModelBase()
    
    // Observable state
    let currentImage = ReactiveProperty<float[]>()
    let hasImage = ReactiveProperty<bool>()
    
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
    
    // Commands
    member _.GenerateSatelliteTrailCommand = ReactiveCommand.create (fun _ -> 
        simulationEngine.PostMessage(GenerateSatelliteTrail))
    
    override _.Dispose(disposing) =
        if disposing then
            imageSubscription.Dispose()

// Main View Model
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
    
    // Commands
    member _.GenerateSatelliteTrailCommand = ReactiveCommand.create (fun _ -> 
        simulationEngine.PostMessage(GenerateSatelliteTrail))
    
    override this.Dispose(disposing) =
        if disposing then
            (mountViewModel :> IDisposable).Dispose()
            (cameraViewModel :> IDisposable).Dispose()
            (atmosphereViewModel :> IDisposable).Dispose()
            (rotatorViewModel :> IDisposable).Dispose()
            (starFieldViewModel :> IDisposable).Dispose()