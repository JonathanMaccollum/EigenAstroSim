# Advanced Astrophotography Simulation
# Subframes, Seeing Effects, and Quantum Efficiency

## Conceptual Overview

The current simulation approach produces unrealistically perfect star profiles that don't capture the dynamic nature of atmospheric seeing and sensor characteristics. A more realistic approach would involve:

1. **Subframe Composition**: Breaking exposures into small (~0.1s) subframes
2. **Dynamic Seeing Effects**: Applying correlated jitter to star positions between subframes
3. **Quantum Efficiency Modeling**: Simulating the probabilistic nature of photon detection
4. **Realistic Noise Accumulation**: Adding read noise only once at the end of the total exposure

This approach will capture the temporal and statistical nature of real-world astrophotography, producing images with the imperfections and characteristics found in actual data.

## Reimagining Our Current Implementation

Our current simulation creates perfect Gaussian stars with a static PSF (Point Spread Function). In reality, stars appear as complex, often elongated or asymmetric patterns due to atmospheric turbulence, tracking imperfections, and optical aberrations. By using our existing code to generate "perfect" subframes and then composing them with realistic perturbations, we can create much more authentic simulations.

## Test-Driven Implementation Strategy

To implement this enhanced approach in a test-driven way:

1. **Create Interface Extensions** - Add types and function signatures for the new features
2. **Write Tests** - Verify realistic star jitter, QE effects, and tracking errors
3. **Implement Core Logic** - Build the subframe composition system
4. **Connect with Existing Code** - Use our current rendering for the basic subframes
5. **Add Parameters to UI** - Allow users to control seeing patterns, QE, etc.

The tests should verify that:
- Stars show elongation in the direction of atmospheric flow
- Star brightness follows proper statistical distributions
- Long exposures show tracking errors like periodic error and cable snags

## 1. Subframe Architecture

### Core Strategy

Instead of generating a single image for the entire exposure duration, we'll:

```
For each exposure:
  1. Initialize an empty accumulation buffer
  2. Break the exposure into N subframes (typically 0.1s each)
  3. For each subframe:
     a. Calculate telescope pointing (including tracking errors)
     b. Generate jittered star positions based on seeing conditions
     c. Accumulate photons into the buffer with QE probability
  4. Apply sensor noise once to the final accumulated image
```

This approach more accurately simulates how a real camera sensor accumulates photons over time, and how atmospheric effects continuously disturb the light path during an exposure.

### Advantages of Subframe Technique

1. **Realistic Tracking Errors**: Can simulate periodic error, wind-induced tracking errors, and cable snags
2. **Dynamic Seeing Effects**: Accounts for the temporal evolution of seeing conditions
3. **Statistical Photon Accumulation**: Models the Poisson nature of photon arrivals
4. **Directional Blurring**: Creates the elongated star shapes seen in real astrophotography
5. **Improved Noise Modeling**: Separates photon shot noise (which accumulates with each subframe) from read noise (applied once per exposure)

### Implementation Structure

```fsharp
/// Top-level exposure generation function
let generateRealisticExposure (state: SimulationState) =
    // Initialize empty accumulation buffer
    let width, height = state.Camera.Width, state.Camera.Height
    let accumulationBuffer = Array2D.create width height 0.0
    
    // Calculate how many subframes we need
    let subframeDuration = 0.1 // 0.1 seconds per subframe
    let subframeCount = int (state.Camera.ExposureTime / subframeDuration)
    
    // Generate atmospheric turbulence model
    let atmosphericLayers = generateAtmosphericLayers state.Atmosphere.SeeingCondition
    
    // Generate a drift model for tracking errors
    let trackingErrorModel = generateTrackingErrorModel state.Mount
    
    // Generate subframes and accumulate
    for i = 0 to subframeCount - 1 do
        // Calculate time point
        let subframeTime = float i * subframeDuration
        
        // Calculate mount position at this time (including tracking errors)
        let mountPosition = calculateMountPosition state.Mount subframeTime trackingErrorModel
        
        // Calculate atmospheric jitter at this time
        let jitter = calculateAtmosphericJitter atmosphericLayers subframeTime
        
        // Generate a clean subframe using existing code
        // (This reuses our current star field and rendering logic, but for a short duration)
        let subframeState = {
            state with
                Camera = { state.Camera with ExposureTime = subframeDuration }
                Mount = mountPosition
        }
        
        // Generate perfect subframe
        let perfectSubframe = generateImage subframeState
        
        // Apply jitter to subframe
        let jitteredSubframe = applyJitter perfectSubframe jitter
        
        // Apply quantum efficiency to convert photons to electrons
        let detectedSubframe = applyQuantumEfficiency jitteredSubframe state.Camera.QuantumEfficiency
        
        // Accumulate into buffer
        accumulateSubframe accumulationBuffer detectedSubframe
    
    // Apply sensor readout effects once
    applyFinalSensorEffects accumulationBuffer state.Camera
```

