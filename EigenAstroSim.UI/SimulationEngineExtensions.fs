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
        currentImageGenerator <- ImageGeneratorFactory.create useHighFidelity
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

/// Example code to patch the SimulationEngine class
module SimulationEnginePatch =
    /// Code to add to SimulationEngine.fs
    let patchExample = """
    // Add to the top of the file
    open EigenAstroSim.Domain.VirtualSensorSimulation
    
    // Add to SimulationEngine class
    let mutable currentImageGenerator : IImageGenerator = SimpleImageGenerator() :> IImageGenerator
    
    member _.CurrentImageGenerator = currentImageGenerator
    
    member _.SetImageGenerationMode(useHighFidelity: bool) =
        currentImageGenerator <- 
            if useHighFidelity then
                VirtualAstrophotographySensor() :> IImageGenerator
            else
                SimpleImageGenerator() :> IImageGenerator
    
    // Modify the exposure timer to use the current generator
    // Update in processCameraMessage:
    timer.Elapsed.Add(fun _ ->
        // Generate image using current generator instead of directly calling ImageGeneration
        let image = currentImageGenerator.GenerateImage state
        imageGenerated.OnNext(image)
        
        // Rest of the code remains the same
        // ...
    )
    """
        
/// Example usage of the simulation engine with high-fidelity rendering
module SimulationEngineExample =
    open System.Threading
    
    /// Demonstrate using the engine with both rendering modes (using extensions)
    let runSimulationExample() =
        // Create the engine
        let engine = SimulationEngine()
        
        // Create the extensions
        let extensions = SimulationEngineExtensions(engine)
        
        // Set up a basic scene for testing
        engine.SlewTo(120.0, 40.0) // Point at a specific star field
        engine.SetSeeingCondition(2.0) // 2" seeing conditions
        engine.SetCloudCoverage(0.0) // Clear skies
        
        // Take an image with the simple generator
        printfn "Taking image with simple generator..."
        extensions.SetImageGenerationMode(false)
        let simpleImage = extensions.GenerateImage()
        printfn "Simple image generated with size: %d" simpleImage.Length
        
        // Take an image with the high-fidelity generator
        printfn "Taking image with high-fidelity generator..."
        extensions.SetImageGenerationMode(true)
        let highFidelityImage = extensions.GenerateImage()
        printfn "High-fidelity image generated with size: %d" highFidelityImage.Length
        
        // Demonstrate starting an exposure with specific mode
        printfn "Starting 5-second exposure with high-fidelity mode..."
        extensions.StartExposureWithMode(5.0, true)
        
        // Wait for the exposure to complete
        Thread.Sleep(6000)
        
        printfn "Exposure complete."
        
        // Report on the current generator
        let currentGeneratorName = extensions.CurrentImageGenerator.ImplementationName
        printfn "Current generator: %s" currentGeneratorName
        
        // Return the extensions for further use
        extensions