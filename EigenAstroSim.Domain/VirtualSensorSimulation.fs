namespace EigenAstroSim.Domain.VirtualSensorSimulation

open System
open EigenAstroSim.Domain
open EigenAstroSim.Domain.Types

/// Represents an atmospheric turbulence layer with specific characteristics
type AtmosphericLayer = {
    /// Height of the layer in kilometers
    Height: float
    
    /// Direction of movement in degrees
    Direction: float
    
    /// Speed in arcseconds per second
    Speed: float
    
    /// Characteristic distortion scale in arcseconds
    DistortionScale: float
    
    /// Fraction of total seeing contribution (0-1)
    DistortionContribution: float
    
    /// Temporal correlation scale in seconds
    TimeScale: float
}

/// Represents a single sub-exposure frame (typically 0.1s)
type Subframe = {
    /// Time index within the exposure
    TimeIndex: int
    
    /// Duration of this subframe in seconds
    Duration: float
    
    /// Light accumulation buffer (width x height)
    Buffer: float[,]
    
    /// Mount state at this moment
    MountState: MountState
    
    /// Atmospheric state at this moment
    AtmosphereState: AtmosphericState
    
    /// Jitter applied to this subframe (x, y) in arcseconds
    Jitter: float * float
    
    /// Timestamp within the exposure
    Timestamp: float
}

/// Describes the layers of transformations for the astrophotography simulation
type TransformationLayer =
    /// Optical system effects (diffraction, aberrations)
    | OpticalSystem of aperture: float * focalLength: float * obstruction: float
    
    /// Atmospheric seeing effects (turbulence, scintillation)
    | AtmosphericSeeing of seeingFWHM: float * layers: AtmosphericLayer[]
    
    /// Mount tracking effects (periodic error, drift)
    | MountTracking of periodicError: float * polarError: float
    
    /// Sensor physics (quantum efficiency, noise, etc.)
    | SensorPhysics of quantumEfficiency: float * readNoise: float * darkCurrent: float

/// Parameters for the high-fidelity simulation
type SimulationParameters = {
    /// Subframe duration in seconds (default: 0.1s)
    SubframeDuration: float
    
    /// Whether to use multi-layer atmospheric model
    UseMultiLayerAtmosphere: bool
    
    /// Whether to simulate mount tracking errors
    SimulateTrackingErrors: bool
    
    /// Whether to simulate sensor quantum efficiency and noise
    SimulateFullSensorPhysics: bool
    
    /// Whether to simulate cloud patterns
    SimulateCloudPatterns: bool
}

/// Interface for image generators that allows switching between implementations
type IImageGenerator =
    /// Generate an image based on the simulation state
    abstract member GenerateImage : SimulationState -> float[]
    
    /// Get the name of the implementation
    abstract member ImplementationName : string
    
    /// Returns whether the implementation supports real-time previews during exposure
    abstract member SupportsRealTimePreview : bool
    
    /// Returns information about the implementation's capabilities
    abstract member Capabilities : ImageGeneratorCapabilities

/// Defines capabilities of an image generator implementation
and ImageGeneratorCapabilities = {
    /// Whether the implementation can generate images in real-time
    SupportsRealTimePreview: bool
    
    /// Whether the implementation simulates subframes
    UsesSubframes: bool
    
    /// Whether the implementation supports advanced physics simulation
    SupportsAdvancedPhysics: bool
    
    /// The fidelity level of the simulation (1-10)
    FidelityLevel: int
}

/// Implementation that wraps the current simple image generation system
type SimpleImageGenerator() =
    interface IImageGenerator with
        member this.GenerateImage(state: SimulationState) =
            // Delegate to the existing image generation code
            ImageGeneration.generateImage state
            
        member this.ImplementationName = "Simple Image Generator"
        
        member this.SupportsRealTimePreview = true
        
        member this.Capabilities = {
            SupportsRealTimePreview = true
            UsesSubframes = false
            SupportsAdvancedPhysics = false
            FidelityLevel = 3
        }

/// Placeholder implementation of the high-fidelity virtual astrophotography sensor
/// Initially, this will delegate to the simple implementation until we implement the full system
type VirtualAstrophotographySensor() =
    interface IImageGenerator with
        member this.GenerateImage(state: SimulationState) =
            // For Phase 1, we delegate to the existing implementation
            // In future phases, we'll use the subframe architecture
            ImageGeneration.generateImage state
            
        member this.ImplementationName = "Virtual Astrophotography Sensor"
        
        member this.SupportsRealTimePreview = false
        
        member this.Capabilities = {
            SupportsRealTimePreview = false
            UsesSubframes = true  // Will be true once implemented
            SupportsAdvancedPhysics = true  // Will be true once implemented
            FidelityLevel = 9
        }