### Leveraging Existing Code

This approach allows us to reuse much of our existing code:

1. `generateImage` creates perfect subframes
2. We add new functions to handle jitter, QE, and accumulation
3. The final image has all the characteristics of real astrophotography

## 2. Dynamic Seeing Effects

### Physical Model

Atmospheric seeing involves several layers of turbulence that can be modeled as:

1. **High-altitude jet stream** (10-15km): Fast-moving, large-scale distortion
2. **Middle atmosphere thermal layers** (5-10km): Medium-speed, moderate distortion 
3. **Ground-level boundary layer** (0-1km): Slower movement but potentially stronger distortion

Each layer has:
- Direction vector (wind direction)
- Velocity (speed)
- Characteristic distortion scale
- Characteristic time scale

This multi-layer approach follows established models in atmospheric optics like the Kolmogorov turbulence model, which describes how air turbulence affects light propagation through statistical variations in the refractive index of air.

### Implementation Approach

```fsharp
/// Calculate jitter from atmospheric layers at a specific time point
let calculateAtmosphericJitter (layers: AtmosphericLayer[]) (time: float) =
    // Each layer contributes to the overall jitter
    layers
    |> Array.map (fun layer ->
        // Calculate position in the distortion pattern based on time and speed
        let patternPosition = time * layer.Speed
        
        // Use normalized noise function for distortion
        // We can use math functions for approximating Perlin-like noise
        let noiseX = Math.Sin(patternPosition + layer.Direction * 0.01) * 
                      Math.Cos(patternPosition * 0.7 + layer.TimeScale * 0.1)
        let noiseY = Math.Cos(patternPosition + layer.Direction * 0.01) * 
                      Math.Sin(patternPosition * 0.7 + layer.TimeScale * 0.1)
                
        // Scale by the layer's contribution to overall seeing
        (noiseX * layer.DistortionScale * layer.DistortionContribution,
         noiseY * layer.DistortionScale * layer.DistortionContribution))
    |> Array.reduce (fun (x1, y1) (x2, y2) -> (x1 + x2, y1 + y2))
```

### Integration with Existing Code

The subframe approach allows us to reuse our current star rendering code while adding realism. The key changes are:

1. Break long exposures into short subexposures
2. Apply jitter and tracking errors to each subframe
3. Apply quantum efficiency during accumulation
4. Add read noise only to the final accumulated image

This mimics the physical reality of how photons arrive at the sensor over time and allows for realistic movement effects.

## Performance Considerations

Simulating many short subframes could be computationally expensive. We can optimize by:

1. **Scaled Complexity** - Use fewer subframes for preview/testing modes
2. **Parallel Processing** - Generate subframes in parallel
3. **GPU Acceleration** - Use pixel shaders for the jitter and compositing
4. **Adaptive Subframe Rate** - Use more subframes during rapid changes (e.g., cable snags)
5. **Caching** - Cache the star field calculation between subframes

For a 30-second exposure with 0.1s subframes, we'd generate 300 frames. Using smart optimizations, this can still maintain interactive performance on modern hardware.

### Temporal Correlation

Key to realistic seeing simulation is temporal correlation - the jitter in one frame should be related to the previous frame. This can be achieved using:

1. **Perlin Noise**: For smoothly evolving patterns
2. **Fourier-based methods**: Generating frequency-space patterns with appropriate power spectra
3. **Random walks with momentum**: Adding correlation by preserving direction tendency

