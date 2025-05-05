namespace EigenAstroSim.Tests

module ImageGeneratorPropertyTests =
    open System
    open Xunit
    open FsUnit.Xunit
    open EigenAstroSim.Domain
    open EigenAstroSim.Domain.Types
    open EigenAstroSim.Domain.StarField
    open EigenAstroSim.Domain.StarFieldGenerator
    open EigenAstroSim.Domain.ImageGeneration
    open EigenAstroSim.Domain.VirtualSensorSimulation

    // Import test helpers from ImageGeneratorTests
    let createTestStar = ImageGeneratorTests.createTestStar
    let createTestMountState = ImageGeneratorTests.createTestMountState
    let createTestCameraState = ImageGeneratorTests.createTestCameraState
    let createTestRotatorState = ImageGeneratorTests.createTestRotatorState
    let createTestAtmosphericState = ImageGeneratorTests.createTestAtmosphericState
    
    // Helper to generate random values
    let random = Random()
    
    let randomRA() = random.NextDouble() * 360.0
    let randomDec() = random.NextDouble() * 180.0 - 90.0
    let randomMagnitude() = random.NextDouble() * 15.0 + 1.0 // 1.0 to 16.0
    let randomSeeing() = random.NextDouble() * 5.0 + 0.5 // 0.5 to 5.5 arcseconds
    let randomCloudCover() = random.NextDouble() // 0.0 to 1.0
    let randomExposureTime() = random.NextDouble() * 9.0 + 1.0 // 1.0 to 10.0 seconds
    
    // Helper to create a random simulation state
    let createRandomState width height =
        let ra = randomRA()
        let dec = randomDec()
        
        // Create a starfield with a single bright star at the center
        let starField = createEmpty ra dec
        let centralStar = createTestStar (Guid.NewGuid()) ra dec (randomMagnitude())
        let stars = Map.empty |> Map.add centralStar.Id centralStar
        
        {
            StarField = { starField with Stars = stars }
            Mount = { createTestMountState ra dec with FocalLength = 400.0 + random.NextDouble() * 1600.0 }
            Camera = { createTestCameraState width height with 
                        ExposureTime = randomExposureTime()
                        PixelSize = 3.0 + random.NextDouble() * 6.0 }
            Rotator = ( createTestRotatorState (random.NextDouble() * 360.0) )
            Atmosphere = ( createTestAtmosphericState (randomSeeing()) (randomCloudCover()) )
            CurrentTime = DateTime.Now
            TimeScale = 1.0
            HasSatelliteTrail = false
        }
    
    // Test multiple property instances
    let testProperty iterations property =
        for _ in 1..iterations do
            let result = property()
            result |> should equal true
    
    [<Fact>]
    let ``Image should have correct dimensions for any valid camera size`` () =
        testProperty 5 (fun () ->
            // Arrange
            let simpleGenerator = SimpleImageGenerator() :> IImageGenerator
            let highFidelityGenerator = EnhancedVirtualAstrophotographySensor() :> IImageGenerator
            
            // Create a random state with random dimensions
            let width = 100 + random.Next(500)
            let height = 100 + random.Next(500)
            let state = createRandomState width height
            
            // Act
            let simpleImage = simpleGenerator.GenerateImage state
            let highFidelityImage = highFidelityGenerator.GenerateImage state
            
            // Assert
            let simpleDimensionsCorrect = simpleImage.Length = (state.Camera.Width * state.Camera.Height)
            let highFidelityDimensionsCorrect = highFidelityImage.Length = (state.Camera.Width * state.Camera.Height)
            
            simpleDimensionsCorrect && highFidelityDimensionsCorrect
        )
    
    [<Fact>]
    let ``Longer exposure time should produce brighter images`` () =
        // Arrange
        let generator = EnhancedImageGeneratorFactory.create false // Use simple generator for predictability
        
        // Create a base state with controlled parameters
        let width, height = 300, 300
        let state = createRandomState width height
        
        // Create two states with different exposure times
        let shortExposureState = { state with Camera = { state.Camera with ExposureTime = 1.0 } }
        let longExposureState = { state with Camera = { state.Camera with ExposureTime = 5.0 } }
        
        // Act
        let shortExposureImage = generator.GenerateImage shortExposureState
        let longExposureImage = generator.GenerateImage longExposureState
        
        // Calculate total brightness of each image
        let shortExposureBrightness = shortExposureImage |> Array.sum
        let longExposureBrightness = longExposureImage |> Array.sum
        
        // Assert - longer exposure should be brighter
        let ratio = longExposureBrightness / shortExposureBrightness
        
        // Should be approximately 5x brighter (with some tolerance)
        Math.Abs(ratio - 5.0) / 5.0 < 0.3
    
    [<Fact>]
    let ``Higher cloud coverage should make images dimmer`` () =
        // Arrange
        let generator = EnhancedImageGeneratorFactory.create false // Use simple generator for predictability
        
        // Create a base state with controlled parameters
        let width, height = 300, 300
        let state = createRandomState width height
        
        // Create two states with different cloud coverage
        let clearState = { state with Atmosphere = { state.Atmosphere with CloudCoverage = 0.0 } }
        let cloudyState = { state with Atmosphere = { state.Atmosphere with CloudCoverage = 0.9 } }
        
        // Act
        let clearImage = generator.GenerateImage clearState
        let cloudyImage = generator.GenerateImage cloudyState
        
        // Calculate total brightness of each image
        let clearBrightness = clearImage |> Array.sum
        let cloudyBrightness = cloudyImage |> Array.sum
        
        // Assert - cloudy image should be dimmer
        cloudyBrightness < clearBrightness
    
    [<Fact>]
    let ``Better seeing should produce stars with higher peak brightness`` () =
        // Arrange
        let generator = EnhancedImageGeneratorFactory.create false // Use simple generator for predictability
        
        // Create a base state with controlled parameters
        let width, height = 300, 300
        let state = createRandomState width height
        
        // Create two states with different seeing
        let goodSeeingState = { state with Atmosphere = { state.Atmosphere with SeeingCondition = 1.0 } }
        let badSeeingState = { state with Atmosphere = { state.Atmosphere with SeeingCondition = 5.0 } }
        
        // Act
        let goodSeeingImage = generator.GenerateImage goodSeeingState
        let badSeeingImage = generator.GenerateImage badSeeingState
        
        // Find peak pixel values
        let goodSeeingPeak = goodSeeingImage |> Array.max
        let badSeeingPeak = badSeeingImage |> Array.max
        
        // Assert - good seeing should have higher peak brightness
        goodSeeingPeak > badSeeingPeak
    
    [<Fact>]
    let ``Subframe processor should produce correct number of subframes`` () =
        // Test the subframe generation in different exposure scenarios
        
        // Case 1: 1-second exposure with 0.1s subframes should yield 10 subframes
        let state1 = createRandomState 100 100
        let state1WithExposure = { state1 with Camera = { state1.Camera with ExposureTime = 1.0 } }
        let params1 = { Defaults.simulationParameters with SubframeDuration = 0.1 }
        let subframes1 = SubframeProcessor.generateSubframes state1WithExposure params1
        
        // Case 2: 0.05-second exposure with 0.1s subframes should yield 1 subframe
        let state2 = createRandomState 100 100
        let state2WithExposure = { state2 with Camera = { state2.Camera with ExposureTime = 0.05 } }
        let params2 = { Defaults.simulationParameters with SubframeDuration = 0.1 }
        let subframes2 = SubframeProcessor.generateSubframes state2WithExposure params2
        
        // Case 3: 2.7-second exposure with 0.1s subframes should yield 27 subframes
        let state3 = createRandomState 100 100
        let state3WithExposure = { state3 with Camera = { state3.Camera with ExposureTime = 2.7 } }
        let params3 = { Defaults.simulationParameters with SubframeDuration = 0.1 }
        let subframes3 = SubframeProcessor.generateSubframes state3WithExposure params3
        
        // Case 4: 1-second exposure with 0.2s subframes should yield 5 subframes
        let state4 = createRandomState 100 100
        let state4WithExposure = { state4 with Camera = { state4.Camera with ExposureTime = 1.0 } }
        let params4 = { Defaults.simulationParameters with SubframeDuration = 0.2 }
        let subframes4 = SubframeProcessor.generateSubframes state4WithExposure params4
        
        // Assert all cases
        subframes1.Length = 10 &&
        subframes2.Length = 1 &&
        subframes3.Length = 27 &&
        subframes4.Length = 5
    
    [<Fact>]
    let ``Subframe combiner should preserve total light`` () =
        // Create a few test subframes with known values
        let width, height = 100, 100
        
        // Create three subframes with different light distributions
        let createBuffer value =
            let buffer = Array2D.create width height 0.0
            for x = 0 to width - 1 do
                for y = 0 to height - 1 do
                    buffer.[x, y] <- value
            buffer
            
        let atmosphere = createTestAtmosphericState 1.0 0.0
        let mount = createTestMountState 0.0 0.0
            
        let subframe1 = {
            TimeIndex = 0
            Duration = 0.1
            Buffer = createBuffer 1.0 // All pixels have value 1.0
            MountState = mount
            AtmosphereState = atmosphere
            Jitter = (0.0, 0.0)
            Timestamp = 0.0
        }
        
        let subframe2 = {
            TimeIndex = 1
            Duration = 0.1
            Buffer = createBuffer 2.0 // All pixels have value 2.0
            MountState = mount
            AtmosphereState = atmosphere
            Jitter = (0.0, 0.0)
            Timestamp = 0.1
        }
        
        let subframe3 = {
            TimeIndex = 2
            Duration = 0.1
            Buffer = createBuffer 3.0 // All pixels have value 3.0
            MountState = mount
            AtmosphereState = atmosphere
            Jitter = (0.0, 0.0)
            Timestamp = 0.2
        }
        
        // Expected total light in all subframes
        let expectedTotalLight = (1.0 + 2.0 + 3.0) * float(width * height)
        
        // Combine the subframes
        let combinedImage = SubframeProcessor.combineSubframes [| subframe1; subframe2; subframe3 |]
        
        // Calculate total light in combined image
        let actualTotalLight = combinedImage |> Array.sum
        
        // Assert total light is preserved (within floating-point precision)
        Math.Abs(actualTotalLight - expectedTotalLight) < 0.001