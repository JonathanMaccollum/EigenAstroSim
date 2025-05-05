namespace EigenAstroSim.Domain.VirtualSensorSimulation.Components

open System
open EigenAstroSim.Domain
open EigenAstroSim.Domain.Types

/// Core domain types for the imaging process
module DomainTypes =
    /// Represents a star with pixel coordinates
    type PixelStar = {
        /// Original star data
        Star: Star
        /// X coordinate on the sensor (in pixels)
        X: float
        /// Y coordinate on the sensor (in pixels)
        Y: float
        /// Photon flux (photons per second)
        PhotonFlux: float option
    }
    
    /// Collection of stars visible on the sensor
    type PixelStarField = PixelStar list
    
    /// Point Spread Function representing the shape of a star
    type PSF = {
        /// 2D array of values representing the PSF
        Values: float[,]
        /// Size of the PSF in pixels
        Size: int
        /// The sum of all values (should be 1.0 for energy conservation)
        Sum: float
    }
    
    /// Represents optical telescope parameters
    type OpticalParameters = {
        /// Aperture diameter in mm
        Aperture: float
        /// Central obstruction diameter in mm (0 for refractors)
        Obstruction: float
        /// Focal length in mm
        FocalLength: float
        /// Optical transmission (0.0-1.0)
        Transmission: float
        /// Optical quality (Strehl ratio, 0.0-1.0)
        OpticalQuality: float
        /// F-ratio (focal length / aperture)
        FRatio: float
    }
    
    /// Represents a jitter offset
    type Jitter = {
        /// X component of jitter in arcseconds
        X: float
        /// Y component of jitter in arcseconds
        Y: float
    }
    
    /// Represents a subframe at a specific point in time
    type Subframe = {
        /// Timestamp within the exposure (seconds from start)
        Timestamp: float
        /// Duration of this subframe in seconds
        Duration: float
        /// Evolved mount state for this subframe
        MountState: MountState
        /// Evolved atmospheric state for this subframe
        AtmosphereState: AtmosphericState
        /// Atmospheric jitter for this subframe
        Jitter: Jitter
        /// 2D array of accumulated photon/electron values
        Buffer: float[,]
    }
    
    /// Sensor model for physical characteristics
    type SensorModel = {
        /// Sensor width in pixels
        Width: int
        /// Sensor height in pixels
        Height: int
        /// Pixel size in microns
        PixelSize: float
        /// Quantum efficiency (0.0-1.0)
        QuantumEfficiency: float
        /// Read noise in electrons RMS
        ReadNoise: float
        /// Dark current in electrons/pixel/second at 0°C
        DarkCurrent: float
        /// Gain in electrons/ADU
        Gain: float
        /// Temperature in °C
        Temperature: float
        /// Full well capacity in electrons
        FullWellCapacity: int
        /// Bias level in ADU
        BiasLevel: int
        /// Bit depth of ADC
        BitDepth: int
    }

/// Module for projecting stars from celestial coordinates to pixel positions
module StarProjection =
    open DomainTypes
    
    /// Convert degrees to radians
    let toRadians degrees = degrees * Math.PI / 180.0
    
    /// Convert radians to degrees
    let toDegrees radians = radians * 180.0 / Math.PI
    
    /// Calculate plate scale in arcseconds per pixel
    let calculatePlateScale (focalLength: float) (pixelSize: float) =
        // Standard formula: 206.265 * (pixel size in μm) / (focal length in mm)
        206.265 * pixelSize / focalLength
    
    /// Project a star from celestial coordinates to pixel coordinates
    let projectStar (star: Star) (mountState: MountState) (cameraState: CameraState) =
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
        let x = (float cameraState.Width / 2.0) - deltaRAArcsec / plateScale
        let y = (float cameraState.Height / 2.0) + deltaDecArcsec / plateScale
        
        // Return star with pixel coordinates
        { 
            Star = star
            X = x
            Y = y
            PhotonFlux = None
        }
    
    /// Determine if a star is visible on the sensor
    let isStarVisible (pixelStar: PixelStar) (width: int) (height: int) =
        // Add a margin for stars that are partially visible
        let margin = 20.0
        
        pixelStar.X >= -margin &&
        pixelStar.X < float width + margin &&
        pixelStar.Y >= -margin &&
        pixelStar.Y < float height + margin
    
    /// Get all stars that are visible on the sensor
    let getVisibleStars (starField: StarFieldState) (mountState: MountState) (cameraState: CameraState) =
        starField.Stars
        |> Map.toSeq
        |> Seq.map snd
        |> Seq.map (fun star -> projectStar star mountState cameraState)
        |> Seq.filter (fun pixelStar -> isStarVisible pixelStar cameraState.Width cameraState.Height)
        |> Seq.toList

