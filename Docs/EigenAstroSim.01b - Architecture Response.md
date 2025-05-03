# Astro Equipment Simulator - Architectural Design

## 1. Overview

The Astro Equipment Simulator is a desktop application that creates a virtual astrophotography environment for testing and development of astronomical imaging software. It simulates an equatorial mount, guide camera, and rotator with ASCOM-compliant interfaces, allowing other astrophotography software to connect to these virtual devices.

## 2. Core Architecture

The application follows a functional programming approach using F# with immutable data structures and composition. It implements the MVVM pattern for the UI with Reactive Extensions (Rx.NET) for handling asynchronous events and reactive workflows.

### 2.1 High-Level Component Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                       Astro Equipment Simulator                      │
├─────────────────────────────────────────────────────────────────────┤
│ ┌───────────────┐ ┌───────────────┐ ┌───────────────┐ ┌───────────┐ │
│ │ ASCOM Drivers │ │  Simulation   │ │    Image     │ │    UI     │ │
│ │               │ │    Engine     │ │  Generation  │ │           │ │
│ ├───────────────┤ ├───────────────┤ ├───────────────┤ ├───────────┤ │
│ │ - MountDriver │ │ - StateManager│ │ - StarField  │ │ - MainView│ │
│ │ - CameraDriver│ │ - Messaging   │ │ - Projection │ │ - ViewMods│ │
│ │ - RotatorDrvr │ │ - Clock       │ │ - Rendering  │ │ - Commands│ │
│ └───────┬───────┘ └───────┬───────┘ └───────┬───────┘ └─────┬─────┘ │
│         │                 │                 │               │       │
│         └─────────────────┼─────────────────┼───────────────┘       │
│                           │                 │                       │
│                  ┌────────┴──────────┐      │                       │
│                  │   Reactive Core   │      │                       │
│                  │   (Event Streams) │      │                       │
│                  └────────┬──────────┘      │                       │
│                           │                 │                       │
│                           └─────────────────┘                       │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

## 3. Domain Model

### 3.1 Core Domain Types

```fsharp
// Core domain types
type Star = {
    Id: Guid
    RA: float
    Dec: float
    Magnitude: float
    Color: float  // B-V index for star color
}

type StarFieldState = {
    Stars: Map<Guid, Star>
    ReferenceRA: float
    ReferenceDec: float
    ReferenceRotation: float
}

type MountState = {
    RA: float
    Dec: float
    TrackingRate: float
    PolarAlignmentError: float
    PeriodicErrorAmplitude: float
    PeriodicErrorPeriod: float
    IsSlewing: bool
    SlewRate: float
    FocalLength: float
}

type CameraState = {
    ExposureTime: float
    Binning: int
    IsExposing: bool
    LastImage: byte array option
    Width: int
    Height: int
    PixelSize: float
    ReadNoise: float
    DarkCurrent: float
}

type RotatorState = {
    Position: float  // In degrees
    IsMoving: bool
}

type AtmosphericState = {
    SeeingCondition: float  // In arcseconds
    CloudCoverage: float    // 0.0 to 1.0
    Transparency: float     // 0.0 to 1.0
}

type SimulationState = {
    StarField: StarFieldState
    Mount: MountState
    Camera: CameraState
    Rotator: RotatorState
    Atmosphere: AtmosphericState
    TimeScale: float        // For accelerating simulation
    CurrentTime: DateTime
    HasSatelliteTrail: bool
}
```

### 3.2 Message-Based Architecture

```fsharp
// Messages for state updates
type Msg =
    | UpdateMount of MountState
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
    | GenerateSatelliteTrail
    | AdvanceTime of seconds:float
```

## 4. Component Design

### 4.1 Simulation Engine

The Simulation Engine is the core component responsible for managing the simulation state and coordinating updates across all components.

