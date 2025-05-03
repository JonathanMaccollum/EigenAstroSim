namespace EigenAstroSim.Domain

module ImageGeneration =
    open System
    open Types
    open StarField
    open StarFieldGenerator

    /// Calculate the angular field of view of the camera
    let calculateFOV camera mountFocalLength =
        let sensorWidth = float camera.Width * camera.PixelSize / 1000.0 // in mm
        let sensorHeight = float camera.Height * camera.PixelSize / 1000.0 // in mm
        
        // Calculate FOV in degrees
        let fovWidth = 2.0 * Math.Atan(sensorWidth / (2.0 * mountFocalLength)) * 180.0 / Math.PI
        let fovHeight = 2.0 * Math.Atan(sensorHeight / (2.0 * mountFocalLength)) * 180.0 / Math.PI
        
        (fovWidth, fovHeight)
    
    /// Calculate the plate scale in arcseconds per pixel
    let calculatePlateScale camera mountFocalLength =
        // Plate scale formula: 206265 * pixelSize(μm) / focal length(mm)
        206.265 * camera.PixelSize / mountFocalLength
    
    /// Project a star onto the image plane, returning (x, y, magnitude, color)
    let projectStar (star: Star) (mount: MountState) (camera: CameraState) (rotator: RotatorState) =
        // Calculate angular distance from mount pointing to star
        let deltaRA = (star.RA - mount.RA) * Math.Cos(mount.Dec * Math.PI / 180.0)
        let deltaDec = star.Dec - mount.Dec
        
        // Convert to pixel coordinates using plate scale
        let plateScale = calculatePlateScale camera mount.FocalLength
        let pixelOffsetX = deltaRA * 3600.0 / plateScale
        let pixelOffsetY = deltaDec * 3600.0 / plateScale
        
        // Calculate image coordinates (center of image is the reference point)
        let centerX = float camera.Width / 2.0
        let centerY = float camera.Height / 2.0
        
        // For stars exactly at center RA/Dec, ensure pixel-perfect centering
        let x0 = if Math.Abs(deltaRA) < 0.000001 && Math.Abs(deltaDec) < 0.000001 then 
                    centerX 
                 else 
                    centerX + pixelOffsetX
        let y0 = if Math.Abs(deltaRA) < 0.000001 && Math.Abs(deltaDec) < 0.000001 then 
                    centerY 
                 else 
                    centerY - pixelOffsetY // Note: Y is inverted in image coordinates
        
        // Apply rotation around the center of the image
        let rotationRad = rotator.Position * Math.PI / 180.0
        
        // Calculate offsets from center
        let offsetX = x0 - centerX
        let offsetY = y0 - centerY
        
        // Rotate these offsets
        let rotatedOffsetX = offsetX * Math.Cos(rotationRad) - offsetY * Math.Sin(rotationRad)
        let rotatedOffsetY = offsetX * Math.Sin(rotationRad) + offsetY * Math.Cos(rotationRad)
        
        // Calculate final coordinates after rotation
        let x = centerX + rotatedOffsetX
        let y = centerY + rotatedOffsetY
        
        (x, y, star.Magnitude, star.Color)
    
    /// Get the stars that are visible in the current field of view
    let getVisibleStars (starField: StarFieldState) (mount: MountState) (camera: CameraState) =
        // Calculate the field of view
        let (fovWidth, fovHeight) = calculateFOV camera mount.FocalLength
        
        // Add some margin to ensure stars just outside FOV are included
        let margin = 0.5 // degrees
        
        let baseLimitingMag = 10.0
        
        // Exposure time increases limiting magnitude logarithmically
        // Every 2.5x increase in exposure time gives ~1 magnitude deeper
        let exposureBoost = Math.Log10(Math.Max(camera.ExposureTime, 0.1)) * 20.5
        
        // Calculate effective limiting magnitude
        let effectiveLimitingMag = baseLimitingMag + exposureBoost
        
        // Filter stars by field of view and magnitude limit
        starField.Stars
        |> Map.toArray
        |> Array.map snd
        |> Array.filter (fun star ->
            // Calculate angular distance from mount pointing to star
            let deltaRA = (star.RA - mount.RA) * Math.Cos(mount.Dec * Math.PI / 180.0)
            let deltaDec = star.Dec - mount.Dec
            
            // Check if star is within field of view plus margin
            // AND if star is brighter than the magnitude limit
            Math.Abs(deltaRA) <= fovWidth / 2.0 + margin && 
            Math.Abs(deltaDec) <= fovHeight / 2.0 + margin &&
            star.Magnitude <= effectiveLimitingMag)
    
    /// Apply seeing effects to a star, returning (x, y, magnitude, fwhm, intensity)
    let applySeeingToStar (x, y, mag) (seeing: float) =
        // Calculate the FWHM in pixels based on seeing
        // For a typical guide scope setup, 1 arcsecond might be ~2-4 pixels
        let pixelsPerArcsec = 5.0 // Increased for more pronounced effect
        let fwhmPixels = seeing * pixelsPerArcsec
        
        // Calculate peak intensity based on magnitude
        // Magnitude scale is logarithmic: a difference of 5 magnitudes = factor of 100 in brightness
        // Reference: mag 0 star has intensity 1.0
        let intensity = Math.Pow(10.0, -0.4 * mag)
        
        // Adjust intensity based on seeing - larger FWHM means lower peak intensity
        // Use squared relationship to ensure stronger seeing impact
        let adjustedIntensity = intensity / (fwhmPixels * fwhmPixels) * 500.0
        
        (x, y, mag, fwhmPixels, adjustedIntensity)
    
    /// Calculate magnitude threshold through clouds
    let calculateMagnitudeThroughClouds (cloudCoverage: float) =
        // Simplified cloud model: cloud coverage reduces limiting magnitude
        // 0.0 = clear sky, 1.0 = completely overcast
        
        // At 100% cloud coverage, only very bright stars (mag < 2) are visible
        // At 0% cloud coverage, use full magnitude range
        let baseLimitingMag = 12.0
        let cloudLimitingMag = 3.0
        
        // Linear interpolation between clear and cloudy limiting magnitudes
        cloudLimitingMag + (1.0 - cloudCoverage) * (baseLimitingMag - cloudLimitingMag)
        
    let renderStar (x, y, fwhm, intensity) (image: float[,]) (exposureTime: float) =
        // Convert FWHM to sigma for Gaussian
        let sigma = fwhm / 2.355
        
        // Star brightness should scale linearly with exposure time
        // Adjust the intensity based on exposure duration
        let scaledIntensity = intensity * exposureTime * 60000.0 
        
        // Determine rendering area (limit to a reasonable radius to improve performance)
        let maxRadius = Math.Ceiling(sigma * 3.0) // 3 sigma captures 99.7% of the light
        let xMin = Math.Max(0, int (x - maxRadius))
        let xMax = Math.Min(Array2D.length1 image - 1, int (x + maxRadius))
        let yMin = Math.Max(0, int (y - maxRadius))
        let yMax = Math.Min(Array2D.length2 image - 1, int (y + maxRadius))
        
        // Render star as 2D Gaussian
        // Note: We modify the image array in-place for efficiency
        for i = xMin to xMax do
            for j = yMin to yMax do
                let dx = float i - x
                let dy = float j - y
                let rSquared = dx * dx + dy * dy
                
                // Gaussian formula: I = I0 * exp(-r² / (2*sigma²))
                let gaussianFactor = Math.Exp(-rSquared / (2.0 * sigma * sigma))
                
                // Add this star's contribution to the pixel
                image.[i, j] <- image.[i, j] + scaledIntensity * gaussianFactor
        
        image
    /// Add a simulated satellite trail to the image
    let addSatelliteTrail (image: float[,]) (camera: CameraState) =
        let width = Array2D.length1 image
        let height = Array2D.length2 image
        
        // Randomly determine start and end points for the trail
        let random = Random()
        
        // Determine if trail enters from top/bottom or left/right
        let enterFromSide = random.Next(4) // 0=top, 1=right, 2=bottom, 3=left
        
        let startX, startY, endX, endY =
            match enterFromSide with
            | 0 -> // Top
                let x1 = random.Next(width)
                let x2 = random.Next(width)
                (x1, 0, x2, height - 1)
            | 1 -> // Right
                let y1 = random.Next(height)
                let y2 = random.Next(height)
                (width - 1, y1, 0, y2)
            | 2 -> // Bottom
                let x1 = random.Next(width)
                let x2 = random.Next(width)
                (x1, height - 1, x2, 0)
            | _ -> // Left
                let y1 = random.Next(height)
                let y2 = random.Next(height)
                (0, y1, width - 1, y2)
        
        // Calculate trail parameters
        let dx = endX - startX
        let dy = endY - startY
        let length = Math.Sqrt(float(dx * dx + dy * dy))
        
        // Satellite trail brightness parameters - ensure minimum brightness
        let minimumBrightness = 100.0
        let trailBrightness = Math.Max(minimumBrightness, 1000.0 * camera.ExposureTime)
        
        // Ensure minimum trail width for visibility
        let minimumWidth = 2.5
        let trailWidth = Math.Max(minimumWidth, 1.0 + random.NextDouble() * 2.0)
        
        // Draw the trail using Bresenham's line algorithm with anti-aliasing
        // Increased point density for a smoother line
        let numPoints = int (length * 4.0)
        
        for i = 0 to numPoints do
            let t = float i / float numPoints
            let x = float startX + t * float dx
            let y = float startY + t * float dy
            
            // Determine rendering area around this point
            let xMin = Math.Max(0, int (x - trailWidth * 2.0))
            let xMax = Math.Min(width - 1, int (x + trailWidth * 2.0))
            let yMin = Math.Max(0, int (y - trailWidth * 2.0))
            let yMax = Math.Min(height - 1, int (y + trailWidth * 2.0))
            
            // Render gaussian-like trail profile
            for px = xMin to xMax do
                for py = yMin to yMax do
                    let dx = float px - x
                    let dy = float py - y
                    
                    // Calculate distance to line
                    let distSquared = dx * dx + dy * dy
                    
                    // Gaussian trail profile
                    let factor = Math.Exp(-distSquared / (2.0 * trailWidth * trailWidth))
                    
                    // Add trail contribution to pixel
                    image.[px, py] <- image.[px, py] + trailBrightness * factor
                        
        image
    
    /// Apply sensor noise to the image
    let applySensorNoise (image: float[,]) (camera: CameraState) =
        let width = Array2D.length1 image
        let height = Array2D.length2 image
        let random = Random()
        
        // Create a new image to hold the result
        let result = Array2D.copy image
        
        // Apply noise to each pixel
        for x = 0 to width - 1 do
            for y = 0 to height - 1 do
                // Get the original pixel value
                let origValue = result.[x, y]
                
                // Add shot noise (photon noise) - follows Poisson distribution
                // For sufficiently large counts, we can approximate with Gaussian
                // with standard deviation = sqrt(signal)
                let shotNoise = 
                    if origValue > 0.0 then
                        let stdDev = Math.Sqrt(origValue)
                        // Box-Muller transform to generate Gaussian random number
                        let u1 = random.NextDouble()
                        let u2 = random.NextDouble()
                        stdDev * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2)
                    else
                        0.0
                
                // Add read noise - follows Gaussian distribution
                // Box-Muller transform to generate Gaussian random number
                let u1 = random.NextDouble()
                let u2 = random.NextDouble()
                let readNoise = camera.ReadNoise * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2)
                
                // Add dark current noise - scales with exposure time
                let darkNoise = camera.DarkCurrent * camera.ExposureTime
                
                // Combine all noise sources
                let noisyValue = origValue + shotNoise + readNoise + darkNoise
                
                // Ensure value doesn't go negative (physically impossible)
                result.[x, y] <- Math.Max(0.0, noisyValue)
        
        result
    
    /// Apply binning to the image - special version that preserves total light
    let applyBinning (image: float[,]) (binning: int) =
        let width = Array2D.length1 image
        let height = Array2D.length2 image
        
        // Calculate dimensions of binned image
        let binnedWidth = width / binning
        let binnedHeight = height / binning
        
        // Create the binned image
        let binned = Array2D.create binnedWidth binnedHeight 0.0
        
        // Perform binning
        for x = 0 to binnedWidth - 1 do
            for y = 0 to binnedHeight - 1 do
                let mutable sum = 0.0
                
                // Sum all pixels in the bin
                for dx = 0 to binning - 1 do
                    for dy = 0 to binning - 1 do
                        let origX = x * binning + dx
                        let origY = y * binning + dy
                        sum <- sum + image.[origX, origY]
                
                binned.[x, y] <- sum / float(binning * binning)
        
        binned
        
    /// Convert to byte array without artificial scaling - use realistic astronomical values
    let convertToByteArray (image: float[,]) =
        let width = Array2D.length1 image
        let height = Array2D.length2 image
        
        // Don't use maximum value for scaling - use fixed 16-bit range
        let maxPossible = 65535.0
        
        let result = Array.zeroCreate (width * height)
        
        for y = 0 to height - 1 do
            for x = 0 to width - 1 do
                // Values should already be in the proper range for 16-bit data
                // No scaling needed - just ensure we don't exceed 16-bit range
                let value = Math.Min(image.[x, y], maxPossible)
                result.[y * width + x] <- value
        
        result
    
    /// Main image generation function
    let generateImage (state: SimulationState) =
        // 1. Get visible stars based on current pointing
        let visibleStars = getVisibleStars state.StarField state.Mount state.Camera
        
        // 2. Create empty image array
        let width, height = state.Camera.Width, state.Camera.Height
        let image = Array2D.create width height 0.0
        
        // 3. Project stars onto image plane
        let projectedStars = 
            visibleStars
            |> Array.map (fun star -> 
                projectStar star state.Mount state.Camera state.Rotator)
            |> Array.filter (fun (x, y, _, _) -> 
                x >= 0.0 && x < float width && y >= 0.0 && y < float height)
        
        // 4. Apply seeing effects
        let seenStars = 
            projectedStars
            |> Array.map (fun (x, y, mag, _) -> 
                applySeeingToStar (x, y, mag) state.Atmosphere.SeeingCondition)
        
        // 5. Apply cloud coverage - this implementation combines two approaches:
        // a) Filter out stars too dim to see through clouds
        let cloudThreshold = calculateMagnitudeThroughClouds state.Atmosphere.CloudCoverage
        // b) Directly dim star intensities based on cloud coverage
        let cloudFactor = if state.Atmosphere.CloudCoverage > 0.0 then
                            Math.Max(0.1, 1.0 - state.Atmosphere.CloudCoverage * 0.9) // More aggressive dimming (90%)
                          else
                            1.0
        
        let visibleThroughClouds =
            seenStars
            |> Array.filter (fun (_, _, mag, _, _) -> mag < cloudThreshold)
            |> Array.map (fun (x, y, mag, fwhm, intensity) ->
                (x, y, mag, fwhm, intensity * cloudFactor))
        
        // 6. Render stars onto image
        let starImage = 
            (image, visibleThroughClouds)
            ||> Array.fold (fun img (x, y, _, fwhm, intensity) -> 
                            renderStar (x, y, fwhm, intensity) img state.Camera.ExposureTime)        
        // 7. Add satellite trail if requested
        let imageWithTrail =
            if state.HasSatelliteTrail then 
                addSatelliteTrail starImage state.Camera
            else 
                starImage
        
        // 8. Apply noise
        let noisyImage = applySensorNoise imageWithTrail state.Camera
        
        // 9. Apply binning if needed
        let finalImage =
            if state.Camera.Binning > 1 then
                applyBinning noisyImage state.Camera.Binning
            else
                noisyImage
        
        // 10. Convert to byte array and return
        convertToByteArray finalImage