/// Module for calculating photon flux based on stellar parameters
module PhotonGeneration =
    open DomainTypes
    
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
        let baseFlux = 1.0e10
        match wavelength with
        | w when w < 500.0 -> baseFlux * 0.8  // Blue stars have fewer photons per unit energy
        | w when w > 600.0 -> baseFlux * 1.2  // Red stars have more photons per unit energy
        | _ -> baseFlux
    
    /// Calculate effective aperture area considering obstruction
    let apertureArea (aperture: float) (obstruction: float) =
        // Area = π(R² - r²) where R is aperture radius and r is obstruction radius
        let apertureRadius = aperture / 2.0
        let obstructionRadius = obstruction / 2.0
        
        Math.PI * (apertureRadius * apertureRadius - obstructionRadius * obstructionRadius) / 1000000.0 // Convert to m²
    
    /// Create optical parameters from mount state
    let createOpticalParameters (mountState: MountState) =
        // Estimate aperture from focal length using typical f-ratios
        // For a typical f/7 system:
        let focalLength = mountState.FocalLength
        let aperture = focalLength / 7.0
        let fRatio = focalLength / aperture
        
        // Typical central obstruction is about 33% of aperture diameter for SCTs
        let obstruction = aperture * 0.33
        
        {
            Aperture = aperture
            Obstruction = obstruction
            FocalLength = focalLength
            Transmission = 0.85  // Typical transmission including mirrors and corrector plates
            OpticalQuality = 0.85 // Typical optical quality (Strehl ratio)
            FRatio = fRatio
        }
    
    /// Calculate photon flux for a given star
    let calculatePhotonFlux (pixelStar: PixelStar) (optics: OpticalParameters) (exposureTime: float) =
        // Get effective wavelength from star color
        let wavelength = colorIndexToWavelength pixelStar.Star.Color
        
        // Get zero point flux for this wavelength
        let zp = zeroPointFlux wavelength
        
        // Calculate relative flux using Pogson's equation
        // Each 5 magnitudes = factor of 100 in brightness
        let relativeFlux = Math.Pow(10.0, -0.4 * pixelStar.Star.Magnitude)
        
        // Get effective aperture area in square meters
        let area = apertureArea optics.Aperture optics.Obstruction
        
        // Calculate total photons collected
        let photons = zp * relativeFlux * area * optics.Transmission * exposureTime
        
        // Return star with calculated photon flux
        { pixelStar with PhotonFlux = Some photons }
    
    /// Calculate photon flux for all stars in the field
    let calculatePhotonFluxForField (stars: PixelStar list) (optics: OpticalParameters) (exposureTime: float) =
        stars |> List.map (fun star -> calculatePhotonFlux star optics exposureTime)

