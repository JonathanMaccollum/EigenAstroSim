namespace EigenAstroSim.Tests

module ImageGeneratorTests =
    open System
    open Xunit
    open FsUnit.Xunit
    open EigenAstroSim.Domain
    open EigenAstroSim.Domain.Types
    open EigenAstroSim.Domain.StarField
    open EigenAstroSim.Domain.StarFieldGenerator
    open EigenAstroSim.Domain.ImageGeneration
    open EigenAstroSim.Domain.VirtualSensorSimulation

    // Import test helpers from existing tests
    // These helper functions create test objects with controlled parameters
    let createTestStar id ra dec mag =
        {
            Id = id
            RA = ra
            Dec = dec
            Magnitude = mag
            Color = 0.0 // Default B-V color index
        }
    
    let createTestMountState ra dec =
        {
            RA = ra
            Dec = dec
            TrackingRate = 1.0
            PolarAlignmentError = 0.0
            PeriodicErrorAmplitude = 0.0
            PeriodicErrorPeriod = 0.0
            PeriodicErrorHarmonics = []
            IsSlewing = false
            SlewRate = 0.0
            FocalLength = 500.0 // 500mm focal length
        }
        
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
        
    let createTestRotatorState position =
        {
            Position = position
            IsMoving = false
        }
        
    let createTestAtmosphericState seeing cloudCoverage =
        {
            SeeingCondition = seeing
            CloudCoverage = cloudCoverage
            Transparency = 1.0
        }
    
    // Helper to create a test state with a single star at the center
    let createTestState width height =
        let ra, dec = 0.0, 0.0
        let starField = createEmpty ra dec
        
        // Add a bright star at the center
        let brightStar = createTestStar (Guid.NewGuid()) ra dec 5.0
        let stars = Map.empty |> Map.add brightStar.Id brightStar
        
        {
            StarField = { starField with Stars = stars }
            Mount = createTestMountState ra dec
            Camera = createTestCameraState width height
            Rotator = createTestRotatorState 0.0
            Atmosphere = createTestAtmosphericState 1.0 0.0
            CurrentTime = DateTime.Now
            TimeScale = 1.0
            HasSatelliteTrail = false
        }
    
    [<Fact>]
    let ``Factory should create the correct implementation`` () =
        // Arrange & Act
        let simpleGenerator = EnhancedImageGeneratorFactory.create false
        let highFidelityGenerator = EnhancedImageGeneratorFactory.create true
        
        // Assert
        simpleGenerator.ImplementationName |> should equal "Simple Image Generator"
        highFidelityGenerator.ImplementationName |> should equal "Enhanced Virtual Astrophotography Sensor"
        
        // Verify capabilities match expectations
        simpleGenerator.Capabilities.FidelityLevel |> should be (lessThan highFidelityGenerator.Capabilities.FidelityLevel)
        simpleGenerator.Capabilities.SupportsRealTimePreview |> should equal true
        highFidelityGenerator.Capabilities.UsesSubframes |> should equal true
    
    [<Fact>]
    let ``SimpleImageGenerator should produce valid images`` () =
        // Arrange
        let generator = SimpleImageGenerator() :> IImageGenerator
        let state = createTestState 100 100
        
        // Act
        let image = generator.GenerateImage state
        
        // Assert
        image.Length |> should equal (state.Camera.Width * state.Camera.Height)
        
        // At least some pixels should have non-zero values (the star should be visible)
        image |> Array.exists (fun value -> value > 0.0) |> should equal true
    