namespace EigenAstroSim.UI

open System
open System.Reactive.Subjects
open EigenAstroSim.Domain
open EigenAstroSim.Domain.Types
open EigenAstroSim.Domain.VirtualSensorSimulation

/// Extension to the SimulationEngine to support high-fidelity rendering
type SimulationEngineExtensions(engine: SimulationEngine) =
    // Current image generator implementation
    let mutable currentImageGenerator : IImageGenerator = SimpleImageGenerator() :> IImageGenerator
    
    // Simulation parameters
    let mutable simulationParameters = {
        SubframeDuration = 0.1
        UseMultiLayerAtmosphere = true
        SimulateTrackingErrors = true
        SimulateFullSensorPhysics = true
        SimulateCloudPatterns = true
    }
    
    // Observable for generator mode changes
    let generatorModeChanged = new Subject<bool>()
    
    // Public properties
    member _.CurrentImageGenerator = currentImageGenerator
    member _.SimulationParameters = simulationParameters
    member _.IsHighFidelityMode = (currentImageGenerator.GetType() = typeof<VirtualAstrophotographySensor>)
    
    // Observable for generator mode changes
    member _.GeneratorModeChanged = generatorModeChanged.AsObservable()
    
    /// Set the image generation mode (simple or high-fidelity)
    member _.SetImageGenerationMode(useHighFidelity: bool) =
        currentImageGenerator <- EnhancedImageGeneratorFactory.create useHighFidelity
        generatorModeChanged.OnNext(useHighFidelity)
    
    /// Update simulation parameters
    member _.UpdateSimulationParameters(parameters: SimulationParameters) =
        simulationParameters <- parameters
        
    /// Generate an image using the current generator
    member _.GenerateImage() =
        currentImageGenerator.GenerateImage engine.CurrentState
        
    /// Custom exposure generation with high-fidelity support
    member _.StartExposureWithMode(duration: float, useHighFidelity: bool) =
        // Set the generator mode
        this.SetImageGenerationMode(useHighFidelity)
        
        // Update camera exposure time
        let currentCamera = engine.CurrentState.Camera
        let newCamera = { currentCamera with ExposureTime = duration }
        engine.UpdateCamera(newCamera)
        
        // Start the exposure
        engine.StartExposure(duration)