/// Module for creating Point Spread Functions (PSFs)
module PsfGeneration =
    open DomainTypes
    open EigenAstroSim.Bessel
    
    /// Generate Airy disk PSF for diffraction-limited optics
    let generateAiryDiskPSF 
            (optics: OpticalParameters) 
            (wavelength: float)  // in nanometers
            (pixelSize: float)   // in microns
            (size: int) =
        
        // Create the PSF array
        let values = Array2D.zeroCreate size size
        let center = float size / 2.0
        
        // Convert wavelength to meters
        let lambda = wavelength * 1.0e-9
        
        // Calculate scaling factor
        // The first zero of the Airy disk occurs at 1.22 * lambda * f-ratio
        let scale = 1.22 * lambda * optics.FRatio
        
        // Convert to pixels
        let scaleFactor = scale * 1.0e6 / pixelSize
        
        // Obstruction ratio (epsilon)
        let epsilon = if optics.Aperture > 0.0 then optics.Obstruction / optics.Aperture else 0.0
        
        // Fill with Airy disk values
        for x = 0 to size - 1 do
            for y = 0 to size - 1 do
                let dx = float x - center
                let dy = float y - center
                let r = Math.Sqrt(dx * dx + dy * dy)
                
                // Handle the center point separately to avoid division by zero
                let value = 
                    if r < 0.001 then
                        1.0 // Center of Airy disk
                    else
                        // Normalized radius
                        let v = Math.PI * r * scaleFactor
                        
                        if epsilon > 0.0 then
                            // With central obstruction
                            let airy = (besselJ1(v) / v - epsilon * epsilon * besselJ1(epsilon * v) / (epsilon * v))
                            airy * airy * 4.0 / ((1.0 - epsilon * epsilon) * (1.0 - epsilon * epsilon))
                        else
                            // Without central obstruction (standard Airy disk)
                            let airy = 2.0 * besselJ1(v) / v
                            airy * airy
                
                values.[x, y] <- value
        
        // Normalize PSF so it sums to 1.0
        let mutable sum = 0.0
        for x = 0 to size - 1 do
            for y = 0 to size - 1 do
                sum <- sum + values.[x, y]
        
        if sum > 0.0 then
            for x = 0 to size - 1 do
                for y = 0 to size - 1 do
                    values.[x, y] <- values.[x, y] / sum
        
        { Values = values; Size = size; Sum = 1.0 }
    
    /// Generate a Gaussian PSF for atmospheric seeing
    let generateGaussianPSF (fwhmPixels: float) (size: int) =
        // Convert FWHM to sigma: sigma = FWHM / (2.35482)
        let sigma = fwhmPixels / 2.35482
        
        // Create the PSF array
        let values = Array2D.zeroCreate size size
        let center = float size / 2.0
        
        // Fill with Gaussian values
        for x = 0 to size - 1 do
            for y = 0 to size - 1 do
                let dx = float x - center
                let dy = float y - center
                let r2 = dx * dx + dy * dy
                let value = Math.Exp(-r2 / (2.0 * sigma * sigma))
                values.[x, y] <- value
        
        // Normalize PSF
        let mutable sum = 0.0
        for x = 0 to size - 1 do
            for y = 0 to size - 1 do
                sum <- sum + values.[x, y]
        
        if sum > 0.0 then
            for x = 0 to size - 1 do
                for y = 0 to size - 1 do
                    values.[x, y] <- values.[x, y] / sum
        
        { Values = values; Size = size; Sum = 1.0 }
    
    /// Calculate the size of the PSF array needed
    let calculatePsfSize (seeingFwhm: float) (plateScale: float) =
        // Convert seeing from arcseconds to pixels
        let fwhmPixels = seeingFwhm / plateScale
        
        // Make the PSF size at least 5 times the FWHM to capture >99% of the energy
        // Ensure it's odd for a centered peak
        let size = int (Math.Ceiling(fwhmPixels * 5.0))
        if size % 2 = 0 then size + 1 else size
    
    /// Convolve two PSFs to get a combined PSF
    let convolvePSFs (psf1: PSF) (psf2: PSF) =
        // Get dimensions
        let size1 = psf1.Size
        let size2 = psf2.Size
        
        // Result size will be size1 + size2 - 1
        let resultSize = size1 + size2 - 1
        let resultValues = Array2D.zeroCreate resultSize resultSize
        
        // Perform the convolution
        for i1 = 0 to size1 - 1 do
            for j1 = 0 to size1 - 1 do
                for i2 = 0 to size2 - 1 do
                    for j2 = 0 to size2 - 1 do
                        let i = i1 + i2
                        let j = j1 + j2
                        resultValues.[i, j] <- resultValues.[i, j] + psf1.Values.[i1, j1] * psf2.Values.[i2, j2]
        
        // Trim to a reasonable size (same as the larger of the inputs)
        let trimSize = Math.Max(size1, size2)
        let startIdx = (resultSize - trimSize) / 2
        let endIdx = startIdx + trimSize - 1
        
        let trimmedValues = Array2D.zeroCreate trimSize trimSize
        for i = 0 to trimSize - 1 do
            for j = 0 to trimSize - 1 do
                trimmedValues.[i, j] <- resultValues.[startIdx + i, startIdx + j]
        
        // Normalize the result
        let mutable sum = 0.0
        for i = 0 to trimSize - 1 do
            for j = 0 to trimSize - 1 do
                sum <- sum + trimmedValues.[i, j]
        
        if sum > 0.0 then
            for i = 0 to trimSize - 1 do
                for j = 0 to trimSize - 1 do
                    trimmedValues.[i, j] <- trimmedValues.[i, j] / sum
        
        { Values = trimmedValues; Size = trimSize; Sum = 1.0 }
    
    /// Generate the combined PSF for a star, considering both diffraction and seeing
    let generateCombinedPSF 
            (optics: OpticalParameters)
            (seeingFwhm: float)
            (plateScale: float)
            (colorIndex: float)
            (pixelSize: float) =
        
        // Calculate the wavelength corresponding to this star's color
        let wavelength = PhotonGeneration.colorIndexToWavelength colorIndex
        
        // Calculate PSF size
        let psfSize = calculatePsfSize seeingFwhm plateScale
        
        // Generate diffraction PSF
        let diffractionPSF = generateAiryDiskPSF optics wavelength pixelSize psfSize
        
        // Generate seeing PSF
        let seeingPSF = generateGaussianPSF (seeingFwhm / plateScale) psfSize
        
        // Combine them
        convolvePSFs diffractionPSF seeingPSF