```fsharp
type SimulationEngine() =
    // Internal state
    let mutable state = SimulationState.Default
    
    // Observable subjects for state changes
    let mountStateChanged = new Subject<MountState>()
    let cameraStateChanged = new Subject<CameraState>()
    let imageGenerated = new Subject<byte[]>()
    
    // Message processing mailbox
    let mailbox = MailboxProcessor.Start(fun inbox ->
        let rec loop() = async {
            let! msg = inbox.Receive()
            let newState = processMessage state msg
            state <- newState
            return! loop()
        }
        loop())
    
    // Message processing function
    let processMessage state msg =
        match msg with
        | UpdateMount mountState ->
            let newState = { state with Mount = mountState }
            mountStateChanged.OnNext(mountState)
            newState
        
        | StartExposure duration ->
            let camera = { state.Camera with IsExposing = true; ExposureTime = duration }
            let newState = { state with Camera = camera }
            cameraStateChanged.OnNext(camera)
            
            // Start async exposure process
            async {
                do! Async.Sleep(int (duration * 1000.0))
                let image = generateImage newState
                imageGenerated.OnNext(image)
                mailbox.Post(StopExposure)
            } |> Async.Start
            
            newState
        
        | SlewTo(ra, dec) ->
            // Start slewing process
            let mount = { state.Mount with IsSlewing = true }
            let newState = { state with Mount = mount }
            mountStateChanged.OnNext(mount)
            
            // Simulate slewing in the background
            async {
                let mutable currentState = newState
                let steps = 20
                
                for i in 1 .. steps do
                    do! Async.Sleep(100)
                    
                    // Calculate intermediate position
                    let progress = float i / float steps
                    let newRA = state.Mount.RA + progress * (ra - state.Mount.RA)
                    let newDec = state.Mount.Dec + progress * (dec - state.Mount.Dec)
                    
                    let mountUpdate = { currentState.Mount with RA = newRA; Dec = newDec }
                    mailbox.Post(UpdateMount mountUpdate)
                
                // Final position
                let finalMount = { newState.Mount with RA = ra; Dec = dec; IsSlewing = false }
                mailbox.Post(UpdateMount finalMount)
            } |> Async.Start
            
            newState
        
        // Handle other message types...
    
    // Public interface
    member this.PostMessage(msg) = mailbox.Post(msg)
    member this.MountStateChanged = mountStateChanged.AsObservable()
    member this.CameraStateChanged = cameraStateChanged.AsObservable()
    member this.ImageGenerated = imageGenerated.AsObservable()
```

### 4.2 Star Field Generator

The Star Field Generator creates and manages the virtual star field.

```fsharp
module StarFieldGenerator =
    // Generate a realistic distribution of stars
    let generateStarField centerRA centerDec fieldRadius limitingMagnitude =
        // Star density depends on galactic latitude
        let galacticLatitude = calculateGalacticLatitude centerRA centerDec
        let densityFactor = Math.Exp(-Math.Abs(galacticLatitude) / 30.0)
        
        // Approximate number of stars in field based on limiting magnitude
        let starCount = int (densityFactor * 100.0 * 
                             fieldRadius * fieldRadius * 
                             Math.Pow(10.0, 0.4 * (limitingMagnitude - 6.0)))
        
        // Generate stars with realistic distribution
        [| for _ in 1 .. starCount do
               // Position within field (random but uniform distribution)
               let r = fieldRadius * Math.Sqrt(random.NextDouble())
               let theta = random.NextDouble() * 2.0 * Math.PI
               
               let ra = centerRA + r * Math.Cos(theta) / Math.Cos(centerDec * Math.PI / 180.0)
               let dec = centerDec + r * Math.Sin(theta)
               
               // Magnitude follows a power law distribution
               let mag = generateStarMagnitude 1.0 limitingMagnitude
               
               // Color correlates with magnitude
               let color = generateStarColor mag
               
               yield {
                   Id = Guid.NewGuid()
                   RA = ra
                   Dec = dec
                   Magnitude = mag
                   Color = color
               }
        |]
        
    // Expand the star field when telescope moves to a new area
    let expandStarField (starField: StarFieldState) centerRA centerDec fieldRadius limitingMagnitude =
        // Check what area is not covered by existing stars
        let existingRegion = calculateCoveredRegion starField.Stars
        let newRegion = {
            CenterRA = centerRA
            CenterDec = centerDec
            Radius = fieldRadius
        }
        
        if regionContains existingRegion newRegion then
            // New region is already covered
            starField
        else
            // Generate additional stars to cover the new region
            let additionalStars = 
                generateStarField centerRA centerDec fieldRadius limitingMagnitude
                |> Array.filter (fun star -> not (starExists starField.Stars star.RA star.Dec))
            
            // Add new stars to the existing collection
            let updatedStars = 
                (starField.Stars, additionalStars) 
                ||> Array.fold (fun map star -> Map.add star.Id star map)
            
            { starField with Stars = updatedStars }
```

### 4.3 Image Generation

The Image Generator creates synthetic images based on the current simulation state.

