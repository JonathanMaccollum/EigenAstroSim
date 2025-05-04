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



/// Functions for projecting stars onto the sensor plane
module StarProjection =
    /// Convert degrees to radians
    let private toRadians degrees = degrees * Math.PI / 180.0
    
    /// Convert radians to degrees
    let private toDegrees radians = radians * 180.0 / Math.PI
    
    /// Calculate the plate scale in arcseconds per pixel
    let calculatePlateScale (focalLength: float) (pixelSize: float) =
        // Standard formula: 206.265 * (pixel size in μm) / (focal length in mm)
        206.265 * pixelSize / focalLength
    
    /// Project a star from celestial coordinates (RA/Dec) to pixel coordinates
    let projectStar 
        (star: Star) 
        (mountState: MountState) 
        (cameraState: CameraState) : float * float =
        
        // Calculate angular distance between star and mount pointing
        // RA is in degrees, and we need to account for the cos(Dec) factor
        let deltaRA = (star.RA - mountState.RA) * Math.Cos(toRadians mountState.Dec)
        let deltaDec = star.Dec - mountState.Dec
        
        // Convert to arcseconds
        let deltaRAArcsec = deltaRA * 3600.0
        let deltaDecArcsec = deltaDec * 3600.0
        
        // Calculate plate scale (arcseconds per pixel)
        let plateScale = calculatePlateScale mountState.FocalLength cameraState.PixelSize
        
        // Convert to pixels (negative for RA because RA increases eastward)
        let x = (-deltaRAArcsec / plateScale) + float cameraState.Width / 2.0
        let y = (deltaDecArcsec / plateScale) + float cameraState.Height / 2.0
        
        (x, y)
    
    /// Determine if a star is visible on the sensor
    let isStarVisible 
        (star: Star) 
        (mountState: MountState) 
        (cameraState: CameraState) : bool =
        
        let (x, y) = projectStar star mountState cameraState
        
        // Check if coordinates are within the sensor bounds
        // Add a margin for stars that are partially visible
        let margin = 20.0  // pixels
        x >= -margin && 
        x < float cameraState.Width + margin && 
        y >= -margin && 
        y < float cameraState.Height + margin
    
    /// Get all stars that are visible on the sensor
    let getVisibleStars 
        (starField: StarFieldState) 
        (mountState: MountState) 
        (cameraState: CameraState) : Star seq =
        
        starField.Stars
        |> Map.toSeq
        |> Seq.map snd
        |> Seq.filter (fun star -> isStarVisible star mountState cameraState)

/// Telescope optical parameters
type TelescopeOptics = {
    /// Aperture diameter in mm
    Aperture: float
    
    /// Central obstruction diameter in mm (0 for refractors)
    Obstruction: float
    
    /// Optical transmission (0.0-1.0)
    Transmission: float
}

