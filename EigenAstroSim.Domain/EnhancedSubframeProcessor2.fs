namespace EigenAstroSim.Domain.VirtualSensorSimulation

open System
open EigenAstroSim.Domain
open EigenAstroSim.Domain.Types
open EigenAstroSim.Domain.BufferManagement

/// Enhanced implementation of subframe processing with physical accuracy
module EnhancedSubframeProcessor2 =

    /// Accumulate photons from a star onto the buffer with optimized memory access
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
            // Pre-compute the x offset in PSF for this buffer row
            let psfX = bufferX - (int (Math.Floor(pixelX)) - psfRadius)
            
            // Skip if outside PSF bounds
            if psfX >= 0 && psfX < psfSize then
                for bufferY = startY to endY do
                    // Calculate corresponding position in the PSF
                    let psfY = bufferY - (int (Math.Floor(pixelY)) - psfRadius)
                    
                    // Check bounds to avoid array out of bounds
                    if psfY >= 0 && psfY < psfSize then
                        // Get the PSF value and add weighted photons to the buffer
                        let psfValue = psf.[psfX, psfY]
                        buffer.[bufferX, bufferY] <- buffer.[bufferX, bufferY] + photons * psfValue
                        
    /// Add a cloud mask to the buffer
    /// This simulates the effect of clouds on star brightness
    let applyCloudMask 
        (buffer: float[,]) 
        (cloudCoverage: float) 
        (timestamp: float) : unit =
        
        // Skip if no clouds
        if cloudCoverage <= 0.0 then
            ()
        else
            let width = Array2D.length1 buffer
            let height = Array2D.length2 buffer
            
            // Cloud pattern parameters
            let cloudScale = 0.1  // Scale of cloud features
            let cloudSpeed = 0.05  // Speed of cloud movement
            
            // Apply cloud attenuation to each pixel
            for x = 0 to width - 1 do
                for y = 0 to height - 1 do
                    // Generate Perlin-like noise for cloud pattern
                    // This is a simple approximation - real Perlin noise would be better
                    let nx = float x / float width
                    let ny = float y / float height
                    
                    // Add time component for moving clouds
                    let tx = nx + timestamp * cloudSpeed
                    let ty = ny + timestamp * cloudSpeed * 0.7
                    
                    // Generate cloud value (0.0 - 1.0)
                    let cloudValue = 
                        0.5 + 0.5 * Math.Sin(tx * 10.0 * cloudScale) * 
                                Math.Cos(ty * 8.0 * cloudScale) *
                                Math.Sin((tx + ty) * 5.0 * cloudScale)
                    
                    // Scale by cloud coverage
                    let cloudDensity = cloudValue * cloudCoverage
                    
                    // Apply to buffer - attenuate light based on cloud density
                    // Transmission = 1.0 - cloudDensity
                    buffer.[x, y] <- buffer.[x, y] * (1.0 - cloudDensity)
                    
    /// Combine multiple subframe buffers with optimization for memory usage
    let combineBuffers (buffers: float[,][]) : float[,] =
        if Array.isEmpty buffers then
            Array2D.create 0 0 0.0
        else
            let width = Array2D.length1 buffers.[0]
            let height = Array2D.length2 buffers.[0]
            
            // Create result buffer
            let result = Array2D.zeroCreate width height
            
            // Process in chunks for better cache locality
            let chunkSize = 16  // Tune based on CPU cache size
            
            // Process each buffer
            for buffer in buffers do
                // Process in chunks
                let toWidth = width - 1
                let toHeight = height - 1
                for startX in [0..chunkSize..toWidth] do         // Changed syntax here
                    let endX = Math.Min(startX + chunkSize - 1, toWidth)
                    
                    for startY in [0..chunkSize..toHeight] do    // Changed syntax here
                        let endY = Math.Min(startY + chunkSize - 1, toHeight)
                        
                        // Process the chunk
                        for x = startX to endX do
                            for y = startY to endY do
                                result.[x, y] <- result.[x, y] + buffer.[x, y]
                    
            result
            
    /// Apply final image processing to the accumulated buffer
    let applyFinalProcessing (buffer: float[,]) (cameraState: CameraState) : float[,] =
        let width = Array2D.length1 buffer
        let height = Array2D.length2 buffer
        
        // Create result buffer
        let result = Array2D.zeroCreate width height
        
        // Apply sensor effects and scaling
        for x = 0 to width - 1 do
            for y = 0 to height - 1 do
                // Get the accumulated photon count
                let photons = buffer.[x, y]
                
                // Add dark current
                let darkElectrons = cameraState.DarkCurrent * cameraState.ExposureTime
                
                // Add read noise (Gaussian)
                let random = Random()
                let u1 = random.NextDouble()
                let u2 = random.NextDouble()
                let z0 = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2)
                let readNoise = z0 * cameraState.ReadNoise
                
                // Add shot noise (Poisson approximation using Gaussian for large values)
                let shotNoise = 
                    if photons > 10.0 then
                        // Normal approximation for large values
                        let stdDev = Math.Sqrt(photons)
                        random.NextDouble() * stdDev
                    else
                        // Direct Poisson for small values
                        let lambda = photons
                        let mutable k = 0
                        let mutable p = 1.0
                        let l = Math.Exp(-lambda)
                        
                        while p > l do
                            k <- k + 1
                            p <- p * random.NextDouble()
                            
                        float (k - 1) - lambda
                
                // Combine all components
                let totalSignal = photons + darkElectrons + readNoise + shotNoise
                
                // Ensure non-negative values
                result.[x, y] <- Math.Max(0.0, totalSignal)
        
        result
    
    /// Convert 2D buffer to 1D array with efficient memory access patterns
    let bufferToArray (buffer: float[,]) : float[] =
        let width = Array2D.length1 buffer
        let height = Array2D.length2 buffer
        let result = Array.zeroCreate (width * height)
        
        // Process in row-major order for better cache locality
        for y = 0 to height - 1 do
            let rowOffset = y * width
            for x = 0 to width - 1 do
                result.[rowOffset + x] <- buffer.[x, y]
        
        result

    /// Process a single subframe with evolving conditions
    let processSubframe 
        (manager: BufferPoolManager)
        (state: SimulationState) 
        (timeIndex: int)
        (subframeDuration: float) 
        (subframeTimestamp: float)
        (totalExposureTime: float) : Subframe =
        
        // Create evolved mount and atmosphere states for this subframe
        let evolvedMount = 
            EvolvingConditions.createEvolvedMountState 
                state.Mount 
                subframeDuration 
                subframeTimestamp
                
        let evolvedAtmosphere = 
            EvolvingConditions.createEvolvedAtmosphericState 
                state.Atmosphere 
                subframeDuration 
                subframeTimestamp 
                totalExposureTime
                
        // Get enhanced optics information
        let optics = 
            EnhancedPSF.createEnhancedOptics evolvedMount
        
        // Create accumulation buffer for this subframe
        use pooledBuffer = createPooledBuffer manager state.Camera.Width state.Camera.Height                
        let buffer = pooledBuffer.Buffer
                
        // Calculate the plate scale (arcseconds per pixel)
        let plateScale = 
            StarProjection.calculatePlateScale 
                evolvedMount.FocalLength 
                state.Camera.PixelSize
        
        // Calculate atmospheric jitter for this subframe
        let jitter = 
            EvolvingConditions.generateAtmosphericJitter 
                evolvedAtmosphere.SeeingCondition 
                subframeTimestamp
        
        // Get visible stars for this subframe
        let visibleStars = 
            StarProjection.getVisibleStars 
                state.StarField 
                evolvedMount 
                state.Camera
                
        // Process each visible star
        for star in visibleStars do
            // Project star to pixel coordinates
            let (baseX, baseY) = 
                StarProjection.projectStar 
                    star 
                    evolvedMount 
                    state.Camera
            
            // Apply jitter to star position
            let jitterX, jitterY = jitter
            let pixelX = baseX + jitterX / plateScale
            let pixelY = baseY + jitterY / plateScale
            
            // Calculate photon flux for this star and subframe duration
            let photons = 
                PhotonFlux.calculatePhotonFlux 
                    star 
                    (PhotonFlux.createDefaultOptics evolvedMount)
                    subframeDuration
                    
            // Generate appropriate PSF for this star
            // Use optical and atmospheric conditions
            let combinedPSF = 
                EnhancedPSF.generateCombinedPSF
                    optics
                    evolvedAtmosphere.SeeingCondition
                    plateScale
                    star.Color
                    state.Camera.PixelSize
            
            // Accumulate photons onto the buffer
            accumulatePhotons 
                buffer 
                pixelX 
                pixelY 
                photons 
                combinedPSF
                
        // Apply cloud cover if present
        if evolvedAtmosphere.CloudCoverage > 0.0 then
            applyCloudMask
                buffer
                evolvedAtmosphere.CloudCoverage
                subframeTimestamp
                
        // Create copy of buffer for the subframe
        // Need to copy since the pooled buffer will be returned to the pool
        let bufferCopy = Array2D.copy buffer
        
        // Create and return the subframe
        {
            TimeIndex = timeIndex
            Duration = subframeDuration
            Buffer = bufferCopy
            MountState = evolvedMount
            AtmosphereState = evolvedAtmosphere
            Jitter = jitter
            Timestamp = subframeTimestamp
        }
    
    /// Generate a series of subframes for the entire exposure with evolving conditions
    let generateSubframes 
        (manager: BufferPoolManager)
        (state: SimulationState)
        (parameters: SimulationParameters) : Subframe[] =
        // Calculate how many subframes we need for the requested exposure time
        let subframeCount = int (Math.Ceiling(state.Camera.ExposureTime / parameters.SubframeDuration))
        
        // Create an array to hold the subframes
        let subframes = Array.zeroCreate subframeCount
        
        // Generate each subframe with progressive evolution of conditions
        for i = 0 to subframeCount - 1 do
            let timestamp = float i * parameters.SubframeDuration
            
            subframes.[i] <- 
                processSubframe 
                    manager
                    state 
                    i 
                    parameters.SubframeDuration 
                    timestamp
                    state.Camera.ExposureTime
        
        subframes
    
    /// Process all subframes and combine them into a final image buffer
    let processFullExposure 
        (manager: BufferPoolManager)
        (state: SimulationState)
        (parameters: SimulationParameters) : float[,] =
        // Generate all subframes
        let subframes = generateSubframes manager state parameters
        
        // Extract the buffers from subframes
        let buffers = subframes |> Array.map (fun sf -> sf.Buffer)
        
        // Combine all buffers
        let combinedBuffer = combineBuffers buffers
        
        // Apply final processing (sensor effects, noise, etc.)
        applyFinalProcessing combinedBuffer state.Camera
    
    /// Convert the combined buffer to the final image format
    let bufferToImage (buffer: float[,]) : float[] =
        bufferToArray buffer