```fsharp
/// Generate temporally correlated jitter sequence for the entire exposure
let generateCorrelatedJitterSequence (duration: float) (seeing: float) (layers: AtmosphericLayer[]) =
    let subframeCount = int (duration * 10.0) // 0.1s subframes
    let jitterSequence = Array.zeroCreate subframeCount
    
    // For each atmospheric layer
    let layerSequences = 
        layers |> Array.map (fun layer ->
            let sequence = Array.zeroCreate subframeCount
            
            // Initial position and velocity
            let mutable posX, posY = 0.0, 0.0
            let mutable velX, velY = 
                // Initial velocity in layer direction
                let angle = layer.Direction * Math.PI / 180.0
                layer.Speed * Math.Cos(angle), layer.Speed * Math.Sin(angle)
                
            // Add random walk with momentum for this layer
            for i = 0 to subframeCount - 1 do
                // Random acceleration (changes velocity)
                let accX = (random.NextDouble() * 2.0 - 1.0) * layer.DistortionScale / 10.0
                let accY = (random.NextDouble() * 2.0 - 1.0) * layer.DistortionScale / 10.0
                
                // Apply acceleration to velocity (with damping)
                velX <- velX * 0.9 + accX
                velY <- velY * 0.9 + accY
                
                // Apply velocity to position
                posX <- posX + velX * 0.1 // 0.1s time step
                posY <- posY + velY * 0.1
                
                // Store position in sequence
                sequence.[i] <- (posX, posY)
            
            // Scale the sequence based on this layer's contribution to overall seeing
            sequence |> Array.map (fun (x, y) -> 
                          (x * layer.DistortionContribution, 
                           y * layer.DistortionContribution))
        )
    
    // Combine all layer sequences
    for i = 0 to subframeCount - 1 do
        let combinedJitter = 
            layerSequences 
            |> Array.map (fun seq -> seq.[i])
            |> Array.reduce (fun (x1, y1) (x2, y2) -> (x1 + x2, y1 + y2))
            
        jitterSequence.[i] <- combinedJitter
        
    jitterSequence
```

## 3. Quantum Efficiency Modeling

Quantum Efficiency (QE) is the probability that a photon hitting the sensor will be detected and converted to an electron. This is inherently probabilistic.

### Statistical Model

For each pixel in each subframe:

1. Calculate the expected number of photons hitting the pixel
2. Determine how many are actually detected based on QE
3. Account for variations across the sensor (e.g., pixel-to-pixel sensitivity variations)

### QE Factors to Model

Most modern astronomical CMOS and CCD sensors have QE between 50% and 95%, depending on:

1. **Wavelength dependence**: QE varies with the wavelength of incoming light
2. **Temperature**: Generally, lower temperatures improve QE 
3. **Pixel-to-pixel variations**: Manufacturing variations cause QE to vary slightly across the sensor
4. **Sensor aging**: QE can degrade over time

We'll add these parameters to our camera model:

```fsharp
type CameraState = {
    // Existing properties
    QuantumEfficiency: float         // Base QE (0.0-1.0)
    QEVariation: float               // Pixel-to-pixel variation (std dev)
    QEWavelengthResponse: (float * float)[] // Wavelength vs QE curve
    PixelSensitivityMap: float[,]    // Per-pixel sensitivity multiplier
}
```

### Wavelength-Dependent QE

Different stars have different spectral types, which affects how their light is detected:

```fsharp
/// Calculate effective QE based on star color (B-V index)
let calculateEffectiveQE (baseQE: float) (colorIndex: float) =
    // Convert B-V to approximate wavelength
    // B-V of 0.0 is roughly 450nm (blue)
    // B-V of 1.0 is roughly 550nm (yellow)
    // B-V of 2.0 is roughly 650nm (red)
    let peakWavelength = 450.0 + colorIndex * 100.0
    
    // Most CCDs have higher QE in red than blue
    // Most CMOS have higher QE in green 
    // This is a simplified model
    match peakWavelength with
    | w

### Implementation Approach

```fsharp
/// Apply quantum efficiency to a subframe
let applyQuantumEfficiency (subframe: float[,]) (quantumEfficiency: float) =
    let width, height = Array2D.length1 subframe, Array2D.length2 subframe
    let result = Array2D.create width height 0.0
    let random = System.Random()
    
    for x = 0 to width - 1 do
        for y = 0 to height - 1 do
            // Expected photon count in this pixel
            let expectedPhotons = subframe.[x, y]
            
            // Each expected photon has QE probability of being detected
            // For large numbers, we can approximate this with the binomial distribution
            // For small numbers, we should use discrete Poisson probability
            
            let detectedPhotons =
                if expectedPhotons > 100.0 then
                    // For large numbers, normal approximation to binomial is accurate
                    let mean = expectedPhotons * quantumEfficiency
                    let stdDev = Math.Sqrt(expectedPhotons * quantumEfficiency * (1.0 - quantumEfficiency))
                    let normalRandom = random.NextDouble() * 2.0 - 1.0 // Generate in [-1, 1]
                    max 0.0 (mean + normalRandom * stdDev)
                else
                    // For small numbers, use Poisson random
                    let poissonMean = expectedPhotons * quantumEfficiency
                    generatePoissonRandom random poissonMean
                    
            result.[i] <- detectedPhotons
            
    result