/// Module for atmospheric evolution modeling
module AtmosphericEvolution =
    open DomainTypes
    
    /// Random number generator - note in a purely functional design, 
    /// we would pass in a random state or use a functional random number generator
    let private random = Random()
    
    /// Generate a random value with Gaussian distribution
    let randomGaussian (mean: float) (sigma: float) =
        // Box-Muller transform
        let u1 = random.NextDouble()
        let u2 = random.NextDouble()
        
        let z0 = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2)
        mean + sigma * z0
    
    /// Atmospheric turbulence layer model
    type AtmosphericLayer = {
        /// Height in kilometers
        Height: float
        /// Direction of flow in degrees
        Direction: float
        /// Speed in arcseconds per second
        Speed: float
        /// Strength of turbulence (fraction of total seeing)
        Strength: float
        /// Characteristic time scale in seconds
        TimeScale: float
    }
    
    /// Generate atmospheric turbulence layers based on seeing conditions
    let generateAtmosphericLayers (seeing: float) =
        // High-altitude jet stream layer (30% of seeing)
        let layer1 = {
            Height = 10.0 + random.NextDouble() * 5.0 // 10-15km
            Direction = random.NextDouble() * 360.0 // Random direction
            Speed = 0.5 + random.NextDouble() * 1.5 // 0.5-2.0 arcsec/sec
            Strength = 0.3 // 30% of total seeing
            TimeScale = 5.0 + random.NextDouble() * 10.0 // 5-15 seconds
        }
        
        // Mid-atmosphere thermal layer (40% of seeing)
        let layer2 = {
            Height = 5.0 + random.NextDouble() * 5.0 // 5-10km
            Direction = random.NextDouble() * 360.0
            Speed = 0.2 + random.NextDouble() * 0.8
            Strength = 0.4
            TimeScale = 3.0 + random.NextDouble() * 7.0
        }
        
        // Ground layer (30% of seeing)
        let layer3 = {
            Height = random.NextDouble() * 1.0 // 0-1km
            Direction = random.NextDouble() * 360.0
            Speed = 0.1 + random.NextDouble() * 0.3
            Strength = 0.3
            TimeScale = 1.0 + random.NextDouble() * 4.0
        }
        
        [layer1; layer2; layer3]
    
    /// Generate jitter from atmospheric layers at a specific time
    let calculateJitter (layers: AtmosphericLayer list) (seeing: float) (timestamp: float) =
        // Convert degrees to radians
        let toRadians degrees = degrees * Math.PI / 180.0
        
        // Calculate jitter from each layer
        let layerJitters = 
            layers |> List.map (fun layer ->
                // Calculate position in flow pattern
                let angle = toRadians layer.Direction
                let distance = layer.Speed * timestamp
                
                // Use noise patterns with proper time scales
                let xNoise = 
                    Math.Sin(timestamp / layer.TimeScale) * 
                    Math.Cos(timestamp / (layer.TimeScale * 0.73 + 0.27)) * 
                    Math.Sin(distance / 10.0)
                    
                let yNoise = 
                    Math.Cos(timestamp / layer.TimeScale) * 
                    Math.Sin(timestamp / (layer.TimeScale * 0.83 + 0.17)) * 
                    Math.Cos(distance / 10.0)
                
                // Calculate jitter components
                let jitterMagnitude = seeing * layer.Strength
                let jitterX = jitterMagnitude * xNoise
                let jitterY = jitterMagnitude * yNoise
                
                (jitterX, jitterY)
            )
            
        // Combine all layer jitters
        let totalX = layerJitters |> List.sumBy fst
        let totalY = layerJitters |> List.sumBy snd
        
        { X = totalX; Y = totalY }
    
    /// Calculate how seeing evolves over time
    let evolveSeeingCondition 
            (initialSeeing: float) 
            (elapsedTime: float) 
            (timestep: float) =
        
        // Real seeing has multiple timescales of evolution
        // - Rapid fluctuations (seconds)
        // - Medium trends (minutes)
        // - Slow changes (hours)
        
        // Calculate trends
        let rapidComponent = Math.Sin(elapsedTime * 0.5) * 0.05 // 5% variation over ~12 seconds
        let mediumComponent = Math.Sin(elapsedTime * 0.05) * 0.1 // 10% variation over ~2 minutes
        let slowComponent = Math.Sin(elapsedTime * 0.005) * 0.15 // 15% variation over ~20 minutes
        
        // Add random component - smaller for shorter timesteps
        let randomComponent = randomGaussian 0.0 0.02 * Math.Sqrt(timestep)
        
        // Calculate new seeing
        let trendFactor = rapidComponent + mediumComponent + slowComponent + randomComponent
        let newSeeing = initialSeeing * (1.0 + trendFactor)
        
        // Constrain to reasonable limits
        Math.Max(0.5, Math.Min(5.0, newSeeing))
    
    /// Evolve cloud coverage over time
    let evolveCloudCoverage
            (initialCoverage: float)
            (elapsedTime: float)
            (timestep: float) =
        
        // Calculate cloud trend
        // Cloud movement is smoother and has directional tendency
        let phaseOffset = 1.234 // Arbitrary phase offset
        let cloudTrend = Math.Sin(elapsedTime * 0.01 + phaseOffset) * 0.2 // 20% sinusoidal variation
        
        // Add small random component
        let randomComponent = randomGaussian 0.0 0.02 * Math.Sqrt(timestep)
        
        // Calculate new coverage
        let newCoverage = initialCoverage + cloudTrend * timestep / 100.0 + randomComponent
        
        // Constrain to [0,1]
        Math.Max(0.0, Math.Min(1.0, newCoverage))
    
    /// Create a fully evolved atmosphere state based on initial conditions
    let evolveAtmosphereState 
            (initialState: AtmosphericState) 
            (elapsedTime: float) 
            (timestep: float) =
        
        let newSeeing = 
            evolveSeeingCondition 
                initialState.SeeingCondition 
                elapsedTime 
                timestep
                
        let newCloudCoverage = 
            evolveCloudCoverage 
                initialState.CloudCoverage 
                elapsedTime 
                timestep
                
        // Transparency often inversely correlates with cloud coverage
        let newTransparency = 
            Math.Max(0.0, Math.Min(1.0, 1.0 - newCloudCoverage * 0.8))
        
        { 
            SeeingCondition = newSeeing
            CloudCoverage = newCloudCoverage
            Transparency = newTransparency
        }

