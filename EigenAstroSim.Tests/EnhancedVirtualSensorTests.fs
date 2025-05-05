namespace EigenAstroSim.Tests

module EnhancedVirtualSensorTests =
    open System
    open Xunit
    open FsUnit.Xunit
    open EigenAstroSim.Domain
    open EigenAstroSim.Domain.BufferManagement
    open EigenAstroSim.Domain.Types
    open EigenAstroSim.Domain.StarField
    open EigenAstroSim.Domain.StarFieldGenerator
    open EigenAstroSim.Domain.VirtualSensorSimulation

    // Import test helpers
    let createTestStar = ImageGeneratorTests.createTestStar
    let createTestMountState = ImageGeneratorTests.createTestMountState
    let createTestCameraState = ImageGeneratorTests.createTestCameraState
    let createTestRotatorState = ImageGeneratorTests.createTestRotatorState
    let createTestAtmosphericState = ImageGeneratorTests.createTestAtmosphericState
    let createTestState = ImageGeneratorTests.createTestState
    
    // Test the Airy disk PSF generation
    [<Fact>]
    let ``Airy disk PSF should have unit sum`` () =
        // Arrange
        let optics = {
            Aperture = 100.0
            Obstruction = 0.0
            FocalLength = 800.0
            Transmission = 0.85
            OpticalQuality = 0.95
            TelescopeType = Reflector
            FRatio = 8.0
        }
        let wavelength = 550.0  // Green light (550nm)
        let pixelSize = 4.0     // 4 microns
        let size = 31           // Odd number for centered PSF
        
        // Act
        let psf = EnhancedPSF.generateAiryDiskPSF optics wavelength pixelSize size
        
        // Calculate the sum of the PSF values
        let mutable sum = 0.0
        for x = 0 to size - 1 do
            for y = 0 to size - 1 do
                sum <- sum + psf.[x, y]
        
        // Assert
        // PSF should sum to 1.0 (within floating-point precision)
        sum |> should (equalWithin 0.001) 1.0
            
    [<Fact>]
    let ``Airy disk PSF should peak at center`` () =
        // Arrange (keep existing setup)
        let optics = {
            Aperture = 100.0
            Obstruction = 0.0
            FocalLength = 800.0
            Transmission = 0.85
            OpticalQuality = 0.95
            TelescopeType = Reflector
            FRatio = 8.0
        }
        let wavelength = 550.0
        let pixelSize = 4.0
        let size = 31
        
        // Act
        let psf = EnhancedPSF.generateAiryDiskPSF optics wavelength pixelSize size
        let center = size / 2
        
        // Find the maximum value in the PSF
        let mutable maxValue = 0.0
        let mutable maxX = 0
        let mutable maxY = 0
        for x = 0 to size - 1 do
            for y = 0 to size - 1 do
                if psf.[x, y] > maxValue then
                    maxValue <- psf.[x, y]
                    maxX <- x
                    maxY <- y
        
        // Assert - peak should be at or very near center (within 1 pixel)
        abs(maxX - center) |> should be (lessThanOrEqualTo 1)
        abs(maxY - center) |> should be (lessThanOrEqualTo 1)            
    [<Fact>]
    let ``Central obstruction should affect PSF shape`` () =
        // Arrange (keep existing setup)
        let opticsWithoutObstruction = {
            Aperture = 100.0
            Obstruction = 0.0   // No obstruction
            FocalLength = 800.0
            Transmission = 0.85
            OpticalQuality = 0.95
            TelescopeType = Reflector
            FRatio = 8.0
        }
        let opticsWithObstruction = {
            opticsWithoutObstruction with
                Obstruction = 33.0  // 33% obstruction
        }
        let wavelength = 550.0
        let pixelSize = 4.0
        let size = 31
        
        // Act
        let psfWithout = EnhancedPSF.generateAiryDiskPSF opticsWithoutObstruction wavelength pixelSize size
        let psfWith = EnhancedPSF.generateAiryDiskPSF opticsWithObstruction wavelength pixelSize size
        
        // Get center values
        let center = size / 2
        let centerValueWithout = psfWithout.[center, center]
        let centerValueWith = psfWith.[center, center]
        
        // Get first ring values (approximate location)
        let ringRadius = 5  // Estimated first Airy ring location
        let ringValueWithout = psfWithout.[center + ringRadius, center]
        let ringValueWith = psfWith.[center + ringRadius, center]
        
        // Calculate center-to-ring ratios
        let ratioWithout = centerValueWithout / ringValueWithout
        let ratioWith = centerValueWith / ringValueWith
        
        // Assert
        // Central obstruction should decrease the ratio of central peak to first ring
        ratioWith |> should be (lessThan ratioWithout)        
    // Test the atmospheric evolution simulation
    [<Fact>]
    let ``Seeing evolution should stay within realistic bounds`` () =
        // Arrange
        let initialSeeing = 2.0  // 2 arcseconds, typical average seeing
        let timestep = 0.1      // 0.1 seconds (standard subframe duration)
        let totalDuration = 10.0 // 10 seconds total
        
        // Act - simulate seeing evolution for 100 steps
        let mutable currentSeeing = initialSeeing
        let seeingValues = ResizeArray<float>()
        
        for i = 0 to 99 do
            let elapsedTime = float i * timestep
            currentSeeing <- 
                EvolvingConditions.evolveSeeingCondition 
                    currentSeeing timestep elapsedTime totalDuration
            seeingValues.Add(currentSeeing)
        
        // Assert - check statistical properties
        // Values should stay within realistic bounds
        let minSeeing = seeingValues |> Seq.min
        let maxSeeing = seeingValues |> Seq.max
        
        // Seeing should vary but not extremely
        minSeeing |> should be (greaterThan 0.5)  // Never unrealistically good
        maxSeeing |> should be (lessThan 5.0)     // Never unrealistically bad
        
        // Some variation should occur (not constant)
        let variation = maxSeeing - minSeeing
        variation |> should be (greaterThan 0.1)  // Should have at least some variation
        
    [<Fact>]
    let ``Cloud coverage evolution should stay within bounds`` () =
        // Arrange
        let initialCoverage = 0.5  // 50% cloud coverage
        let timestep = 0.1        // 0.1 seconds
        let totalDuration = 10.0   // 10 seconds total
        
        // Act - simulate cloud evolution for 100 steps
        let mutable currentCoverage = initialCoverage
        let coverageValues = ResizeArray<float>()
        
        for i = 0 to 99 do
            let elapsedTime = float i * timestep
            currentCoverage <- 
                EvolvingConditions.evolveCloudCoverage
                    currentCoverage timestep elapsedTime totalDuration
            coverageValues.Add(currentCoverage)
        
        // Assert
        let minCoverage = coverageValues |> Seq.min
        let maxCoverage = coverageValues |> Seq.max
        
        // Coverage should stay within bounds [0, 1]
        minCoverage |> should be (greaterThanOrEqualTo 0.0)
        maxCoverage |> should be (lessThanOrEqualTo 1.0)
        
        // Some variation should occur
        let variation = maxCoverage - minCoverage
        variation |> should be (greaterThan 0.05)  // Should have at least some variation
            
    [<Fact>]
    let ``Buffer pool should reuse buffers`` () =
        // Create instance for test
        use manager = new BufferPoolManager()
        
        // Get a pool
        let pool = manager.GetBufferPool 100 100
        
        // Rest of the test using this instance
        let buffer1 = pool.GetBuffer()
        pool.ReturnBuffer(buffer1)
        let buffer2 = pool.GetBuffer()
        
        // Assertions
        pool.Created |> should equal 1
        pool.Reused |> should equal 1
        Object.ReferenceEquals(buffer1, buffer2) |> should equal true    // Test the enhanced subframe processor
    [<Fact>]
    let ``Enhanced processor should generate correct number of subframes`` () =
        // Create a buffer manager for the test
        use manager = new BufferPoolManager()
        let state = createTestState 100 100
        
        let state1 = { state with Camera = { state.Camera with ExposureTime = 1.0 } }
        let params1 = { Defaults.simulationParameters with SubframeDuration = 0.1 }
        
        // Pass manager to function
        let subframes1 = EnhancedSubframeProcessor2.generateSubframes manager state1 params1
        
        subframes1.Length |> should equal 10        
    [<Fact>]
    let ``Enhanced virtual sensor should produce brighter images for longer exposures`` () =
        // Arrange
        let state = createTestState 100 100
        
        // Create two states with different exposure times
        let shortExpState = { state with Camera = { state.Camera with ExposureTime = 1.0 } }
        let longExpState = { state with Camera = { state.Camera with ExposureTime = 4.0 } }
        
        // Act
        let generator = EnhancedVirtualAstrophotographySensor() :> IImageGenerator
        let shortExpImage = generator.GenerateImage shortExpState
        let longExpImage = generator.GenerateImage longExpState
        
        // Calculate total brightness
        let shortExpBrightness = shortExpImage |> Array.sum
        let longExpBrightness = longExpImage |> Array.sum
        
        // Assert - longer exposure should be approximately 4x brighter
        let ratio = longExpBrightness / shortExpBrightness
        let ratioIsWithinTolerance = Math.Abs(ratio - 4.0) / 4.0 < 0.2
        
        ratioIsWithinTolerance |> should equal true