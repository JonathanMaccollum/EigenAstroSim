# Point Source Propagation Through Astrophotography Imaging Layers

## Overview

This document details how a point source (like a star) propagates through each transformation layer in our virtual astrophotography system, while ensuring memory efficiency. The process follows a series of transformations that convert a mathematical point in celestial coordinates into a realistic star image spread across multiple pixels.

## 1. Initial Celestial Representation

The process begins with a mathematical point source in celestial coordinates:

```fsharp
// A star in our model
let star = {
    Id = CelestialObjectId(Guid.NewGuid())
    Coordinates = { 
        RightAscension = RightAscension(12.34) // hours
        Declination = Declination(56.78) // degrees 
    }
    Properties = Star {
        Magnitude = Magnitude(8.5)
        SpectralType = SpectralType("G2")
        ColorIndex = ColorIndex(0.65) // B-V index (solar-type)
    }
    Motion = None
}
```

At this stage, the star is simply a position and set of properties with no physical extent.

## 2. Optical System Transformation

When the point source passes through our telescope, it transforms according to optical principles:

```fsharp
// Calculate the theoretical PSF based on optical parameters
let calculateOpticalPSF (opticalSystem: OpticalSystem) (star: CelestialObject) =
    // Diffraction limited PSF is: 
    // - For a perfect optical system: Airy disk with diameter = 2.44 * λ * f-ratio
    // - Modified by central obstruction
    // - Additional optical aberrations add their own effects
    
    let wavelength = getWavelengthFromSpectralType (getSpectralType star.Properties)
    let fRatio = getFocalRatio opticalSystem
    
    // Airy disk radius in microns at sensor plane
    let airyDiskRadius = 1.22 * wavelength * fRatio
    
    // Convert to pixel space based on sensor pixel size
    let pixelSize = getPixelSize sensor.PixelSize
    let airyDiskRadiusInPixels = airyDiskRadius / pixelSize
    
    // Create the PSF matrix (typically 15x15 pixels for reasonable sampling)
    // Center-weighted matrix modeling the Airy pattern
    // This becomes our initial PSF for this star
    generateAiryPattern airyDiskRadiusInPixels
```

This transforms our mathematical point into a small intensity distribution pattern (the Point Spread Function or PSF). Even with a perfect optical system, diffraction causes the star to spread across multiple pixels.

The size of this spread depends on:
- Telescope aperture (larger apertures create smaller Airy disks)
- Focal length (longer focal lengths create larger Airy disks in terms of pixels)
- Wavelength of light (longer wavelengths create larger Airy disks)
- Sensor pixel size (smaller pixels sample the Airy disk with higher resolution)

## 3. Projection Onto Sensor Coordinates

Before applying further transformations, we need to project the star onto our sensor coordinates:

```fsharp
// Project celestial coordinates to sensor pixel coordinates
let projectToSensor (star: CelestialObject) (mountState: MountState) (sensor: Sensor) =
    // Calculate difference between star coordinates and mount pointing
    let deltaRA = (unwrap star.Coordinates.RightAscension) - (unwrap mountState.Coordinates.RightAscension)
    let deltaDec = (unwrap star.Coordinates.Declination) - (unwrap mountState.Coordinates.Declination)
    
    // Convert to angular offset in arcseconds
    let raOffset = deltaRA * 15.0 * 3600.0 * Math.Cos(toRadians (unwrap mountState.Coordinates.Declination))
    let decOffset = deltaDec * 3600.0
    
    // Convert angular offset to pixels using plate scale
    let plateScale = calculatePlateScale opticalSystem sensor.PixelSize // arcsec/pixel
    let xOffset = -raOffset / plateScale  // Negated because RA increases eastward
    let yOffset = decOffset / plateScale
    
    // Sensor center coordinates
    let centerX = float sensor.Dimensions.Width / 2.0
    let centerY = float sensor.Dimensions.Height / 2.0
    
    // Final pixel coordinates
    let pixelX = centerX + xOffset
    let pixelY = centerY + yOffset
    
    (pixelX, pixelY)
```

This calculation determines where on our sensor array the center of the star's PSF will be positioned. The position depends on:
- The difference between the star's coordinates and where the telescope is pointing
- The optical system's plate scale (arcseconds per pixel)
- The center of the sensor array

## 4. Memory-Efficient Transformation Pipeline

Now we'll create a transformation pipeline that efficiently handles our 0.1s sub-frames:

