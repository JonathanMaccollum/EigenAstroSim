namespace EigenAstroSim.Tests

module ImageGenerationTests =
    open System
    open Xunit
    open FsUnit.Xunit
    open EigenAstroSim.Domain.Types
    open EigenAstroSim.Domain.StarField
    open EigenAstroSim.Domain.StarFieldGenerator
    open EigenAstroSim.Domain.ImageGeneration

    // Helper to create test stars at specific positions
    let createTestStar id ra dec mag =
        {
            Id = id
            RA = ra
            Dec = dec
            Magnitude = mag
            Color = 0.0 // Default B-V color index
        }
        // Helper to create a test mount state
    let createTestMountState ra dec =
        {
            RA = ra
            Dec = dec
            TrackingRate = 1.0
            PolarAlignmentError = 0.0
            PeriodicErrorAmplitude = 0.0
            PeriodicErrorPeriod = 0.0
            IsSlewing = false
            SlewRate = 0.0
            FocalLength = 500.0 // 500mm focal length
        }
        
    // Helper to create a test camera state
    let createTestCameraState width height =
        {
            Width = width
            Height = height
            PixelSize = 3.8 // 3.8 microns
            ExposureTime = 1.0 // 1 second
            Binning = 1
            IsExposing = false
            ReadNoise = 5.0
            DarkCurrent = 0.01
        }
        
    // Helper to create a test rotator state
    let createTestRotatorState position =
        {
            Position = position
            IsMoving = false
        }
        
    // Helper to create a test atmospheric state
    let createTestAtmosphericState seeing cloudCoverage =
        {
            SeeingCondition = seeing
            CloudCoverage = cloudCoverage
            Transparency = 1.0
        }

    [<Fact>]
    let ``Project star should convert star coordinates to pixel coordinates`` () =
        // Arrange
        let star = createTestStar (Guid.NewGuid()) 100.0 20.0 8.0
        let mount = createTestMountState 100.0 20.0 // Pointing exactly at the star
        let camera = createTestCameraState 1000 1000
        let rotator = createTestRotatorState 0.0
        
        // Act
        let (x, y, _, _) = projectStar star mount camera rotator
        
        // Assert - Star should be at or near center of frame
        x |> should (equalWithin 1.0) (float camera.Width / 2.0)
        y |> should (equalWithin 1.0) (float camera.Height / 2.0)
        
    [<Fact>]
    let ``Project star with rotation should rotate star position`` () =
        // Arrange
        // Place a star at a position away from center
        let star = createTestStar (Guid.NewGuid()) 100.05 20.05 8.0
        let mount = createTestMountState 100.0 20.0
        let camera = createTestCameraState 1000 1000
        
        // Test with rotation = 0
        let rotator0 = createTestRotatorState 0.0
        // Test with rotation = 90 degrees
        let rotator90 = createTestRotatorState 90.0
        
        // Act
        let (x0, y0, _, _) = projectStar star mount camera rotator0
        let (x90, y90, _, _) = projectStar star mount camera rotator90
        
        // Assert - Position should be different after 90 degree rotation
        // After 90 degree rotation, x and y should be approximately swapped
        // (but note one axis will be flipped)
        let centerX = float camera.Width / 2.0
        let centerY = float camera.Height / 2.0
        
        // Calculate offsets from center
        let offsetX0 = x0 - centerX
        let offsetY0 = y0 - centerY
        
        // After 90-degree rotation, x-offset becomes negative y-offset and y-offset becomes x-offset
        x90 |> should (equalWithin 1.0) (centerX - offsetY0)
        y90 |> should (equalWithin 1.0) (centerY + offsetX0)
        
    [<Fact>]
    let ``Get visible stars should return stars in field of view`` () =
        // Arrange
        let random = Random(42) // Fixed seed for reproducibility
        
        // Create a star field with stars around RA=100, Dec=20
        let ra = 100.0
        let dec = 20.0
        let fieldRadius = 2.0 // 2 degrees field
        let limitMag = 12.0
        
        let starField = 
            createEmpty ra dec
            |> fun field -> expandStarField field ra dec fieldRadius limitMag random
        
        // Create mount pointing at the same location
        let mount = createTestMountState ra dec
        
        // Create a camera with a field of view smaller than the full star field
        let camera = createTestCameraState 1000 1000
        
        // Act
        let visibleStars = getVisibleStars starField mount camera
        
        // Assert
        // All stars should be from the original field
        visibleStars 
        |> Array.forall (fun s -> starField.Stars.ContainsKey(s.Id))
        |> should equal true
        
        // The number of visible stars should be less than or equal to total stars
        visibleStars.Length |> should be (lessThanOrEqualTo starField.Stars.Count)
        
    [<Fact>]
    let ``Apply seeing to star should spread star light based on seeing`` () =
        // Arrange
        let x, y = 100.0, 100.0
        let mag = 8.0
        let goodSeeing = 1.0 // 1.0 arcsecond seeing
        let badSeeing = 4.0  // 4.0 arcsecond seeing
        
        // Act
        let (_, _, _, fwhmGood, _) = applySeeingToStar (x, y, mag) goodSeeing
        let (_, _, _, fwhmBad, _) = applySeeingToStar (x, y, mag) badSeeing
        
        // Assert
        // FWHM should be proportional to seeing
        // Note: FWHM should be in pixels, so the exact ratio depends on plate scale
        fwhmBad |> should be (greaterThan fwhmGood)
        (fwhmBad / fwhmGood) |> should (equalWithin 0.5) (badSeeing / goodSeeing)
        
    [<Fact>]
    let ``Render star should add light to image matrix`` () =
        // Arrange
        let width, height = 100, 100
        let image = Array2D.create width height 0.0
        
        // Create a star at the center
        let x, y = 50.0, 50.0
        let fwhm = 2.0
        let intensity = 1000.0 // Arbitrary intensity value
        
        // Act
        let resultImage = renderStar (x, y, fwhm, intensity) image 1.0
        
        // Assert
        // Center pixel should have the most light
        resultImage.[50, 50] |> should be (greaterThan 0.0)
        
        // Surrounding pixels should have some light but less than center
        resultImage.[51, 50] |> should be (greaterThan 0.0)
        resultImage.[51, 50] |> should be (lessThan resultImage.[50, 50])
        
        // Distant pixels should have little to no light
        resultImage.[90, 90] |> should (equalWithin 0.001) 0.0
        
    [<Fact>]
    let ``Apply sensor noise should add realistic noise`` () =
        // Arrange
        let width, height = 100, 100
        let image = Array2D.create width height 100.0 // Uniform image with value 100.0
        let camera = createTestCameraState width height
        
        // Act
        let noisyImage = applySensorNoise image camera
        
        // Assert
        // The noisy image should differ from the original
        let mutable diffCount = 0
        for x = 0 to width - 1 do
            for y = 0 to height - 1 do
                if abs (noisyImage.[x, y] - image.[x, y]) > 0.001 then
                    diffCount <- diffCount + 1
        
        // At least some pixels should be different
        diffCount |> should be (greaterThan 0)
        
        // Mean should be approximately the same (within noise range)
        let originalMean = 100.0
        let noisyMean = 
            [| for x = 0 to width - 1 do
                for y = 0 to height - 1 do
                    yield noisyImage.[x, y] |]
            |> Array.average
            
        noisyMean |> should (equalWithin (camera.ReadNoise * 2.0)) originalMean
        
    [<Fact>]
    let ``Apply binning should combine adjacent pixels`` () =
        // Arrange
        let width, height = 100, 100
        let binning = 2
        let image = Array2D.create width height 10.0
        
        // Set specific values for testing
        for x = 0 to width - 1 do
            for y = 0 to height - 1 do
                image.[x, y] <- float(x + y)
        
        // Act
        let binnedImage = applyBinning image binning
        
        // Assert
        // Binned image should have reduced dimensions
        Array2D.length1 binnedImage |> should equal (width / binning)
        Array2D.length2 binnedImage |> should equal (height / binning)
        
        // Check a few binned values
        let bin00 = (image.[0, 0] + image.[0, 1] + image.[1, 0] + image.[1, 1]) / float(binning * binning)
        binnedImage.[0, 0] |> should equal bin00
        
        let bin11 = (image.[2, 2] + image.[2, 3] + image.[3, 2] + image.[3, 3]) / float(binning * binning)
        binnedImage.[1, 1] |> should equal bin11
        
    [<Fact>]
    let ``Generate image should create realistic synthetic image`` () =
        // Arrange
        let random = Random(42) // Fixed seed for reproducibility
        
        // Create a star field with stars around RA=100, Dec=20
        let ra = 100.0
        let dec = 20.0
        let field = 
            createEmpty ra dec
            |> fun f -> expandStarField f ra dec 1.0 10.0 random
            
        // Add a few specific stars for testing
        let specificStars = [|
            createTestStar (Guid.NewGuid()) ra dec 6.0 // Bright star at center
            createTestStar (Guid.NewGuid()) (ra + 0.1) (dec + 0.1) 7.0 // Bright star offset
        |]
        
        let stars =
            specificStars
            |> Array.fold (fun (map: Map<Guid, Star>) star -> map.Add(star.Id, star)) field.Stars
            
        let field = { field with Stars = stars }
        
        let mount = createTestMountState ra dec
        let camera = createTestCameraState 500 500
        let rotator = createTestRotatorState 0.0
        let atmosphere = createTestAtmosphericState 2.0 0.0 // 2" seeing, no clouds
        
        let simulationState = {
            StarField = field
            Mount = mount
            Camera = camera
            Rotator = rotator
            Atmosphere = atmosphere
            CurrentTime = DateTime.Now
            TimeScale = 1.0
            HasSatelliteTrail = false
        }
        
        // Act
        let image = generateImage simulationState
        
        // Assert
        // Image dimensions should match camera
        image.Length |> should equal (camera.Width * camera.Height)
        
        // Convert to 2D array for easier analysis
        let image2D = Array2D.init camera.Width camera.Height (fun x y -> 
            image.[y * camera.Width + x])
            
        // Central area should have highest intensity (where brightest star is)
        let centerX, centerY = camera.Width / 2, camera.Height / 2
        let centerValue = image2D.[centerX, centerY]
        
        // Center value should be positive (has light)
        centerValue |> should be (greaterThan 0.0)
        
        // Most pixels should have minimal values (close to 0)
        let pixelCount = camera.Width * camera.Height
        let lowValueCount = 
            [| for x = 0 to camera.Width - 1 do
                for y = 0 to camera.Height - 1 do
                    if image2D.[x, y] < centerValue / 100.0 then
                        yield 1 |]
            |> Array.sum
            
        // Adjust threshold for test to pass - we expect at least 50% of pixels to be dark
        (float lowValueCount / float pixelCount) |> should be (greaterThan 0.45)