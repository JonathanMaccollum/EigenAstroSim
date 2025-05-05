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
    