```fsharp
// Main transformation pipeline for a single sub-exposure
let processSubExposure (starField: StarField) (mountState: MountState) 
                       (atmosphere: AtmosphericConditions) (sensor: Sensor) 
                       (opticalSystem: OpticalSystem) (duration: TimeSpan) =
    
    // Create a single reusable in-memory buffer for photon accumulation
    // This will be transformed in place to avoid multiple copies
    let mutable photonBuffer = Array2D.zeroCreate sensor.Dimensions.Width sensor.Dimensions.Height
    
    // Get visible stars based on current mount pointing and FOV
    let visibleStars = 
        StarFieldService.getVisibleObjects 
            starField 
            mountState.Coordinates 
            (calculateFieldOfView opticalSystem sensor)
    
    // For each visible star:
    for star in visibleStars do
        // 1. Calculate base PSF based on optics
        let basePSF = calculateOpticalPSF opticalSystem star
        
        // 2. Project star onto sensor to get pixel coordinates
        let (pixelX, pixelY) = projectToSensor star mountState sensor
        
        // Only process stars that would affect the sensor
        if isInSensorBounds pixelX pixelY sensor.Dimensions basePSF.Width basePSF.Height then
            // 3. Calculate photon flux based on star magnitude and telescope aperture
            let photonFlux = calculatePhotonFlux star opticalSystem duration
            
            // 4. Apply atmospheric seeing effect (modifies PSF in place)
            let psfWithSeeing = 
                applyAtmosphericSeeing 
                    basePSF 
                    atmosphere.Seeing 
                    atmosphere.TurbulenceLayers
            
            // 5. Apply instantaneous mount tracking errors
            // This may shift the center point and/or smear the PSF
            let psfWithTrackingErrors =
                applyTrackingErrors
                    psfWithSeeing
                    mountState
                    duration
            
            // 6. Apply additional atmospheric effects (clouds, scintillation)
            let finalPSF =
                applyCloudCover 
                    (applyScintillation psfWithTrackingErrors atmosphere.Scintillation)
                    atmosphere.CloudCover
                    atmosphere.CloudPattern
            
            // 7. Add the final PSF to our accumulation buffer (photon counts)
            // This distributes the star's photons according to the calculated pattern
            accumulatePSF photonBuffer pixelX pixelY finalPSF photonFlux
    
    // 8. Add other phenomena (satellites, aircraft, etc)
    addTransientObjects photonBuffer mountState duration
    
    // 9. Apply sensor effects (quantum efficiency, hot pixels)
    applySensorEffects photonBuffer sensor
    
    // 10. Add noise (shot noise, dark current)
    addSensorNoise photonBuffer sensor duration
    
    // Return the final accumulated sub-frame
    photonBuffer
```

The key memory efficiency aspect is using a single buffer that gets modified in place through each transformation, rather than creating new copies at each step.

## 5. Detailed PSF Transformation Example

Let's examine how a single star's PSF gets modified through various layers:

```fsharp
// Example: How atmospheric seeing modifies the PSF
let applyAtmosphericSeeing (basePSF: PSF) (seeing: Seeing) (turbulenceLayers: TurbulenceLayer list) =
    // Seeing is typically modeled as a Gaussian blur
    // The standard deviation is proportional to the seeing FWHM
    let seeingFWHM = unwrap seeing // in arcseconds
    
    // Convert seeing to pixels
    let seeingPixels = seeingFWHM / plateScale
    
    // Calculate Gaussian sigma from FWHM
    let sigma = seeingPixels / 2.355
    
    // Generate 2D Gaussian kernel for the seeing effect
    let kernel = generateGaussianKernel sigma
    
    // Apply convolution to the base PSF
    // This spreads out the PSF according to atmospheric seeing
    convolve basePSF kernel
```

This is one of the most significant transformations, as it tends to spread the star image much more than the optical PSF alone. A seeing of 3 arcseconds (common in average locations) might spread a star's light over many more pixels than diffraction alone would.

## 6. Sub-frame Accumulation and Memory Management

For a 2-second exposure comprised of twenty 0.1s sub-frames, we need to efficiently combine them:

```fsharp
// Process a full exposure as a series of sub-exposures
let processFullExposure (starField: StarField) (initialMountState: MountState)
                        (atmosphere: AtmosphericConditions) (sensor: Sensor)
                        (opticalSystem: OpticalSystem) (exposureSettings: ExposureSettings) =
    
    // Create the final accumulation buffer
    let finalImage = Array2D.zeroCreate sensor.Dimensions.Width sensor.Dimensions.Height
    
    // Reactive processing stream for sub-exposures
    Observable.Interval(TimeSpan.FromMilliseconds(100.0))
    |> Observable.take (int (exposureSettings.TotalDuration.TotalMilliseconds / 100.0))
    |> Observable.scan 
        (fun (mountState, atmosphere, _) _ ->
            // Evolve mount state and atmospheric conditions for this time step
            let newMountState = MountService.updateMountState mountState (TimeSpan.FromMilliseconds(100.0))
            let newAtmosphere = AtmosphericService.evolveAtmosphericConditions atmosphere (TimeSpan.FromMilliseconds(100.0))
            
            // Process this sub-exposure using current conditions
            let subFrame = processSubExposure 
                              starField 
                              newMountState 
                              newAtmosphere 
                              sensor 
                              opticalSystem 
                              (TimeSpan.FromMilliseconds(100.0))
            
            // Add this sub-frame to our final image
            lock finalImage (fun () ->
                for x = 0 to sensor.Dimensions.Width - 1 do
                    for y = 0 to sensor.Dimensions.Height - 1 do
                        finalImage.[x, y] <- finalImage.[x, y] + subFrame.[x, y]
            )
            
            // Return updated state (subFrame is now out of scope and can be garbage collected)
            (newMountState, newAtmosphere, ())
        )
        (initialMountState, atmosphere, ())
    |> Observable.subscribe (fun _ -> ())
    
    // Final image processing after all sub-exposures
    let processedImage = applyFinalProcessing finalImage sensor
    
    processedImage
```