```fsharp
module ImageGenerator =
    // Main image generation function
    let generateImage (state: SimulationState) =
        // 1. Get visible stars based on current pointing
        let visibleStars = getVisibleStars state.StarField state.Mount state.Camera
        
        // 2. Create empty image array
        let width, height = state.Camera.Width, state.Camera.Height
        let image = Array2D.create width height 0.0
        
        // 3. Project stars onto image plane
        let projectedStars = 
            visibleStars
            |> Array.map (fun star -> 
                projectStar star state.Mount state.Camera state.Rotator)
            |> Array.filter (fun (x, y, _, _) -> 
                x >= 0.0 && x < float width && y >= 0.0 && y < float height)
        
        // 4. Apply seeing effects
        let seenStars = 
            projectedStars
            |> Array.map (fun (x, y, mag, color) -> 
                applySeeingToStar (x, y, mag, color) state.Atmosphere.SeeingCondition)
        
        // 5. Apply cloud coverage
        let visibleThroughClouds =
            seenStars
            |> Array.filter (fun (_, _, mag, _, _) -> 
                mag < calculateMagnitudeThroughClouds state.Atmosphere.CloudCoverage)
        
        // 6. Render stars onto image
        let starImage = 
            (image, visibleThroughClouds)
            ||> Array.fold (fun img star -> renderStar star img state.Camera.ExposureTime)
        
        // 7. Add satellite trail if requested
        let imageWithTrail =
            if state.HasSatelliteTrail then 
                addSatelliteTrail starImage state.Camera
            else 
                starImage
        
        // 8. Apply noise
        let noisyImage = applySensorNoise imageWithTrail state.Camera
        
        // 9. Apply binning if needed
        let finalImage =
            if state.Camera.Binning > 1 then
                applyBinning noisyImage state.Camera.Binning
            else
                noisyImage
        
        // 10. Convert to byte array and return
        convertToByteArray finalImage
```

### 4.4 ASCOM Drivers

The ASCOM drivers provide standardized interfaces for external applications to connect to our virtual devices.

```fsharp
// Virtual Mount Driver
type VirtualMountDriver(simulationEngine: SimulationEngine) =
    let mutable connected = false
    let mutable clientId = ""
    
    // Listen for mount state changes
    let subscription = 
        simulationEngine.MountStateChanged.Subscribe(fun _ -> ())
    
    interface ASCOM.DeviceInterface.ITelescopeV3 with
        member this.Connected 
            with get() = connected
            and set(value) = 
                connected <- value
                if not connected then clientId <- ""
        
        member this.RightAscension 
            with get() = 
                if not connected then raise (ASCOM.NotConnectedException())
                simulationEngine.CurrentState.Mount.RA
        
        member this.Declination
            with get() = 
                if not connected then raise (ASCOM.NotConnectedException())
                simulationEngine.CurrentState.Mount.Dec
        
        member this.Tracking
            with get() = 
                if not connected then raise (ASCOM.NotConnectedException())
                simulationEngine.CurrentState.Mount.TrackingRate > 0.0
            and set(value) =
                if not connected then raise (ASCOM.NotConnectedException())
                let rate = if value then 1.0 else 0.0
                simulationEngine.PostMessage(SetTrackingRate rate)
        
        member this.SlewToCoordinates(rightAscension, declination) =
            if not connected then raise (ASCOM.NotConnectedException())
            simulationEngine.PostMessage(SlewTo(rightAscension, declination))
        
        member this.PulseGuide(direction, duration) =
            if not connected then raise (ASCOM.NotConnectedException())
            
            // Convert ASCOM direction to RA/Dec rates
            let (raRate, decRate) = 
                match direction with
                | ASCOM.DeviceInterface.GuideDirections.guideEast -> (-1.0, 0.0)
                | ASCOM.DeviceInterface.GuideDirections.guideWest -> (1.0, 0.0)
                | ASCOM.DeviceInterface.GuideDirections.guideNorth -> (0.0, 1.0)
                | ASCOM.DeviceInterface.GuideDirections.guideSouth -> (0.0, -1.0)
                | _ -> (0.0, 0.0)
            
            // Send nudge command
            let durationSec = float duration / 1000.0
            simulationEngine.PostMessage(Nudge(raRate, decRate, durationSec))
        
        // Implement other required interface methods...

// Virtual Camera Driver
type VirtualCameraDriver(simulationEngine: SimulationEngine) =
    let mutable connected = false
    let mutable lastImage: byte[] option = None
    
    // Listen for new images
    let imageSubscription = 
        simulationEngine.ImageGenerated.Subscribe(fun image ->
            lastImage <- Some image)
    
    interface ASCOM.DeviceInterface.ICameraV3 with
        member this.Connected
            with get() = connected
            and set(value) = connected <- value
        
        member this.StartExposure(duration, light) =
            if not connected then raise (ASCOM.NotConnectedException())
            if duration <= 0.0 then raise (ASCOM.InvalidValueException("Duration must be positive"))
            
            simulationEngine.PostMessage(StartExposure duration)
        
        member this.StopExposure() =
            if not connected then raise (ASCOM.NotConnectedException())
            simulationEngine.PostMessage(StopExposure)
        
        member this.ImageReady
            with get() = 
                if not connected then raise (ASCOM.NotConnectedException())
                lastImage.IsSome
        
        member this.ImageArray
            with get() =
                if not connected then raise (ASCOM.NotConnectedException())
                if lastImage.IsNone then raise (ASCOM.InvalidOperationException("No image is available"))
                
                convertByteArrayToImageArray lastImage.Value 
                    simulationEngine.CurrentState.Camera.Width 
                    simulationEngine.CurrentState.Camera.Height
                    simulationEngine.CurrentState.Camera.Binning
        
        member this.BinX
            with get() = 
                if not connected then raise (ASCOM.NotConnectedException())
                simulationEngine.CurrentState.Camera.Binning
            and set(value) =
                if not connected then raise (ASCOM.NotConnectedException())
                let camera = simulationEngine.CurrentState.Camera
                let newCamera = { camera with Binning = value }
                simulationEngine.PostMessage(UpdateCamera newCamera)
        
        member this.BinY
            with get() = 
                if not connected then raise (ASCOM.NotConnectedException())
                simulationEngine.CurrentState.Camera.Binning
            and set(value) =
                if not connected then raise (ASCOM.NotConnectedException())
                let camera = simulationEngine.CurrentState.Camera
                let newCamera = { camera with Binning = value }
                simulationEngine.PostMessage(UpdateCamera newCamera)
        
        // Implement other required interface methods...
```

