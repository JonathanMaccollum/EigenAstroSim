namespace EigenAstroSim.Domain

open System
open EigenAstroSim.Domain
open EigenAstroSim.Domain.Types

/// Telescope optical parameters with additional details
type EnhancedTelescopeOptics = {
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
    
    /// Telescope type
    TelescopeType: TelescopeType
    
    /// Focal ratio (f/number)
    FRatio: float
}

/// Types of telescopes with different optical characteristics
and TelescopeType =
    | Reflector  // Newtonian, Dobsonian
    | SCT        // Schmidt-Cassegrain
    | Refractor  // Refractive telescope
    | RCT        // Ritchey-Chrétien
    | Maksutov   // Maksutov-Cassegrain

/// Enhanced Point Spread Function calculations
module EnhancedPSF =
    /// Mathematical constants
    let private pi = Math.PI
    
    /// Bessel function of first kind, order 1
    /// Approximation valid for typical astronomical PSF calculations
    let rec private besselJ1 (x: float) =
        if x = 0.0 then
            0.5
        else if x < 8.0 then
            // Polynomial approximation for small x values
            let x2 = x * x
            let num = x * (0.5 - x2 / 16.0 * (1.0 - x2 / 384.0 * (1.0 - x2 / 18432.0)))
            num
        else
            // Asymptotic form for large x values
            let f = Math.Sqrt(0.636619772 / x)
            let theta = x - 2.356194491
            f * Math.Cos(theta)

    /// Calculate factor to convert from mm to pixels
    let private mmToPixels (pixelSize: float) = 1.0 / pixelSize
    
    /// Generate Airy disk PSF for diffraction-limited optics
    let generateAiryDiskPSF 
            (optics: EnhancedTelescopeOptics) 
            (wavelength: float)  // in nanometers
            (pixelSize: float)   // in microns
            (size: int) : float[,] =
        
        // Create the PSF array
        let psf = Array2D.zeroCreate size size
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
                        let v = pi * r * scaleFactor
                        
                        if epsilon > 0.0 then
                            // With central obstruction
                            let airy = (besselJ1(v) / v - epsilon * epsilon * besselJ1(epsilon * v) / (epsilon * v))
                            airy * airy * 4.0 / ((1.0 - epsilon * epsilon) * (1.0 - epsilon * epsilon))
                        else
                            // Without central obstruction (standard Airy disk)
                            let airy = 2.0 * besselJ1(v) / v
                            airy * airy
                
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
        
    /// Generate a realistic atmospheric seeing PSF
    /// This approximates a long-exposure seeing-limited PSF
    let generateAtmosphericPSF 
            (seeingFWHM: float)  // in arcseconds
            (plateScale: float)  // in arcseconds per pixel
            (size: int) : float[,] =
        
        // Convert seeing FWHM from arcseconds to pixels
        let fwhmPixels = seeingFWHM / plateScale
        
        // Convert FWHM to sigma: sigma = FWHM / (2.35482)
        let sigma = fwhmPixels / 2.35482
        
        // Create the PSF array
        let psf = Array2D.zeroCreate size size
        let center = float size / 2.0
        
        // Fill with Gaussian values (long exposure seeing approximation)
        for x = 0 to size - 1 do
            for y = 0 to size - 1 do
                let dx = float x - center
                let dy = float y - center
                let r2 = dx * dx + dy * dy
                let value = Math.Exp(-r2 / (2.0 * sigma * sigma))
                psf.[x, y] <- value
        
        // Normalize PSF
        let mutable sum = 0.0
        for x = 0 to size - 1 do
            for y = 0 to size - 1 do
                sum <- sum + psf.[x, y]
        
        if sum > 0.0 then
            for x = 0 to size - 1 do
                for y = 0 to size - 1 do
                    psf.[x, y] <- psf.[x, y] / sum
        
        psf
    
    /// Calculate the size of the PSF array needed
    let calculatePSFSize (seeingFwhm: float) (plateScale: float) =
        // Convert seeing from arcseconds to pixels
        let fwhmPixels = seeingFwhm / plateScale
        
        // Make the PSF size at least 5 times the FWHM to capture >99% of the energy
        // Ensure it's odd for a centered peak
        let size = int (Math.Ceiling(fwhmPixels * 5.0))
        if size % 2 = 0 then size + 1 else size
        
    /// Convolve the optical PSF with atmospheric seeing PSF
    /// This simulates the combined effect of diffraction and atmospheric turbulence
    let convolveOpticalAndAtmosphericPSFs 
            (opticalPSF: float[,]) 
            (atmosphericPSF: float[,]) : float[,] =
        
        // Get dimensions
        let optSize = Array2D.length1 opticalPSF
        let atmSize = Array2D.length1 atmosphericPSF
        
        // Result size will be optSize + atmSize - 1
        let resultSize = optSize + atmSize - 1
        let result = Array2D.zeroCreate resultSize resultSize
        
        // Perform the convolution
        for i1 = 0 to optSize - 1 do
            for j1 = 0 to optSize - 1 do
                for i2 = 0 to atmSize - 1 do
                    for j2 = 0 to atmSize - 1 do
                        let i = i1 + i2
                        let j = j1 + j2
                        result.[i, j] <- result.[i, j] + opticalPSF.[i1, j1] * atmosphericPSF.[i2, j2]
        
        // Trim to a reasonable size (same as the larger of the inputs)
        let trimSize = Math.Max(optSize, atmSize)
        let startIdx = (resultSize - trimSize) / 2
        let endIdx = startIdx + trimSize - 1
        
        let trimmed = Array2D.zeroCreate trimSize trimSize
        for i = 0 to trimSize - 1 do
            for j = 0 to trimSize - 1 do
                trimmed.[i, j] <- result.[startIdx + i, startIdx + j]
        
        // Normalize the result
        let mutable sum = 0.0
        for i = 0 to trimSize - 1 do
            for j = 0 to trimSize - 1 do
                sum <- sum + trimmed.[i, j]
        
        if sum > 0.0 then
            for i = 0 to trimSize - 1 do
                for j = 0 to trimSize - 1 do
                    trimmed.[i, j] <- trimmed.[i, j] / sum
        
        trimmed
        
    /// Create enhanced optics from mount and camera information
    let createEnhancedOptics (mountState: MountState) =
        let focalLength = mountState.FocalLength
        
        // Estimate aperture from focal length using typical f-ratios
        // For a typical f/7 system:
        let aperture = focalLength / 7.0
        
        // Typical central obstruction is about 33% of aperture diameter for SCTs
        let obstruction = aperture * 0.33
        
        // Typical optical quality for an amateur telescope (Strehl ratio)
        let opticalQuality = 0.85
        
        // Calculate focal ratio
        let fRatio = focalLength / aperture
        
        {
            Aperture = aperture
            Obstruction = obstruction
            FocalLength = focalLength
            Transmission = 0.85  // Typical transmission including mirrors and corrector plates
            OpticalQuality = opticalQuality
            TelescopeType = SCT  // Default to SCT
            FRatio = fRatio
        }
        
    /// Calculate the optimum wavelength for PSF calculation based on star color
    let colorIndexToWavelength (colorIndex: float) =
        // Approximate conversion based on stellar spectral types
        // B-V = -0.3 (hot blue stars) -> ~450nm
        // B-V = 0.6 (Sun-like stars) -> ~550nm  
        // B-V = 1.5 (cool red stars) -> ~650nm
        450.0 + (colorIndex + 0.3) * 100.0
        |> min 700.0
        |> max 400.0
        
    /// Generate final PSF for a star combining optical and atmospheric effects
    let generateCombinedPSF 
            (optics: EnhancedTelescopeOptics)
            (seeing: float)  // in arcseconds
            (plateScale: float)  // in arcseconds per pixel
            (colorIndex: float)
            (pixelSize: float) : float[,] =
    
        // Calculate appropriate wavelength
        let wavelength = colorIndexToWavelength colorIndex
        
        // Calculate appropriate PSF size
        let psfSize = calculatePSFSize seeing plateScale
        
        // Generate the optical PSF
        let opticalPSF = generateAiryDiskPSF optics wavelength pixelSize psfSize
        
        // Generate the atmospheric PSF
        let atmosphericPSF = generateAtmosphericPSF seeing plateScale psfSize
        
        // Convolve to get the final PSF
        convolveOpticalAndAtmosphericPSFs opticalPSF atmosphericPSF