/// Type extension to model additional camera parameters
type CameraState with
    member this.QuantumEfficiency = 0.7 // Default 70% QE
    
/// Type for atmospheric layer simulation
type AtmosphericLayer = {
    Height: float            // Height in km
    Direction: float         // Direction in degrees
    Speed: float             // Speed in arcsec/second
    DistortionScale: float   // Characteristic distortion size in arcsec
    DistortionContribution: float // Fraction of total seeing (0-1)
    TimeScale: float         // How quickly pattern evolves (seconds)
}

/// Generate atmospheric layer model based on seeing
let generateAtmosphericLayers (seeing: float) =
    let random = System.Random()
    
    // Three typical layers
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
    
    result
    
/// Generate a Poisson random variable 
let generatePoissonRandom (random: System.Random) (mean: float) =
    if mean <= 0.0 then 0.0
    else
        // For small means, use direct Poisson calculation
        if mean < 30.0 then
            let L = Math.Exp(-mean)
            let mutable k = 0
            let mutable p = 1.0
            
            while p > L do
                k <- k + 1
                p <- p * random.NextDouble()
                
            float (k - 1)
        else
            // For larger means, normal approximation is accurate enough
            let stdDev = Math.Sqrt(mean)
            let normalRandom = random.NextDouble() * 2.0 - 1.0
            max 0.0 (mean + normalRandom * stdDev)

/// Apply jitter to an image by shifting and resampling
let applyJitter (image: float[]) (width: int) (height: int) (jitterX: float) (jitterY: float) =
    // Create output buffer
    let result = Array.zeroCreate (width * height)
    
    // Convert to 2D for easier manipulation
    let image2D = Array2D.init width height (fun x y -> image.[y * width + x])
    let result2D = Array2D.create width height 0.0
    
    // Perform bilinear interpolation to shift image
    for x = 0 to width - 1 do
        for y = 0 to height - 1 do
            // Calculate source position
            let srcX = float x - jitterX
            let srcY = float y - jitterY
            
            // Ensure we're within bounds
            if srcX >= 0.0 && srcX < float width - 1.0 && 
               srcY >= 0.0 && srcY < float height - 1.0 then
                // Get integer and fractional parts
                let x0 = int srcX
                let y0 = int srcY
                let x1 = x0 + 1
                let y1 = y0 + 1
                
                let dx = srcX - float x0
                let dy = srcY - float y0
                
                // Bilinear interpolation
                let value = 
                    image2D.[x0, y0] * (1.0 - dx) * (1.0 - dy) +
                    image2D.[x1, y0] * dx * (1.0 - dy) +
                    image2D.[x0, y1] * (1.0 - dx) * dy +
                    image2D.[x1, y1] * dx * dy
                    
                result2D.[x, y] <- value
    
    // Convert back to 1D array
    for y = 0 to height - 1 do
        for x = 0 to width - 1 do
            result.[y * width + x] <- result2D.[x, y]
            
    result

/// Accumulate a subframe into the main buffer
let accumulateSubframe (buffer: float[,]) (subframe: float[]) =
    let width, height = Array2D.length1 buffer, Array2D.length2 buffer
    
    // Add subframe to main buffer
    for y = 0 to height - 1 do
        for x = 0 to width - 1 do
            buffer.[x, y] <- buffer.[x, y] + subframe.[y * width + x]