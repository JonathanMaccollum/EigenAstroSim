namespace EigenAstroSim.Domain

open System
open EigenAstroSim.Domain
open EigenAstroSim.Domain.Types

/// Provides advanced models for sensor physics simulation
module SensorPhysics =
    /// Random number generator
    let private random = new Random()
    /// Sensor types with different characteristics
    type SensorType =
        | CCD               // Traditional CCD sensor
        | CMOS              // Standard CMOS sensor
        | BackIlluminatedCMOS  // Back-illuminated CMOS (highest QE)
    
    /// Generate a random value with Gaussian distribution using Box-Muller transform
    let randomGaussian (mean: float) (sigma: float) =
        let u1 = random.NextDouble()
        let u2 = random.NextDouble()
        
        if u1 <= 0.0 then
            mean // Avoid logarithm of zero
        else
            let z0 = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2)
            mean + sigma * z0
    
    /// Generate a Poisson-distributed random value
    /// More accurate for small lambda values than normal approximation
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
    
    /// Models quantum efficiency based on wavelength
    /// Returns the probability that a photon will be detected
    let getQuantumEfficiency (wavelength: float) (sensorType: SensorType) =
        match sensorType with
        | CCD ->
            // CCDs typically have better QE in red wavelengths
            let peak = 650.0 // Peak QE around 650nm (red)
            let peakQE = 0.85 // 85% peak quantum efficiency
            let width = 150.0 // Width of the QE curve
            
            // Gaussian approximation of QE curve
            peakQE * Math.Exp(-Math.Pow((wavelength - peak) / width, 2.0))
            
        | CMOS ->
            // CMOS typically has peak in green wavelengths
            let peak = 550.0 // Peak QE around 550nm (green)
            let peakQE = 0.75 // 75% peak quantum efficiency
            let width = 180.0 // Wider QE curve than CCD
            
            peakQE * Math.Exp(-Math.Pow((wavelength - peak) / width, 2.0))
            
        | BackIlluminatedCMOS ->
            // Back-illuminated CMOS has excellent QE across spectrum
            let peak = 550.0 // Peak QE around 550nm (green)
            let peakQE = 0.95 // 95% peak quantum efficiency
            let width = 200.0 // Very wide QE curve
            
            peakQE * Math.Exp(-Math.Pow((wavelength - peak) / width, 2.0))
    
    /// Calculate dark current based on temperature
    /// Returns electrons/pixel/second
    let calculateDarkCurrent (baseDarkCurrent: float) (temperature: float) =
        // Dark current typically doubles every 6-7°C
        // Uses 6.5°C as a middle ground
        let tempFactor = Math.Pow(2.0, temperature / 6.5)
        baseDarkCurrent * tempFactor
    
            
    /// Extended sensor model with detailed physical properties
    type EnhancedSensorModel = {
        /// Sensor width in pixels
        Width: int
        
        /// Sensor height in pixels
        Height: int
        
        /// Pixel size in microns
        PixelSize: float
        
        /// Sensor type
        SensorType: SensorType
        
        /// Base dark current at 0°C (electrons/pixel/second)
        BaseDarkCurrent: float
        
        /// Read noise (electrons RMS)
        ReadNoise: float
        
        /// Bias level (ADU)
        BiasLevel: int
        
        /// Full well capacity (electrons)
        FullWellCapacity: int
        
        /// Gain (electrons/ADU)
        Gain: float
        
        /// Bit depth of ADC
        BitDepth: int
        
        /// Operating temperature in °C
        Temperature: float
        
        /// Map of hot pixels (x,y) -> intensity multiplier
        HotPixels: Map<int * int, float>
        
        /// Column amplifier offset variations
        ColumnOffsets: float[]
        
        /// Row pattern noise
        RowPattern: float[]
    }
    
    /// Create a realistic sensor model based on camera state
    let createSensorModel (cameraState: CameraState) =
        let width = cameraState.Width
        let height = cameraState.Height
        
        // Randomly generate hot pixels
        let hotPixelDensity = 0.0001 // 0.01% of pixels are hot
        let hotPixelCount = int (float (width * height) * hotPixelDensity)
        
        let hotPixels =
            [for _ in 1..hotPixelCount do
                let x = random.Next(width)
                let y = random.Next(height)
                let intensity = 1.0 + random.NextDouble() * 9.0 // 1x to 10x normal dark current
                ((x, y), intensity)]
            |> Map.ofList
            
        // Generate column offset pattern (fixed pattern noise)
        let columnOffsets = 
            Array.init width (fun _ -> 
                randomGaussian 0.0 0.5) // Slight column-to-column variation
                
        // Generate row pattern noise
        let rowPattern =
            Array.init height (fun _ ->
                randomGaussian 0.0 0.3) // Slight row-to-row variation
        
        {
            Width = width
            Height = height
            PixelSize = cameraState.PixelSize
            SensorType = BackIlluminatedCMOS // Default to high-end sensor
            BaseDarkCurrent = cameraState.DarkCurrent
            ReadNoise = cameraState.ReadNoise
            BiasLevel = 1000 // Typical bias level for modern cameras
            FullWellCapacity = 50000 // Typical full well for astro cameras
            Gain = 0.5 // Electrons per ADU (unity gain would be 1.0)
            BitDepth = 16 // Most astro cameras use 16-bit ADCs
            Temperature = -15.0 // Typical cooled camera temperature
            HotPixels = hotPixels
            ColumnOffsets = columnOffsets
            RowPattern = rowPattern
        }
        
    /// Apply quantum efficiency to convert photons to electrons
    let applyQuantumEfficiency 
            (pixels: float[,]) 
            (sensorModel: EnhancedSensorModel)
            (wavelength: float) : float[,] =
        
        let width = Array2D.length1 pixels
        let height = Array2D.length2 pixels
        let result = Array2D.zeroCreate width height
        
        // Get QE for this wavelength
        let qe = getQuantumEfficiency wavelength sensorModel.SensorType
        
        // Apply QE to each pixel
        for x = 0 to width - 1 do
            for y = 0 to height - 1 do
                // Each photon has QE probability of being detected
                let photons = pixels.[x, y]
                
                if photons > 0.0 then
                    // Apply QE - photons to electrons conversion is probabilistic
                    let electrons = 
                        if photons > 100.0 then
                            // For large photon counts, use normal approximation
                            photons * qe
                        else
                            // For small counts, use Poisson to model photon statistics
                            randomPoisson (photons * qe)
                
                    result.[x, y] <- electrons
        
        result
        
    /// Apply dark current to the sensor
    let applyDarkCurrent 
            (pixels: float[,]) 
            (sensorModel: EnhancedSensorModel)
            (exposureDuration: float) : float[,] =
        
        let width = Array2D.length1 pixels
        let height = Array2D.length2 pixels
        
        // Calculate effective dark current at operating temperature
        let darkCurrent = 
            calculateDarkCurrent sensorModel.BaseDarkCurrent sensorModel.Temperature
            
        // Expected dark electrons per pixel
        let darkElectrons = darkCurrent * exposureDuration
        
        // Apply dark current to each pixel
        for x = 0 to width - 1 do
            for y = 0 to height - 1 do
                // Check if this is a hot pixel
                let hotPixelMultiplier = 
                    match Map.tryFind (x, y) sensorModel.HotPixels with
                    | Some multiplier -> multiplier
                    | None -> 1.0
                    
                // Hot pixels have higher dark current
                let pixelDarkElectrons = darkElectrons * hotPixelMultiplier
                
                // Add dark electrons - use Poisson distribution for realism
                let darkNoise = randomPoisson pixelDarkElectrons
                
                pixels.[x, y] <- pixels.[x, y] + darkNoise
                
        pixels
        
    /// Apply read noise and fixed pattern noise to the sensor
    let applyReadNoise 
            (pixels: float[,]) 
            (sensorModel: EnhancedSensorModel) : float[,] =
        
        let width = Array2D.length1 pixels
        let height = Array2D.length2 pixels
        
        // Apply read noise to each pixel
        for x = 0 to width - 1 do
            for y = 0 to height - 1 do
                // Read noise is Gaussian
                let noise = randomGaussian 0.0 sensorModel.ReadNoise
                
                // Add column and row fixed pattern noise
                let columnOffset = sensorModel.ColumnOffsets.[x]
                let rowOffset = sensorModel.RowPattern.[y]
                
                // Combine all noise sources
                pixels.[x, y] <- pixels.[x, y] + noise + columnOffset + rowOffset
                
        pixels
        
    /// Apply ADC conversion to convert electrons to ADU
    let applyADC 
            (pixels: float[,]) 
            (sensorModel: EnhancedSensorModel) : int[,] =
        
        let width = Array2D.length1 pixels
        let height = Array2D.length2 pixels
        let result = Array2D.zeroCreate width height
        
        // Maximum ADU value based on bit depth
        let maxADU = (1 <<< sensorModel.BitDepth) - 1
        
        // Apply gain and digitize
        for x = 0 to width - 1 do
            for y = 0 to height - 1 do
                // Add bias level
                let electrons = pixels.[x, y]
                
                // Convert to ADU
                let adu = sensorModel.BiasLevel + int (electrons / sensorModel.Gain)
                
                // Clamp to valid range (saturation)
                let clampedADU = Math.Max(0, Math.Min(maxADU, adu))
                
                result.[x, y] <- clampedADU
                
        result
        
    /// Apply full sensor processing pipeline (electrons to ADU)
    let processElectrons 
            (electrons: float[,]) 
            (sensorModel: EnhancedSensorModel)
            (exposureDuration: float) : int[,] =
        
        let withDark = 
            applyDarkCurrent 
                electrons 
                sensorModel 
                exposureDuration
                
        let withNoise = 
            applyReadNoise 
                withDark 
                sensorModel
                
        let aduValues = 
            applyADC 
                withNoise 
                sensorModel
                
        aduValues
    
    /// Convert ADU values back to normalized float array (0.0-1.0)
    let normalizeADU (aduValues: int[,]) (maxADU: int) : float[,] =
        let width = Array2D.length1 aduValues
        let height = Array2D.length2 aduValues
        let result = Array2D.zeroCreate width height
        
        // Normalize to 0.0-1.0 range
        let scale = 1.0 / float maxADU
        
        for x = 0 to width - 1 do
            for y = 0 to height - 1 do
                result.[x, y] <- float aduValues.[x, y] * scale
                
        result
        
    /// Apply full sensor simulation to photon buffer
    let simulateSensor 
            (photons: float[,]) 
            (cameraState: CameraState)
            (wavelength: float) : float[] =
        
        let width = Array2D.length1 photons
        let height = Array2D.length2 photons
        
        // Create sensor model
        let sensorModel = createSensorModel cameraState
        
        // Apply QE to convert photons to electrons
        let electrons = 
            applyQuantumEfficiency 
                photons 
                sensorModel 
                wavelength
                
        // Process electrons through sensor pipeline
        let aduValues = 
            processElectrons 
                electrons 
                sensorModel 
                cameraState.ExposureTime
                
        // Normalize to 0.0-1.0 range
        let normalized = 
            normalizeADU 
                aduValues 
                ((1 <<< sensorModel.BitDepth) - 1)
                
        // Convert to 1D array in row-major order
        let result = Array.zeroCreate (width * height)
        for y = 0 to height - 1 do
            let rowOffset = y * width
            for x = 0 to width - 1 do
                result.[rowOffset + x] <- normalized.[x, y]
                
        result