/// Functions for calculating photon flux based on star properties and optical system
module PhotonFlux =
    /// Convert B-V color index to effective wavelength in nanometers
    let colorIndexToWavelength (colorIndex: float) =
        // Approximate conversion based on stellar spectral types
        // B-V = -0.3 (hot blue stars) -> ~450nm
        // B-V = 0.6 (Sun-like stars) -> ~550nm  
        // B-V = 1.5 (cool red stars) -> ~650nm
        450.0 + (colorIndex + 0.3) * 100.0
        |> min 700.0
        |> max 400.0
    
    /// Calculate the zero-point flux in photons/s/m²
    /// This is the flux for a 0-magnitude star
    let zeroPointFlux (wavelength: float) =
        // Standard value for V-band (550nm) is approximately 1e10 photons/s/m²
        // We adjust slightly based on wavelength
        let baseFlux = 1.0e10
        match wavelength with
        | w when w < 500.0 -> baseFlux * 0.8  // Blue stars have fewer photons per unit energy
        | w when w > 600.0 -> baseFlux * 1.2  // Red stars have more photons per unit energy
        | _ -> baseFlux
    
    /// Calculate effective aperture area considering obstruction
    let apertureArea (optics: TelescopeOptics) =
        // Area = π(R² - r²) where R is aperture radius and r is obstruction radius
        let apertureRadius = optics.Aperture / 2.0
        let obstructionRadius = optics.Obstruction / 2.0
        
        Math.PI * (apertureRadius * apertureRadius - obstructionRadius * obstructionRadius) / 1000000.0 // Convert to m²
    
    /// Calculate photon flux for a given star
    let calculatePhotonFlux 
        (star: Star) 
        (optics: TelescopeOptics)
        (exposureTime: float) : float =
        
        // Get effective wavelength from star color
        let wavelength = colorIndexToWavelength star.Color
        
        // Get zero point flux for this wavelength
        let zp = zeroPointFlux wavelength
        
        // Calculate relative flux using Pogson's equation
        // Each 5 magnitudes = factor of 100 in brightness
        let relativeFlux = Math.Pow(10.0, -0.4 * star.Magnitude)
        
        // Get effective aperture area in square meters
        let area = apertureArea optics
        
        // Calculate total photons collected
        let photons = zp * relativeFlux * area * optics.Transmission * exposureTime
        
        photons
    
    /// Create default telescope optics for typical amateur setups
    let createDefaultOptics (mountState: MountState) =
        // Estimate aperture from focal length using typical f-ratios
        let focalLength = mountState.FocalLength
        
        // For a typical f/7 system:
        let aperture = focalLength / 7.0
        
        // Typical central obstruction is about 33% of aperture diameter for SCTs
        let obstruction = aperture * 0.33
        
        // Typical transmission including mirrors and corrector plates
        let transmission = 0.85
        
        {
            Aperture = aperture
            Obstruction = obstruction
            Transmission = transmission
        }
    
    /// Calculate a range of magnitudes that should be simulated
    /// Returns (brightest, faintest) magnitudes to consider
    let calculateMagnitudeRange (optics: TelescopeOptics) (exposureTime: float) =
        // Calculate limiting magnitude based on aperture (rough approximation)
        // Visual limiting magnitude ≈ 7.5 + 5*log10(aperture in cm)
        let apertureCm = optics.Aperture / 10.0
        let visualLimit = 7.5 + 5.0 * Math.Log10(apertureCm)
        
        // Adjust for exposure time (each doubling of exposure adds ~0.75 mag)
        let exposureGain = 0.75 * Math.Log(exposureTime / 0.1) / Math.Log(2.0)
        
        // Brightest stars to consider (to avoid overflow)
        let brightestMag = -1.0
        
        // Faintest stars to consider (based on detection limits)
        let faintestMag = visualLimit + exposureGain
        
        (brightestMag, faintestMag)