### 4.5 MVVM UI Layer

The UI layer follows the MVVM pattern for clean separation of concerns.

```fsharp
// Main ViewModel
type MainViewModel(simulationEngine: SimulationEngine) =
    inherit ViewModelBase()
    
    // Child ViewModels
    let mountViewModel = MountViewModel(simulationEngine)
    let cameraViewModel = CameraViewModel(simulationEngine)
    let atmosphereViewModel = AtmosphereViewModel(simulationEngine)
    let starFieldViewModel = StarFieldViewModel(simulationEngine)
    
    // Properties
    member this.Mount = mountViewModel
    member this.Camera = cameraViewModel
    member this.Atmosphere = atmosphereViewModel
    member this.StarField = starFieldViewModel
    
    // Commands
    member this.GenerateSatelliteTrailCommand = 
        ReactiveCommand.Create(fun _ -> 
            simulationEngine.PostMessage(GenerateSatelliteTrail))

// Mount ViewModel
type MountViewModel(simulationEngine: SimulationEngine) =
    inherit ViewModelBase()
    
    // Observable state
    let ra = new ReactiveProperty<float>()
    let dec = new ReactiveProperty<float>()
    let isTracking = new ReactiveProperty<bool>()
    let isSlewing = new ReactiveProperty<bool>()
    let polarError = new ReactiveProperty<float>()
    
    // Subscribe to simulation state changes
    let subscription = 
        simulationEngine.MountStateChanged
            .ObserveOnDispatcher()
            .Subscribe(fun state ->
                ra.Value <- state.RA
                dec.Value <- state.Dec
                isTracking.Value <- state.TrackingRate > 0.0
                isSlewing.Value <- state.IsSlewing
                polarError.Value <- state.PolarAlignmentError)
    
    // Properties
    member this.RA = ra
    member this.Dec = dec
    member this.IsTracking = isTracking
    member this.IsSlewing = isSlewing
    member this.PolarAlignmentError = polarError
    
    // Commands
    member this.NudgeNorthCommand = 
        ReactiveCommand.Create(fun _ -> 
            simulationEngine.PostMessage(Nudge(0.0, 0.01, 1.0)))
    
    member this.NudgeSouthCommand = 
        ReactiveCommand.Create(fun _ -> 
            simulationEngine.PostMessage(Nudge(0.0, -0.01, 1.0)))
    
    member this.NudgeEastCommand = 
        ReactiveCommand.Create(fun _ -> 
            simulationEngine.PostMessage(Nudge(-0.01, 0.0, 1.0)))
    
    member this.NudgeWestCommand = 
        ReactiveCommand.Create(fun _ -> 
            simulationEngine.PostMessage(Nudge(0.01, 0.0, 1.0)))
    
    member this.SetTrackingCommand = 
        ReactiveCommand.Create(fun isOn -> 
            let rate = if isOn then 1.0 else 0.0
            simulationEngine.PostMessage(SetTrackingRate rate))
    
    member this.SetPolarErrorCommand = 
        ReactiveCommand.Create(fun error -> 
            let mount = simulationEngine.CurrentState.Mount
            let newMount = { mount with PolarAlignmentError = error }
            simulationEngine.PostMessage(UpdateMount newMount))
```