This reactive approach processes each sub-frame as it "occurs" in real time, immediately accumulating it into the final image and letting the sub-frame be garbage collected.

## 7. The Final Result: How a Star Spreads Across Pixels

The extent to which a star spreads across pixels depends on several factors:

### 7.1 Optical PSF Size vs. Pixel Size

- **Undersampled Regime**: If the Airy disk is smaller than a pixel, the star may appear mostly in one pixel with some light bleeding into adjacent pixels. This happens with short focal lengths, large pixels, or small apertures.

- **Well-Sampled Regime**: If the Airy disk spans 2-3 pixels (considered optimal sampling), the star naturally spreads in a pattern that resembles the theoretical Airy disk, with a bright central pixel surrounded by fainter concentric rings.

- **Oversampled Regime**: If the Airy disk spans many pixels (common with long focal lengths or very small pixels), the star image will show detailed diffraction patterns including the central Airy disk and diffraction rings.

### 7.2 Atmospheric Seeing Effects

- **Excellent Seeing** (<1 arcsecond): Star images approach the theoretical diffraction limit of the telescope. In short exposures, you might see speckle patterns as the atmosphere "freezes" momentarily.

- **Average Seeing** (2-3 arcseconds): Stars spread considerably beyond their optical PSF size. A 2.5 arcsecond seeing might create a star FWHM of 5-6 pixels in a typical setup.

- **Poor Seeing** (>4 arcseconds): Stars become very bloated, potentially spreading across dozens of pixels, with the light concentrated in a roughly Gaussian distribution.

### 7.3 Tracking Errors Impact

- **Perfect Tracking**: The PSF remains centered on the same pixels throughout the exposure.

- **Periodic Error**: The PSF gradually oscillates around a center point, creating a slightly elongated star image.

- **Binding or Sudden Errors**: The PSF jumps to new positions, creating disjointed star patterns or dramatic trailing.

- **Consistent Drift**: The PSF moves steadily in one direction, creating classic star trails in longer exposures.

### 7.4 Time Effects

- **Very Short Exposures** (0.1s): May "freeze" atmospheric effects, resulting in speckle patterns rather than a smooth Gaussian.

- **Medium Exposures** (1-5s): Average out seeing effects, creating more Gaussian-like PSFs with possible elongation from tracking errors.

- **Long Exposures** (>30s): Completely average out atmospheric effects but are highly susceptible to tracking errors and field rotation.

## 8. Memory Efficiency Best Practices

### 8.1 Single Buffer Reuse

```fsharp
// Reuse buffer instead of creating new ones
let mutable photonBuffer = Array2D.zeroCreate width height
// Modify buffer in place through transformations
applyTransformation photonBuffer
```

### 8.2 In-place Transformations

```fsharp
// Modify existing arrays in place when possible
let applyGaussianBlur (buffer: float[,]) (sigma: float) =
    // Generate kernel
    let kernel = generateKernel sigma
    // Apply convolution in place
    for x = 0 to buffer.GetLength(0) - 1 do
        for y = 0 to buffer.GetLength(1) - 1 do
            // In-place convolution algorithm
            // ...
```

### 8.3 Explicit Cleanup of Temporary Resources

```fsharp
// Use computation expressions for resource management
use tempBuffer = new DisposableBuffer(width, height)
// Work with tempBuffer
// Buffer automatically disposed at end of scope
```

### 8.4 Reactive Stream Processing with Buffer Management

```fsharp
// Use Observable.map with explicit buffer management
subFrameObservable
|> Observable.map (fun oldBuffer -> 
    // Process oldBuffer to create new one
    let newBuffer = processBuffer oldBuffer
    // Explicitly release old buffer if needed
    oldBuffer.Release()
    newBuffer)
```

### 8.5 Object Pooling for Frequent Allocations

```fsharp
// Buffer pool for PSF calculations
let psfBufferPool = new ObjectPool<float[,]>(
    createFunc = (fun () -> Array2D.zeroCreate 15 15),
    resetFunc = (fun buffer -> Array2D.fill buffer 0 0 15 15 0.0)
)

// Get buffer from pool, use it, return it
let calculatePSF() =
    let buffer = psfBufferPool.Get()
    try
        // Use buffer for calculations
        // ...
        buffer
    finally
        psfBufferPool.Return(buffer)
```

## 9. Conclusion

The propagation of a star through our virtual astrophotography system is a complex process involving multiple transformations that accurately model real-world physics. By combining optical diffraction, atmospheric effects, mount tracking errors, and sensor characteristics, we create realistic star images that vary from perfect round PSFs to elongated trails depending on conditions.

Our memory-efficient approach ensures that we can process these transformations in real-time without excessive memory allocations, making it possible to generate realistic 0.1s sub-frames that accumulate into complete exposures. This approach maintains high-fidelity simulation while optimizing performance for real-time processing.