/// Functions for buffer management and PSF generation
module BufferManagement =
    /// Create a new accumulation buffer for photons
    let createBuffer (width: int) (height: int) : float[,] =
        Array2D.zeroCreate width height
    
    /// Generate a Gaussian PSF (Point Spread Function) for a star
    let generateGaussianPSF (fwhmPixels: float) (size: int) : float[,] =
        // Convert FWHM to sigma: sigma = FWHM / (2.35482)
        let sigma = fwhmPixels / 2.35482
        
        // Create the PSF array
        let psf = Array2D.zeroCreate size size
        let center = float size / 2.0
        
        // Fill with Gaussian values
        for x = 0 to size - 1 do
            for y = 0 to size - 1 do
                let dx = float x - center
                let dy = float y - center
                let r2 = dx * dx + dy * dy
                let value = Math.Exp(-r2 / (2.0 * sigma * sigma))
                psf.[x, y] <- value
        
        // Normalize PSF so it sums to 1.0
        let mutable sum = 0.0
        for x = 0 to size - 1 do
            for y = 0 to size - 1 do
                sum <- sum + psf.[x, y]
        
        if sum > 0.0 then
            for x = 0 to size - 1 do
                for y = 0 to size - 1 do
                    psf.[x, y] <- psf.[x, y] / sum
        
        psf
    
    /// Calculate the PSF size needed for a given FWHM
    let calculatePSFSize (fwhmPixels: float) =
        // Make the PSF size at least 5 times the FWHM to capture >99% of the energy
        // Ensure it's odd for a centered peak
        let size = int (Math.Ceiling(fwhmPixels * 5.0))
        if size % 2 = 0 then size + 1 else size
    
    /// Accumulate photons from a star onto the buffer
    let accumulatePhotons 
        (buffer: float[,]) 
        (pixelX: float) 
        (pixelY: float) 
        (photons: float) 
        (psf: float[,]) : unit =
        
        // Get dimensions
        let psfSize = Array2D.length1 psf
        let bufferWidth = Array2D.length1 buffer
        let bufferHeight = Array2D.length2 buffer
        
        // PSF is centered on the star position
        let psfRadius = psfSize / 2
        
        // Calculate the region where the PSF overlaps the buffer
        let startX = Math.Max(0, int (Math.Floor(pixelX)) - psfRadius)
        let startY = Math.Max(0, int (Math.Floor(pixelY)) - psfRadius)
        let endX = Math.Min(bufferWidth - 1, int (Math.Ceiling(pixelX)) + psfRadius)
        let endY = Math.Min(bufferHeight - 1, int (Math.Ceiling(pixelY)) + psfRadius)
        
        // Apply the PSF to the buffer
        for bufferX = startX to endX do
            for bufferY = startY to endY do
                // Calculate corresponding position in the PSF
                let psfX = bufferX - (int (Math.Floor(pixelX)) - psfRadius)
                let psfY = bufferY - (int (Math.Floor(pixelY)) - psfRadius)
                
                // Check bounds to avoid array out of bounds
                if psfX >= 0 && psfX < psfSize && psfY >= 0 && psfY < psfSize then
                    // Get the PSF value and add weighted photons to the buffer
                    let psfValue = psf.[psfX, psfY]
                    buffer.[bufferX, bufferY] <- buffer.[bufferX, bufferY] + photons * psfValue
    
    /// Convert a 2D buffer to a 1D array in row-major order
    let bufferToArray (buffer: float[,]) : float[] =
        let width = Array2D.length1 buffer
        let height = Array2D.length2 buffer
        let result = Array.zeroCreate (width * height)
        
        for y = 0 to height - 1 do
            for x = 0 to width - 1 do
                result.[y * width + x] <- buffer.[x, y]
        
        result
        
    /// Combine multiple subframe buffers into a single buffer
    let combineBuffers (buffers: float[,][]) : float[,] =
        if Array.isEmpty buffers then
            Array2D.create 0 0 0.0
        else
            let width = Array2D.length1 buffers.[0]
            let height = Array2D.length2 buffers.[0]
            let result = Array2D.zeroCreate width height
            
            // Add all buffers to the result
            for buffer in buffers do
                for x = 0 to width - 1 do
                    for y = 0 to height - 1 do
                        result.[x, y] <- result.[x, y] + buffer.[x, y]
            
            result