/// Module for mount tracking evolution and errors
module MountTracking =
    open DomainTypes
    
    /// Random number generator
    let private random = Random()
    
    /// Generate a random value with Gaussian distribution
    let randomGaussian (mean: float) (sigma: float) =
        // Box-Muller transform
        let u1 = random.NextDouble()
        let u2 = random.NextDouble()
        
        let z0 = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2)
        mean + sigma * z0
    
    /// Convert degrees to radians
    let toRadians degrees = degrees * Math.PI / 180.0
    
    /// Convert radians to degrees
    let toDegrees radians = radians * 180.0 / Math.PI
    
    /// Calculate periodic error at a specific time
    let calculatePeriodicError 
            (amplitude: float) 
            (period: float) 
            (timestamp: float)
            (harmonics: (int * float * float) list) =
        
        if period <= 0.0 then 0.0 else
        
        // Primary periodic error (fundamental frequency)
        let primaryPhase = 2.0 * Math.PI * timestamp / period
        let primaryError = amplitude * Math.Sin(primaryPhase)
        
        // Add harmonics if provided
        let harmonicError = 
            harmonics 
            |> List.sumBy (fun (harmonic, relAmp, phase) ->
                amplitude * relAmp * Math.Sin(float harmonic * primaryPhase + phase))
                
        primaryError + harmonicError
    
    /// Calculate polar alignment errors
    let calculatePolarAlignmentEffects
            (polarError: float)  // degrees
            (timestamp: float)
            (ra: float)
            (dec: float) =
        
        if polarError <= 0.0 then (0.0, 0.0) else
        
        // Split polar error into azimuth and altitude components
        let azError = polarError * 0.707 // 45° split for simplicity
        let altError = polarError * 0.707
        
        // Convert to radians
        let azErrorRad = toRadians azError
        let altErrorRad = toRadians altError
        let decRad = toRadians dec
        
        // Hour angle affects field rotation - assume 6 hours from meridian initially
        // and adjust based on elapsed time (15°/hour)
        let hourAngleRad = toRadians(15.0 * ((timestamp / 3600.0) - 6.0))
        
        // Field rotation rate (radians/hour)
        let rotationRate = 
            15.0 * (azErrorRad * Math.Cos(hourAngleRad) / Math.Cos(decRad) - 
                    altErrorRad * Math.Sin(hourAngleRad))
                    
        // Declination drift rate (radians/hour)
        let driftRate = 
            15.0 * (azErrorRad * Math.Sin(hourAngleRad) + 
                    altErrorRad * Math.Cos(hourAngleRad) * Math.Sin(decRad))
                    
        // Convert to arcseconds per second
        let rotationArcsec = toDegrees(rotationRate) * 3600.0 / 3600.0
        let driftArcsec = toDegrees(driftRate) * 3600.0 / 3600.0
        
        (rotationArcsec, driftArcsec)
    
    /// Calculate random tracking errors
    let calculateRandomTrackingErrors (timestep: float) =
        // Scale factors for errors
        let raScale = 0.2 // arcseconds per root-second
        let decScale = 0.1 // typically less error in Dec
        
        // Random walk with correlation
        let raError = raScale * Math.Sqrt(timestep) * randomGaussian 0.0 1.0
        let decError = decScale * Math.Sqrt(timestep) * randomGaussian 0.0 1.0
        
        (raError, decError)
    
    /// Check for mechanical binding events
    let checkForBindingEvent 
            (lastBindingTime: float) 
            (timestamp: float) 
            (meanInterval: float) =
        
        // Time since last binding
        let timeSinceLastBinding = timestamp - lastBindingTime
        
        // Probability increases with time since last event
        let probability = 1.0 - Math.Exp(-timeSinceLastBinding / meanInterval)
        
        // Check if binding occurs
        if random.NextDouble() < probability * 0.1 then
            // Calculate binding jump magnitude and direction
            let magnitude = 0.5 + random.NextDouble() * 1.5 // 0.5-2.0 arcseconds
            let angle = random.NextDouble() * 2.0 * Math.PI
            
            let raJump = magnitude * Math.Cos(angle)
            let decJump = magnitude * Math.Sin(angle)
            
            // Return binding event details and new last binding time
            (true, timestamp, raJump, decJump)
        else
            // No binding occurred
            (false, lastBindingTime, 0.0, 0.0)
    
    /// Evolve mount state over time
    let evolveMountState 
            (initialState: MountState) 
            (elapsedTime: float) 
            (timestep: float)
            (lastBindingTime: float) =
            
        // Start with tracking movement (15°/hour in RA)
        let trackingRateRadPerSec = initialState.TrackingRate * Math.PI / (12.0 * 3600.0)
        let trackedRA = initialState.RA + initialState.TrackingRate * timestep / 3600.0
        
        // Calculate periodic error
        let periodicErrorArcsec = 
            calculatePeriodicError 
                initialState.PeriodicErrorAmplitude 
                initialState.PeriodicErrorPeriod 
                elapsedTime
                initialState.PeriodicErrorHarmonics
                
        // Convert to degrees
        let periodicErrorDeg = periodicErrorArcsec / 3600.0
        
        // Calculate polar alignment effects
        let (rotationArcsec, driftArcsec) = 
            calculatePolarAlignmentEffects
                initialState.PolarAlignmentError
                elapsedTime
                initialState.RA
                initialState.Dec
                
        // Convert to degrees
        let driftDeg = driftArcsec * timestep / 3600.0
        
        // Calculate random errors
        let (raRandomArcsec, decRandomArcsec) = calculateRandomTrackingErrors timestep
        let raRandomDeg = raRandomArcsec / 3600.0
        let decRandomDeg = decRandomArcsec / 3600.0
        
        // Check for binding events
        let (bindingOccurred, newBindingTime, raBindingArcsec, decBindingArcsec) = 
            checkForBindingEvent lastBindingTime elapsedTime 600.0 // Mean 10 minutes between bindings
            
        let raBindingDeg = raBindingArcsec / 3600.0
        let decBindingDeg = decBindingArcsec / 3600.0
        
        // Combine all effects
        let newRA = trackedRA + periodicErrorDeg + raRandomDeg + raBindingDeg
        let newDec = initialState.Dec + driftDeg + decRandomDeg + decBindingDeg
        
        // Create new mount state
        let newMountState = {
            initialState with
                RA = newRA
                Dec = newDec
        }
        
        (newMountState, newBindingTime)

