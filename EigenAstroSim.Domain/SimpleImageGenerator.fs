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