/// Core module for subframe processing using physics-based simulation
module EnhancedSubframeProcessor =
    /// Process a single subframe for the given time slice
    let processSubframe 
        (state: SimulationState) 
        (timeIndex: int)
        (subframeDuration: float) 
        (subframeTimestamp: float) : Subframe =
        
        // Get the simulation parameters
        let optics = PhotonFlux.createDefaultOptics state.Mount
        
        // Create accumulation buffer
        let width = state.Camera.Width
        let height = state.Camera.Height
        let buffer = BufferManagement.createBuffer width height
        
        // Calculate the plate scale (arcseconds per pixel)
        let plateScale = StarProjection.calculatePlateScale state.Mount.FocalLength state.Camera.PixelSize
        
        // Calculate the PSF FWHM in pixels
        let fwhmArcsec = state.Atmosphere.SeeingCondition
        let fwhmPixels = fwhmArcsec / plateScale
        
        // Create the PSF
        let psfSize = BufferManagement.calculatePSFSize fwhmPixels
        let psf = BufferManagement.generateGaussianPSF fwhmPixels psfSize
        
        // Get visible stars for this subframe
        let visibleStars = 
            StarProjection.getVisibleStars 
                state.StarField 
                state.Mount 
                state.Camera
                
        // Calculate atmospheric jitter for this subframe
        // This will be more sophisticated in future implementations
        let simplifiedJitter = 
            let random = Random(int (subframeTimestamp * 1000.0))
            let jitterScale = state.Atmosphere.SeeingCondition / 5.0  // Scale jitter with seeing
            (random.NextDouble() * 2.0 - 1.0) * jitterScale,
            (random.NextDouble() * 2.0 - 1.0) * jitterScale
        
        // Process each visible star
        for star in visibleStars do
            // Project star to pixel coordinates
            let (baseX, baseY) = StarProjection.projectStar star state.Mount state.Camera
            
            // Apply jitter to star position
            let jitterX, jitterY = simplifiedJitter
            let pixelX = baseX + jitterX / plateScale
            let pixelY = baseY + jitterY / plateScale
            
            // Calculate photon flux for this star and subframe duration
            let photons = 
                PhotonFlux.calculatePhotonFlux 
                    star 
                    optics 
                    subframeDuration
            
            // Accumulate photons onto the buffer
            BufferManagement.accumulatePhotons 
                buffer 
                pixelX 
                pixelY 
                photons 
                psf
        
        // Create and return the subframe
        {
            TimeIndex = timeIndex
            Duration = subframeDuration
            Buffer = buffer
            MountState = state.Mount
            AtmosphereState = state.Atmosphere
            Jitter = simplifiedJitter
            Timestamp = subframeTimestamp
        }
    
    /// Generate a series of subframes for the entire exposure
    let generateSubframes (state: SimulationState) (parameters: SimulationParameters) : Subframe[] =
        // Calculate how many subframes we need for the requested exposure time
        let subframeCount = int (Math.Ceiling(state.Camera.ExposureTime / parameters.SubframeDuration))
        
        // Create an array to hold the subframes
        let subframes = Array.zeroCreate subframeCount
        
        // Generate each subframe
        for i = 0 to subframeCount - 1 do
            let timestamp = float i * parameters.SubframeDuration
            subframes.[i] <- processSubframe state i parameters.SubframeDuration timestamp
        
        subframes
    
    /// Process all subframes and combine them into a final image buffer
    let processFullExposure (state: SimulationState) (parameters: SimulationParameters) : float[,] =
        // Generate all subframes
        let subframes = generateSubframes state parameters
        
        // Extract the buffers from subframes
        let buffers = subframes |> Array.map (fun sf -> sf.Buffer)
        
        // Combine all buffers
        BufferManagement.combineBuffers buffers
    
    /// Convert the combined buffer to the final image format
    let bufferToImage (buffer: float[,]) : float[] =
        BufferManagement.bufferToArray buffer

/// Placeholder implementation of the high-fidelity virtual astrophotography sensor
/// Initially, this will delegate to the simple implementation until we implement the full system
/// Enhanced implementation of the high-fidelity virtual astrophotography sensor
type VirtualAstrophotographySensor() =
    // Default parameters for simulation
    let parameters = Defaults.simulationParameters
    
    /// Create an image using the physics-based simulation approach
    let generateWithPhysicsModel (state: SimulationState) =
        // Process the exposure using our subframe architecture
        let combinedBuffer = 
            EnhancedSubframeProcessor.processFullExposure 
                state 
                parameters
        
        // Convert to 1D array in the expected format
        EnhancedSubframeProcessor.bufferToImage combinedBuffer
    
    interface IImageGenerator with
        member this.GenerateImage(state: SimulationState) =
            // Use our physics-based model implementation
            generateWithPhysicsModel state
            
        member this.ImplementationName = "Virtual Astrophotography Sensor"
        
        member this.SupportsRealTimePreview = false
        
        member this.Capabilities = {
            SupportsRealTimePreview = false
            UsesSubframes = true
            SupportsAdvancedPhysics = true
            FidelityLevel = 9
        }

/// Factory for creating image generators
module ImageGeneratorFactory =
    /// Create an image generator of the specified type
    let create (useHighFidelity: bool) : IImageGenerator =
        if useHighFidelity then
            VirtualAstrophotographySensor() :> IImageGenerator
        else
            SimpleImageGenerator() :> IImageGenerator