/// Core functions for the subframe architecture
module SubframeProcessor =
    /// Generate a series of subframes for the exposure
    let generateSubframes (state: SimulationState) (parameters: SimulationParameters) : Subframe[] =
        // Calculate how many subframes we need for the requested exposure time
        let subframeCount = int (Math.Ceiling(state.Camera.ExposureTime / parameters.SubframeDuration))
        
        // Create an array to hold the subframes
        let subframes = Array.zeroCreate subframeCount
        
        // Initialize each subframe
        for i = 0 to subframeCount - 1 do
            // Create a clean accumulation buffer
            let buffer = Array2D.zeroCreate state.Camera.Width state.Camera.Height
            
            // For the prototype, use the same mount and atmosphere state for all subframes
            // Later we'll simulate evolution over time
            subframes.[i] <- {
                TimeIndex = i
                Duration = parameters.SubframeDuration
                Buffer = buffer
                MountState = state.Mount
                AtmosphereState = state.Atmosphere
                Jitter = (0.0, 0.0)  // No jitter in prototype
                Timestamp = float i * parameters.SubframeDuration
            }
        
        subframes

    /// Process a subframe through the atmospheric seeing layer
    let applyAtmosphericSeeing (subframe: Subframe) : Subframe =
        // In Phase 1, this is just a placeholder
        // Later we'll implement the atmospheric seeing model
        subframe
        
    /// Process a subframe through the mount tracking layer
    let applyMountTracking (subframe: Subframe) : Subframe =
        // In Phase 1, this is just a placeholder
        // Later we'll implement the mount tracking model
        subframe
        
    /// Process a subframe through the sensor physics layer
    let applySensorPhysics (subframe: Subframe) : Subframe =
        // In Phase 1, this is just a placeholder
        // Later we'll implement the sensor physics model
        subframe
        
    /// Process a subframe through all transformation layers
    let processSubframe (subframe: Subframe) (starField: StarFieldState) : Subframe =
        subframe
        |> applyAtmosphericSeeing
        |> applyMountTracking
        |> applySensorPhysics

    /// Combine subframes into a final image
    let combineSubframes (subframes: Subframe[]) : float[] =
        if Array.isEmpty subframes then
            [| |]
        else
            // Extract dimensions from the first subframe
            let width = Array2D.length1 subframes.[0].Buffer
            let height = Array2D.length2 subframes.[0].Buffer
            
            // Create a buffer for the combined image
            let combinedBuffer = Array2D.zeroCreate width height
            
            // Add all subframes to the combined buffer
            for subframe in subframes do
                for x = 0 to width - 1 do
                    for y = 0 to height - 1 do
                        combinedBuffer.[x, y] <- combinedBuffer.[x, y] + subframe.Buffer.[x, y]
            
            // Convert to 1D array in the expected format
            let result = Array.zeroCreate (width * height)
            for y = 0 to height - 1 do
                for x = 0 to width - 1 do
                    result.[y * width + x] <- combinedBuffer.[x, y]
            
            result

/// Factory for creating image generators
module ImageGeneratorFactory =
    /// Create an image generator of the specified type
    let create (useHighFidelity: bool) : IImageGenerator =
        if useHighFidelity then
            VirtualAstrophotographySensor() :> IImageGenerator
        else
            SimpleImageGenerator() :> IImageGenerator

/// Default simulation parameters
module Defaults =
    /// Default parameters for high-fidelity simulation
    let simulationParameters = {
        SubframeDuration = 0.1
        UseMultiLayerAtmosphere = true
        SimulateTrackingErrors = true
        SimulateFullSensorPhysics = true
        SimulateCloudPatterns = true
    }
    
    /// Generate atmospheric layers based on seeing condition
    let generateAtmosphericLayers (seeing: float) =
        let random = System.Random()
        
        [|
            // High-altitude jet stream layer
            {
                Height = 10.0 + random.NextDouble() * 5.0 // 10-15km
                Direction = random.NextDouble() * 360.0 // Random direction
                Speed = 0.5 + random.NextDouble() * 1.5 // 0.5-2.0 arcsec/sec
                DistortionScale = seeing * 0.3 // 30% of seeing budget
                DistortionContribution = 0.3
                TimeScale = 5.0 + random.NextDouble() * 10.0 // 5-15 seconds
            }
            
            // Mid-atmosphere thermal layer
            {
                Height = 5.0 + random.NextDouble() * 5.0 // 5-10km
                Direction = random.NextDouble() * 360.0 // Random direction
                Speed = 0.2 + random.NextDouble() * 0.8 // 0.2-1.0 arcsec/sec
                DistortionScale = seeing * 0.4 // 40% of seeing budget
                DistortionContribution = 0.4
                TimeScale = 3.0 + random.NextDouble() * 7.0 // 3-10 seconds
            }
            
            // Ground-level layer
            {
                Height = random.NextDouble() * 1.0 // 0-1km
                Direction = random.NextDouble() * 360.0 // Random direction
                Speed = 0.1 + random.NextDouble() * 0.3 // 0.1-0.4 arcsec/sec
                DistortionScale = seeing * 0.3 // 30% of seeing budget
                DistortionContribution = 0.3
                TimeScale = 1.0 + random.NextDouble() * 4.0 // 1-5 seconds
            }
        |]