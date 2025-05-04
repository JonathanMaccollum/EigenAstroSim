# EigenAstroSim Simulation Engine Documentation

## Overview

The EigenAstroSim Simulation Engine provides a comprehensive, real-time astronomical imaging simulation environment. It models telescope mounts, cameras, rotators, and atmospheric conditions to create realistic virtual astrophotography experiences. The engine is designed with a message-based architecture that enables asynchronous operation and clear separation of concerns.

## Core Architecture

### Component-Based Design

The simulation engine models distinct astronomical equipment components:

- **Mount**: Telescope mount that controls pointing and tracking
- **Camera**: Imaging sensor that captures exposures
- **Rotator**: Field rotator that changes image orientation
- **Atmosphere**: Environmental conditions affecting image quality

Each component has its own state, behavior, and message types, allowing the system to be extended modularly.

### Message-Driven Communication

The simulation engine uses a mailbox processor pattern to handle commands asynchronously. Messages are organized by component type for clarity and maintainability:

```
Client Code → Component Messages → Mailbox Processor → Component Handlers → State Update → Event Notifications
```

This architecture allows multiple client components to send commands to the simulation without blocking UI threads.

## Message Types

The engine defines component-specific message types that encapsulate commands for each equipment type:

### Mount Messages (`MountMsg`)

```fsharp
type MountMsg =
    | UpdateMount of MountState
    | UpdateDetailedMount of DetailedMountState
    | SlewTo of ra:float * dec:float
    | Nudge of raRate:float * decRate:float * duration:float
    | SetTrackingRate of rate:float
    | SetPolarAlignmentError of degrees:float
    | SetPeriodicError of amplitude:float * period:float
    | SimulateCableSnag of raAmount:float * decAmount:float
```

### Camera Messages (`CameraMsg`)

```fsharp
type CameraMsg =
    | UpdateCamera of CameraState
    | StartExposure of duration:float
    | StopExposure
    | SetContinuousCapture of isEnabled:bool
```

### Rotator Messages (`RotatorMsg`)

```fsharp
type RotatorMsg =
    | UpdateRotator of RotatorState
    | SetRotatorPosition of angle:float
```

### Atmosphere Messages (`AtmosphereMsg`)

```fsharp
type AtmosphereMsg =
    | UpdateAtmosphere of AtmosphericState
    | SetSeeingCondition of arcseconds:float
    | SetCloudCoverage of percentage:float
    | SetTransparency of percentage:float
```

### Simulation Messages (`SimulationMsg`)

```fsharp
type SimulationMsg =
    | AdvanceTime of seconds:float
    | GenerateSatelliteTrail
```

These component-specific messages are wrapped in a unified `Msg` type:

```fsharp
type Msg =
    | MountCommand of MountMsg
    | CameraCommand of CameraMsg
    | RotatorCommand of RotatorMsg
    | AtmosphereCommand of AtmosphereMsg
    | SimulationCommand of SimulationMsg
```

## Using the Simulation Engine

### Instantiation

```fsharp
// Create a new simulation engine instance
let simulationEngine = SimulationEngine()
```

### Sending Commands

The engine provides two ways to send commands:

#### 1. Using Extension Methods (Recommended)

```fsharp
// Mount operations
simulationEngine.SlewTo(6.75, 45.0)  // Slew to RA 6h45m, Dec +45°
simulationEngine.SetTrackingRate(1.0)  // Set to sidereal rate
simulationEngine.SetPolarAlignmentError(1.5)  // Set 1.5° polar alignment error

// Camera operations
simulationEngine.StartExposure(30.0)  // Start a 30-second exposure
simulationEngine.StopExposure()  // Abort current exposure
simulationEngine.SetContinuousCapture(true)  // Enable continuous capture mode

// Rotator operations
simulationEngine.SetRotatorPosition(90.0)  // Rotate to 90 degrees

// Atmosphere operations
simulationEngine.SetSeeingCondition(2.5)  // Set seeing to 2.5 arcseconds
simulationEngine.SetCloudCoverage(0.3)  // Set 30% cloud coverage

// Simulation operations
simulationEngine.AdvanceTime(5.0)  // Advance simulation time by 5 seconds
simulationEngine.GenerateSatelliteTrail()  // Generate a satellite trail in the next image
```

#### 2. Using Explicit Message Constructors

```fsharp
// Mount operations
simulationEngine.PostMessage(MountCommand(SlewTo(6.75, 45.0)))
simulationEngine.PostMessage(MountCommand(SetTrackingRate(1.0)))

// Camera operations
simulationEngine.PostMessage(CameraCommand(StartExposure(30.0)))
```

### Observing State Changes

The engine provides Observable streams for each component's state changes:

```fsharp
// Subscribe to camera state changes
simulationEngine.CameraStateChanged
    .Subscribe(fun cameraState -> 
        // Handle camera state update
        printfn "Camera is now: %s" (if cameraState.IsExposing then "Exposing" else "Idle")
    )
    |> disposables.Add

// Subscribe to mount state changes
simulationEngine.MountStateChanged
    .Subscribe(fun mountState -> 
        // Handle mount state update
        printfn "Mount position: RA %f, Dec %f" mountState.RA mountState.Dec
    )
    |> disposables.Add

// Subscribe to image generation events
simulationEngine.ImageGenerated
    .Subscribe(fun imageData -> 
        // Process the generated image
        displayImage imageData
    )
    |> disposables.Add

// Subscribe to starfield updates
simulationEngine.StarFieldChanged
    .Subscribe(fun starField -> 
        // Update star catalog display
        updateStarDisplay starField
    )
    |> disposables.Add
```

### Reactive Programming with Observables

The engine is designed to work well with reactive extensions. Here's how to connect UI controls to the simulation engine:

```fsharp
// Connect a slider for seeing conditions to the simulation
seeingConditionSlider
    .AsObservable()
    .Skip(1)  // Skip initial value
    .DistinctUntilChanged()
    .Subscribe(fun value -> 
        simulationEngine.SetSeeingCondition(value)
    )
    |> cleanup.Add

// Connect exposure duration numeric control
exposureDurationControl
    .AsObservable()
    .Skip(1)
    .DistinctUntilChanged()
    .Subscribe(fun duration -> 
        // Update the exposure duration for the next capture
        let currentCamera = simulationEngine.CurrentState.Camera
        simulationEngine.UpdateCamera({ currentCamera with ExposureTime = duration })
    )
    |> cleanup.Add
```

## Implementation Details

### State Management

The engine maintains an internal state representing all components:

```fsharp
type SimulationState = {
    StarField: StarFieldState
    Mount: MountState
    Camera: CameraState
    Rotator: RotatorState
    Atmosphere: AtmosphericState
    TimeScale: float
    CurrentTime: DateTime
    HasSatelliteTrail: bool
}
```

Each component handler function updates only its relevant portion of the state and triggers appropriate notification events.

### Event Notifications

The engine provides observables for each component state change:

```fsharp
member _.MountStateChanged = mountStateChanged.AsObservable()
member _.DetailedMountStateChanged = detailedMountStateChanged.AsObservable()
member _.CameraStateChanged = cameraStateChanged.AsObservable()
member _.RotatorStateChanged = rotatorStateChanged.AsObservable()
member _.AtmosphereStateChanged = atmosphereStateChanged.AsObservable()
member _.ImageGenerated = imageGenerated.AsObservable()
member _.StarFieldChanged = starFieldChanged.AsObservable()
```

### Mailbox Processor Pattern

The engine uses F#'s `MailboxProcessor` to handle messages asynchronously:

```fsharp
let mailbox = MailboxProcessor.Start(fun inbox -> 
    let rec loop() = async {
        let! msg = inbox.Receive()
        processMessage (Option.get mailboxRef) msg
        return! loop()
    }
    loop())
```

Messages are routed to the appropriate component handler based on message type:

```fsharp
let processMessage (mb: MailboxProcessor<Msg>) msg =
    logger.Infof "Processing message: %A" msg
    match msg with
    | MountCommand mountMsg -> 
        processMountMessage mb mountMsg
    | CameraCommand cameraMsg -> 
        processCameraMessage mb cameraMsg
    // ... other component handlers
```

### Component Message Handlers

Each component has its own message handler function that encapsulates all logic for that component:

```fsharp
let processMountMessage (mb: MailboxProcessor<Msg>) (msg: MountMsg) =
    match msg with
    | UpdateMount newMountState ->
        // Update mount state
    | SlewTo(targetRa, targetDec) ->
        // Handle slew operation
    // ... other mount message handlers
```

## Extending the Engine

### Adding a New Component Type

To add a new equipment component (e.g., a filter wheel):

1. Define a state type for the component:

```fsharp
type FilterWheelState = {
    CurrentPosition: int
    FilterNames: string[]
    IsMoving: bool
}
```

2. Create a message type for the component:

```fsharp
type FilterWheelMsg =
    | UpdateFilterWheel of FilterWheelState
    | MoveToFilter of position:int
    | SetFilterNames of names:string[]
```

3. Add the component to the unified message type:

```fsharp
type Msg =
    | MountCommand of MountMsg
    | CameraCommand of CameraMsg
    // ... existing messages
    | FilterWheelCommand of FilterWheelMsg
```

4. Update the simulation state to include the new component:

```fsharp
type SimulationState = {
    // ... existing state
    FilterWheel: FilterWheelState
}
```

5. Create a message handler for the component:

```fsharp
let processFilterWheelMessage (msg: FilterWheelMsg) =
    match msg with
    | UpdateFilterWheel newState ->
        state <- { state with FilterWheel = newState }
        filterWheelStateChanged.OnNext(newState)
    | MoveToFilter position ->
        // Implementation
    | SetFilterNames names ->
        // Implementation
```

6. Update the main message processor:

```fsharp
let processMessage (mb: MailboxProcessor<Msg>) msg =
    match msg with
    // ... existing handlers
    | FilterWheelCommand filterWheelMsg -> 
        processFilterWheelMessage filterWheelMsg
```

7. Add an observable for the component's state changes:

```fsharp
let filterWheelStateChanged = new Subject<FilterWheelState>()
member _.FilterWheelStateChanged = filterWheelStateChanged.AsObservable()
```

8. Add extension methods for the component:

```fsharp
type SimulationEngine with
    member this.UpdateFilterWheel(filterWheelState) = 
        this.PostMessage(FilterWheelCommand(UpdateFilterWheel filterWheelState))
    member this.MoveToFilter(position) = 
        this.PostMessage(FilterWheelCommand(MoveToFilter position))
    member this.SetFilterNames(names) = 
        this.PostMessage(FilterWheelCommand(SetFilterNames names))
```

### Adding New Functionality to Existing Components

To add a new feature to an existing component (e.g., dithering for the mount):

1. Add a new message type to the appropriate component:

```fsharp
type MountMsg =
    | UpdateMount of MountState
    // ... existing messages
    | StartDithering of pattern:DitheringPattern * amplitude:float
    | StopDithering
```

2. Update the component's message handler:

```fsharp
let processMountMessage (mb: MailboxProcessor<Msg>) (msg: MountMsg) =
    match msg with
    // ... existing handlers
    | StartDithering(pattern, amplitude) ->
        // Implement dithering
    | StopDithering ->
        // Stop dithering
```

3. Add extension methods for the new functionality:

```fsharp
type SimulationEngine with
    // ... existing extensions
    member this.StartDithering(pattern, amplitude) = 
        this.PostMessage(MountCommand(StartDithering(pattern, amplitude)))
    member this.StopDithering() = 
        this.PostMessage(MountCommand(StopDithering))
```

## Best Practices

### Message Handling

- Keep message handlers focused on a single component
- Ensure handlers only update their specific component state
- Use helper functions for complex operations to keep handlers readable

### Observable Usage

- Always dispose of subscriptions to prevent memory leaks
- Use `DistinctUntilChanged()` to prevent unnecessary updates
- Consider using `throttle` or `debounce` for UI inputs that change rapidly

### Error Handling

- Add error handling in message processors to prevent crashing:

```fsharp
let processMessage (mb: MailboxProcessor<Msg>) msg =
    try
        match msg with
        // ... message handling
    with
    | ex ->
        logger.Errorf "Error processing message %A: %s" msg ex.Message
```

### State Initialization

- Initialize components with reasonable defaults
- Consider exposing methods to reset components to known states

### Logging

- Log important state transitions
- Include detailed information for debugging
- Use appropriate log levels (Info, Debug, Error)

## Common Patterns

### Delayed Operations

For operations that need to complete after a delay:

```fsharp
let timer = new System.Timers.Timer(duration * 1000.0)
timer.AutoReset <- false
timer.Elapsed.Add(fun _ -> 
    // Perform delayed operation
    mb.Post(CameraCommand(UpdateCamera updatedCamera))
    timer.Dispose()
)
timer.Start()
```

### Component Interactions

When one component needs to respond to another component's state changes:

```fsharp
// Example: Stop tracking if the temperature gets too high
simulationEngine.TemperatureStateChanged
    .Where(fun temp -> temp > 40.0)
    .Subscribe(fun _ -> 
        simulationEngine.SetTrackingRate(0.0)
    )
    |> disposables.Add
```

### Sequential Operations

For operations that need to happen in sequence:

```fsharp
// Example: Slew, then start an exposure when complete
simulationEngine.MountStateChanged
    .Where(fun mount -> not mount.IsSlewing)
    .Take(1)
    .Subscribe(fun _ -> 
        simulationEngine.StartExposure(10.0)
    )
    |> disposables.Add

simulationEngine.SlewTo(10.5, 41.2)
```

## Performance Considerations

- Consider running CPU-intensive simulations on background threads
- Use reactive throttling for high-frequency updates
- Minimize allocations in hot paths
- Cache complex calculations when possible

## Conclusion

The EigenAstroSim Simulation Engine provides a robust framework for astronomical simulations. Its component-based, message-driven architecture enables clear separation of concerns while maintaining extensibility. The observable-based event system allows for responsive UI updates and complex interaction patterns.

By following the patterns and practices outlined in this document, developers can effectively use, maintain, and extend the simulation engine to support new features and equipment types.