## 5. Implementation Strategy

### 5.1 Starfield Implementation

The star field is dynamically generated and expanded as the telescope moves across the virtual sky:

1. **Initial Empty Field**: Start with an empty star field.
2. **On-Demand Generation**: Generate stars in the current field of view when needed.
3. **Persistent Stars**: Once generated, stars are stored in the simulation state.
4. **Coordinate-Based**: Stars have fixed celestial coordinates (RA/Dec).
5. **Magnitude-Limited**: Stars are generated down to a limiting magnitude.

### 5.2 Seeing and Atmospheric Effects

Atmospheric effects are modeled using a multi-layer approach:

1. **Seeing Effects**: Applied as Gaussian PSF with width based on seeing parameter.
2. **Cloud Coverage**: Implemented as transparency factor affecting star visibility.
3. **Transparency**: Affects the limiting magnitude of visible stars.
4. **Layered Implementation**: Different atmospheric layers can move independently.

### 5.3 Mount Behavior Simulation

The mount simulation models realistic telescope behavior:

1. **Sidereal Tracking**: Basic star tracking with adjustable rate.
2. **Polar Alignment Error**: Causes field rotation and Dec drift.
3. **Periodic Error**: Sinusoidal motion in RA due to worm gear.
4. **Slewing**: Progressive movement when slewing to a target.
5. **Guide Commands**: Responds to pulse-guide commands with appropriate motion.

### 5.4 Camera Simulation

The camera simulation produces realistic synthetic images:

1. **Point Spread Function**: Stars rendered as 2D Gaussians.
2. **Shot Noise**: Poisson noise based on star brightness.
3. **Read Noise**: Gaussian noise added to each pixel.
4. **Dark Current**: Accumulating noise over exposure time.
5. **Binning**: Proper summation of adjacent pixels.
6. **Sensor Size**: Configurable dimensions and pixel size.

## 6. Testing Strategy

### 6.1 Unit Testing

Unit tests will cover core simulation components:

- **Star Field Generation**: Test star distribution properties.
- **Image Generation**: Test rendering of known star patterns.
- **Mount Behavior**: Test tracking, slewing, and guiding response.
- **ASCOM Compliance**: Test all ASCOM interface methods.

### 6.2 Integration Testing

Integration tests will verify system-wide behavior:

- **End-to-End Image Generation**: From star field to final image.
- **Guide Response**: Verify mount response to pulse-guide commands.
- **ASCOM Client Connection**: Test with standard ASCOM clients.

### 6.3 Performance Testing

Performance tests will ensure the system meets real-time requirements:

- **Image Generation Speed**: Ensure images are generated within exposure time.
- **Memory Usage**: Monitor memory consumption over extended use.
- **CPU Usage**: Verify CPU usage stays within reasonable limits.

## 7. Future Expansion

### 7.1 Planned Features

- **Main Imaging Camera**: Add a second camera for main imaging.
- **Star Catalog Integration**: Replace synthetic stars with real star catalog data.
- **Filter Wheel**: Add virtual filter wheel simulation.
- **Focuser**: Add virtual focuser with temperature effects.
- **Weather Simulation**: More detailed weather patterns affecting seeing.
- **Guider Calibration**: Simulate guider calibration process.
- **Realistic Autoguiding**: Simulate how real autoguiders would respond.

### 7.2 Extensibility Points

The architecture includes the following extension points:

- **Plugin System**: For adding new effects or equipment models.
- **Custom Star Field Providers**: To replace the synthetic generator.
- **Alternative Mount Models**: To simulate different telescope types.
- **Custom Atmospheric Models**: For more complex atmospheric effects.

## 8. Conclusion

This architectural design provides a solid foundation for the Astro Equipment Simulator. The functional, reactive approach ensures clean separation of concerns and maintainability, while the detailed simulation models provide realistic behavior for testing and development of astrophotography software.

The message-based architecture and state management approach allow for complex interactions between components while maintaining immutability and predictability in the core simulation logic.