/// Module for sensor physics simulation
module SensorPhysics =
    open DomainTypes
    
    /// Random number generator
    let private random = Random()
    
    /// Generate a random value with Gaussian distribution
    let randomGaussian (mean: float) (sigma: float) =
        // Box-Muller transform
        let u1 = random.NextDouble()
        let u2 = random.NextDouble()
        
        let z0 = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2)
        mean + sigma * z0
    
    /// Generate a Poisson-distributed random value
    let randomPoisson (lambda: float) =
        if lambda <= 0.0 then
            0.0
        elif lambda < 30.0 then
            // Direct Poisson sampling for small values
            let L = Math.Exp(-lambda)
            let mutable k = 0
            let mutable p = 1.0
            
            while p > L do
                k <- k + 1
                p <- p * random.NextDouble()
                
            float (k - 1)
        else
            // Normal approximation for large values
            let gaussian = randomGaussian lambda (Math.Sqrt(lambda))
            Math.Max(0.0, gaussian) // Ensure non-negative
    
    /// Calculate dark current based on temperature
    let calculateDarkCurrent (baseDarkCurrent: float) (temperature: float) =
        // Dark current typically doubles every 6-7°C
        // Uses 6.5°C as a middle ground
        let tempFactor = Math.Pow(2.0, temperature / 6.5)
        baseDarkCurrent * tempFactor
    
    /// Create a sensor model from camera state
    let createSensorModel (camera: CameraState) (temperature: float) =
        {
            Width = camera.Width
            Height = camera.Height
            PixelSize = camera.PixelSize
            QuantumEfficiency = 0.8  // Typical QE for modern sensors
            ReadNoise = camera.ReadNoise
            DarkCurrent = camera.DarkCurrent
            Gain = 0.5  // Typical gain for DSO imaging (e-/ADU)
            Temperature = temperature
            FullWellCapacity = 50000  // Typical for astronomical cameras
            BiasLevel = 1000  // Typical bias level
            BitDepth = 16  // Most astro cameras use 16-bit ADCs
        }
    
    /// Apply photon shot noise to a buffer
    let applyPhotonShotNoise (buffer: float[,]) =
        let width = Array2D.length1 buffer
        let height = Array2D.length2 buffer
        let result = Array2D.copy buffer
        
        for x = 0 to width - 1 do
            for y = 0 to height - 1 do
                let photons = buffer.[x, y]
                if photons > 0.0 then
                    // Apply shot noise
                    let shotNoise = 
                        if photons > 30.0 then
                            // For large photon counts, use normal approximation
                            let stdDev = Math.Sqrt(photons)
                            randomGaussian 0.0 stdDev
                        else
                            // For small counts, use direct Poisson
                            randomPoisson photons - photons
                            
                    result.[x, y] <- Math.Max(0.0, photons + shotNoise)
                    
        result
    
    /// Apply quantum efficiency to convert photons to electrons
    let applyQuantumEfficiency (buffer: float[,]) (qe: float) =
        let width = Array2D.length1 buffer
        let height = Array2D.length2 buffer
        let result = Array2D.create width height 0.0
        
        for x = 0 to width - 1 do
            for y = 0 to height - 1 do
                let photons = buffer.[x, y]
                
                // Apply QE - probabilistic conversion of photons to electrons
                let electrons = 
                    if photons > 30.0 then
                        // For large photon counts, use expectation
                        photons * qe
                    else
                        // For small counts, use Poisson to model photon statistics
                        randomPoisson (photons * qe)
                        
                result.[x, y] <- electrons
                
        result
    
    /// Apply dark current to a buffer
    let applyDarkCurrent (buffer: float[,]) (darkCurrent: float) (exposureTime: float) =
        let width = Array2D.length1 buffer
        let height = Array2D.length2 buffer
        
        for x = 0 to width - 1 do
            for y = 0 to height - 1 do
                // Expected dark electrons
                let darkElectrons = darkCurrent * exposureTime
                
                // Apply Poisson noise to dark current
                let darkNoise = randomPoisson darkElectrons
                
                // Add to buffer
                buffer.[x, y] <- buffer.[x, y] + darkNoise
                
        buffer
    
    /// Apply read noise to a buffer
    let applyReadNoise (buffer: float[,]) (readNoise: float) =
        let width = Array2D.length1 buffer
        let height = Array2D.length2 buffer
        
        for x = 0 to width - 1 do
            for y = 0 to height - 1 do
                // Add Gaussian read noise
                let noise = randomGaussian 0.0 readNoise
                buffer.[x, y] <- buffer.[x, y] + noise
                
        buffer
    
    /// Convert electrons to ADU values
    let applyADC (buffer: float[,]) (model: SensorModel) =
        let width = Array2D.length1 buffer
        let height = Array2D.length2 buffer
        let result = Array2D.create width height 0
        
        // Maximum ADU value
        let maxADU = (1 <<< model.BitDepth) - 1
        
        for x = 0 to width - 1 do
            for y = 0 to height - 1 do
                // Convert to ADU and add bias
                let adu = model.BiasLevel + int (buffer.[x, y] / model.Gain)
                
                // Clip to valid range
                let clippedADU = Math.Max(0, Math.Min(maxADU, adu))
                
                result.[x, y] <- clippedADU
                
        result
    
    /// Convert ADU values to normalized float values (0.0-1.0)
    let normalizeADU (aduValues: int[,]) (maxADU: int) =
        let width = Array2D.length1 aduValues
        let height = Array2D.length2 aduValues
        let result = Array2D.create width height 0.0
        
        // Normalize to 0.0-1.0 range
        let scale = 1.0 / float maxADU
        
        for x = 0 to width - 1 do
            for y = 0 to height - 1 do
                result.[x, y] <- float aduValues.[x, y] * scale
                
        result
    
    /// Process a buffer through the complete sensor physics pipeline
    let processSensorPhysics 
            (buffer: float[,]) 
            (model: SensorModel)
            (exposureTime: float) =
        
        // Apply quantum efficiency
        let electronBuffer = applyQuantumEfficiency buffer model.QuantumEfficiency
        
        // Add dark current
        let withDark = 
            applyDarkCurrent 
                electronBuffer 
                (calculateDarkCurrent model.DarkCurrent model.Temperature)
                exposureTime
        
        // Add read noise
        let withNoise = applyReadNoise withDark model.ReadNoise
        
        // Convert to ADU
        let aduValues = applyADC withNoise model
        
        // Normalize to 0.0-1.0
        let normalized = normalizeADU aduValues ((1 <<< model.BitDepth) - 1)
        
        normalized

