namespace EigenAstroSim.Domain.VirtualSensorSimulation

open System
open EigenAstroSim.Domain
open EigenAstroSim.Domain.BufferManagement
open EigenAstroSim.Domain.Types

/// Enhanced implementation of the high-fidelity virtual astrophotography sensor
type EnhancedVirtualAstrophotographySensor() =
    // Cache for the last generated image (for performance)
    let mutable lastState: SimulationState option = None
    let mutable lastImage: float[] option = None
    let mutable lastGenerationTime = DateTime.MinValue
    
    // Cache invalidation time threshold (ms)
    let cacheThreshold = 100
    let bufferManager = new BufferPoolManager()

    // Default parameters for simulation (configurable)
    let mutable parameters = Defaults.simulationParameters
    
    // Progress tracking
    let mutable currentProgress = 0.0
    let mutable processingSubframe = 0
    let mutable totalSubframes = 0
    
    // Create an image using the enhanced physics-based simulation approach
    let generateWithEnhancedPhysicsModel (state: SimulationState) =
        // Start timing the generation
        let startTime = DateTime.Now
        
        // Calculate how many subframes based on exposure time
        totalSubframes <- int (Math.Ceiling(state.Camera.ExposureTime / parameters.SubframeDuration))
        
        // Reset progress tracking
        currentProgress <- 0.0
        processingSubframe <- 0
        
        // Process the exposure using our enhanced subframe architecture
        let combinedBuffer = 
            EnhancedSubframeProcessor2.processFullExposure 
                bufferManager
                state 
                parameters
                
        // Update progress tracking
        currentProgress <- 1.0
        
        // Convert to 1D array in the expected format
        let result = EnhancedSubframeProcessor2.bufferToImage combinedBuffer
        
        // Update cache
        lastState <- Some state
        lastImage <- Some result
        lastGenerationTime <- DateTime.Now
        
        // Return the result
        result
        
    /// Set simulation parameters
    member _.SetParameters(newParameters: SimulationParameters) =
        parameters <- newParameters
        // Invalidate cache when parameters change
        lastState <- None
        lastImage <- None
    
    /// Get current progress (0.0 - 1.0)
    member _.CurrentProgress = currentProgress
    
    /// Get number of subframes being processed
    member _.TotalSubframes = totalSubframes
    
    /// Get current subframe being processed
    member _.ProcessingSubframe = processingSubframe
    
    interface IImageGenerator with
        member this.GenerateImage(state: SimulationState) =
            // Check if we can use cached image
            match lastState, lastImage with
            | Some cachedState, Some cachedImage when 
                // Use cached image if state hasn't changed and cache is fresh
                Object.ReferenceEquals(cachedState, state) && 
                (DateTime.Now - lastGenerationTime).TotalMilliseconds < cacheThreshold ->
                    cachedImage
            | _ ->
                // Generate new image
                generateWithEnhancedPhysicsModel state
            
        member this.ImplementationName = "Enhanced Virtual Astrophotography Sensor"
        
        member this.SupportsRealTimePreview = true
        
        member this.Capabilities = {
            SupportsRealTimePreview = true
            UsesSubframes = true
            SupportsAdvancedPhysics = true
            FidelityLevel = 10
        }
    interface IDisposable with
        member this.Dispose() =
            (bufferManager :> IDisposable).Dispose()

/// Updated factory for creating image generators with the enhanced implementation
module EnhancedImageGeneratorFactory =
    /// Create an image generator of the specified type
    let create (useHighFidelity: bool) : IImageGenerator =
        if useHighFidelity then
            EnhancedVirtualAstrophotographySensor() :> IImageGenerator
        else
            SimpleImageGenerator() :> IImageGenerator