/// Module for buffer operations
module BufferOperations =
    open DomainTypes
    
    /// Create an empty buffer of the specified size
    let createEmptyBuffer (width: int) (height: int) =
        Array2D.zeroCreate width height
    
    /// Accumulate photons from a star onto the buffer
    let accumulatePhotons 
        (buffer: float[,]) 
        (x: float) 
        (y: float) 
        (photons: float) 
        (psf: PSF) =
        
        // Get dimensions
        let width = Array2D.length1 buffer
        let height = Array2D.length2 buffer
        let psfSize = psf.Size
        
        // PSF is centered on the star position
        let psfHalfSize = psfSize / 2
        
        // Calculate buffer indices for the center of the PSF using rounding for accurate centering
        let centerX = int (Math.Round(x))
        let centerY = int (Math.Round(y))
        
        // Calculate the region where the PSF overlaps the buffer
        let startX = Math.Max(0, centerX - psfHalfSize)
        let startY = Math.Max(0, centerY - psfHalfSize)
        let endX = Math.Min(width - 1, centerX + psfHalfSize)
        let endY = Math.Min(height - 1, centerY + psfHalfSize)
        
        // First calculate total PSF weight that will be applied
        let mutable totalAppliedWeight = 0.0
        
        for bufferX = startX to endX do
            for bufferY = startY to endY do
                // Calculate PSF indices
                let psfX = bufferX - (centerX - psfHalfSize)
                let psfY = bufferY - (centerY - psfHalfSize)
                
                // Skip if outside PSF bounds
                if psfX >= 0 && psfX < psfSize && psfY >= 0 && psfY < psfSize then
                    totalAppliedWeight <- totalAppliedWeight + psf.Values.[psfX, psfY]
        
        // Apply normalization factor if needed to conserve energy
        let normalizationFactor = 
            if totalAppliedWeight > 0.0 && Math.Abs(totalAppliedWeight - 1.0) > 1e-6 then
                1.0 / totalAppliedWeight
            else
                1.0
        
        // Apply the PSF to the buffer with normalization
        for bufferX = startX to endX do
            for bufferY = startY to endY do
                // Calculate PSF indices
                let psfX = bufferX - (centerX - psfHalfSize)
                let psfY = bufferY - (centerY - psfHalfSize)
                
                // Only apply if within PSF bounds
                if psfX >= 0 && psfX < psfSize && psfY >= 0 && psfY < psfSize then
                    let psfValue = psf.Values.[psfX, psfY]
                    buffer.[bufferX, bufferY] <- buffer.[bufferX, bufferY] + photons * psfValue * normalizationFactor
    
    /// Apply cloud cover effect to a buffer
    let applyCloudCover 
            (buffer: float[,]) 
            (cloudCoverage: float) 
            (timestamp: float) =
        
        if cloudCoverage <= 0.0 then
            // No clouds, no change
            buffer
        else
            let width = Array2D.length1 buffer
            let height = Array2D.length2 buffer
            
            // Cloud pattern parameters
            let cloudScale = 0.1  // Scale of cloud features
            let cloudSpeed = 0.05  // Speed of cloud movement
            
            // Apply cloud pattern
            for x = 0 to width - 1 do
                for y = 0 to height - 1 do
                    // Generate cloud pattern (simplified Perlin-like)
                    let nx = float x / float width
                    let ny = float y / float height
                    
                    // Move pattern over time
                    let tx = nx + timestamp * cloudSpeed
                    let ty = ny + timestamp * cloudSpeed * 0.7
                    
                    // Generate noise value
                    let noise = 
                        0.5 + 0.5 * Math.Sin(tx * 6.28 * cloudScale) * 
                                    Math.Cos(ty * 6.28 * cloudScale) * 
                                    Math.Sin((tx + ty) * 6.28 * cloudScale / 2.0)
                                    
                    // Calculate attenuation
                    let attenuation = 1.0 - cloudCoverage * noise
                    
                    // Apply to buffer
                    buffer.[x, y] <- buffer.[x, y] * attenuation
            
            buffer
    
    /// Combine multiple buffers by summing
    let combineBuffers (buffers: float[,] list) =
        if List.isEmpty buffers then
            createEmptyBuffer 0 0
        else
            let firstBuffer = List.head buffers
            let width = Array2D.length1 firstBuffer
            let height = Array2D.length2 firstBuffer
            
            // Create result buffer
            let result = createEmptyBuffer width height
            
            // Add all buffers
            for buffer in buffers do
                for x = 0 to width - 1 do
                    for y = 0 to height - 1 do
                        result.[x, y] <- result.[x, y] + buffer.[x, y]
            
            result
    
    /// Convert 2D buffer to 1D array in row-major order
    let bufferToArray (buffer: float[,]) =
        let width = Array2D.length1 buffer
        let height = Array2D.length2 buffer
        let array = Array.zeroCreate (width * height)
        
        for y = 0 to height - 1 do
            let rowOffset = y * width
            for x = 0 to width - 1 do
                array.[rowOffset + x] <- buffer.[x, y]
                
        array
    
    /// Accumulate a full subframe
    let processSubframe 
            (buffer: float[,]) 
            (stars: PixelStar list) 
            (optics: OpticalParameters)
            (atmosphereState: AtmosphericState)
            (plateScale: float)
            (timestamp: float) =
        
        // Generate atmospheric jitter
        let layers = AtmosphericEvolution.generateAtmosphericLayers atmosphereState.SeeingCondition
        let jitter = AtmosphericEvolution.calculateJitter layers atmosphereState.SeeingCondition timestamp
        
        // Process each star
        for star in stars do
            // Get photon flux
            match star.PhotonFlux with
            | Some flux ->
                // Apply jitter to star position
                let jitteredX = star.X + jitter.X / plateScale
                let jitteredY = star.Y + jitter.Y / plateScale
                
                // Generate PSF for this star
                let psf = PsfGeneration.generateCombinedPSF 
                              optics 
                              atmosphereState.SeeingCondition 
                              plateScale 
                              star.Star.Color 
                              optics.FRatio
                
                // Accumulate photons
                accumulatePhotons buffer jitteredX jitteredY flux psf
            | None -> 
                // Skip stars without flux calculated
                ()
                
        // Apply cloud coverage
        if atmosphereState.CloudCoverage > 0.0 then
            applyCloudCover buffer atmosphereState.CloudCoverage timestamp
        else
            buffer