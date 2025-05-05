namespace EigenAstroSim.Tests

module VirtualSensorTests =
    open System
    open Xunit
    open FsUnit.Xunit
    open EigenAstroSim.Domain
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

    // Test the star projection functionality
    [<Fact>]
    let ``Star projection should map correctly from RA/Dec to pixel coordinates`` () =
        // Arrange
        let star = createTestStar (Guid.NewGuid()) 0.0 0.0 5.0
        let mount = createTestMountState 0.0 0.0
        let camera = createTestCameraState 100 100
        
        // Act
        let (x, y) = StarProjection.projectStar star mount camera
        
        // Assert
        // A star at exactly the mount's pointing should be in the center of the sensor
        x |> should (equalWithin 0.001) 50.0
        y |> should (equalWithin 0.001) 50.0
        
    [<Fact>]
    let ``Star projection should account for RA/Dec offsets`` () =
        // Arrange
        let star = createTestStar (Guid.NewGuid()) 0.1 0.1 5.0  // Offset by 0.1 degrees
        let mount = createTestMountState 0.0 0.0
        let camera = createTestCameraState 100 100
        
        // Act
        let (x, y) = StarProjection.projectStar star mount camera
        
        // Assert
        // Star should be offset from center
        x |> should be (lessThan 50.0)  // RA increases eastward (negative pixel direction)
        y |> should be (greaterThan 50.0)  // Dec increases northward (positive pixel direction)
        
    [<Fact>]
    let ``isStarVisible should correctly identify stars on sensor`` () =
        // Arrange
        let star1 = createTestStar (Guid.NewGuid()) 0.0 0.0 5.0  // Center
        let star2 = createTestStar (Guid.NewGuid()) 10.0 10.0 5.0  // Far outside
        let mount = createTestMountState 0.0 0.0
        let camera = createTestCameraState 100 100
        
        // Act
        let isVisible1 = StarProjection.isStarVisible star1 mount camera
        let isVisible2 = StarProjection.isStarVisible star2 mount camera
        
        // Assert
        isVisible1 |> should equal true
        isVisible2 |> should equal false
        
    // Test the photon flux calculation
    [<Fact>]
    let ``Photon flux should follow Pogson equation for magnitude differences`` () =
        // Arrange
        let star1 = createTestStar (Guid.NewGuid()) 0.0 0.0 5.0
        let star2 = createTestStar (Guid.NewGuid()) 0.0 0.0 10.0  // 5 magnitudes fainter
        
        let optics = {
            Aperture = 100.0  // 100mm aperture
            Obstruction = 0.0  // No obstruction
            Transmission = 1.0  // Perfect transmission
        }
        
        let exposureTime = 1.0  // 1 second
        
        // Act
        let flux1 = PhotonFlux.calculatePhotonFlux star1 optics exposureTime
        let flux2 = PhotonFlux.calculatePhotonFlux star2 optics exposureTime
        
        // Assert
        // 5 magnitude difference should correspond to 100x flux difference
        let ratio = flux1 / flux2
        ratio |> should (equalWithin 1.0) 100.0
        
    [<Fact>]
    let ``Photon flux should scale linearly with exposure time`` () =
        // Arrange
        let star = createTestStar (Guid.NewGuid()) 0.0 0.0 5.0
        let optics = {
            Aperture = 100.0
            Obstruction = 0.0
            Transmission = 1.0
        }
        
        // Act
        let flux1 = PhotonFlux.calculatePhotonFlux star optics 1.0
        let flux2 = PhotonFlux.calculatePhotonFlux star optics 2.0
        
        // Assert
        // Doubling exposure time should double flux
        let ratio = flux2 / flux1
        ratio |> should (equalWithin 0.001) 2.0
        
    [<Fact>]
    let ``Photon flux should scale with aperture area`` () =
        // Arrange
        let star = createTestStar (Guid.NewGuid()) 0.0 0.0 5.0
        let optics1 = {
            Aperture = 100.0
            Obstruction = 0.0
            Transmission = 1.0
        }
        let optics2 = {
            Aperture = 141.4  // ~2x the area (sqrt(2) * 100)
            Obstruction = 0.0
            Transmission = 1.0
        }
        
        // Act
        let flux1 = PhotonFlux.calculatePhotonFlux star optics1 1.0
        let flux2 = PhotonFlux.calculatePhotonFlux star optics2 1.0
        
        // Assert
        // Doubling aperture area should double flux
        let ratio = flux2 / flux1
        ratio |> should (equalWithin 0.1) 2.0
        
    // Test the PSF generation
    [<Fact>]
    let ``Generated PSF should have unit sum`` () =
        // Arrange
        let fwhmPixels = 3.0
        let size = 15
        
        // Act
        let psf = BufferManagement.generateGaussianPSF fwhmPixels size
        
        // Calculate the sum of the PSF values
        let mutable sum = 0.0
        for x = 0 to size - 1 do
            for y = 0 to size - 1 do
                sum <- sum + psf.[x, y]
        
        // Assert
        // PSF should sum to 1.0 (within floating-point precision)
        sum |> should (equalWithin 0.001) 1.0
        
    [<Fact>]
    let ``PSF should peak at center`` () =
        // Arrange
        let fwhmPixels = 3.0
        let size = 15
        
        // Act
        let psf = BufferManagement.generateGaussianPSF fwhmPixels size
        let center = size / 2
        let centerValue = psf.[center, center]
        
        // Calculate the maximum value in the PSF
        let mutable maxValue = 0.0
        for x = 0 to size - 1 do
            for y = 0 to size - 1 do
                if psf.[x, y] > maxValue then
                    maxValue <- psf.[x, y]
        
        // Assert
        centerValue |> should equal maxValue
        
    // Test photon accumulation
    [<Fact>]
    let ``Accumulating photons should preserve total photon count`` () =
        // Arrange
        let width, height = 100, 100
        let buffer = BufferManagement.createBuffer width height
        let psfSize = 11
        let psf = BufferManagement.generateGaussianPSF 5.0 psfSize
        
        // Total photons to add
        let totalPhotons = 1000.0
        
        // Act
        // Add photons at the center of the buffer
        BufferManagement.accumulatePhotons buffer 50.0 50.0 totalPhotons psf
        
        // Calculate sum of all values in the buffer
        let mutable sum = 0.0
        for x = 0 to width - 1 do
            for y = 0 to height - 1 do
                sum <- sum + buffer.[x, y]
        
        // Assert
        // The total sum should equal the input photon count (within floating-point precision)
        sum |> should (equalWithin 0.001) totalPhotons
        
    // Test subframe processing
    [<Fact>]
    let ``Subframe processor should generate the correct number of subframes`` () =
        // Arrange
        let state = createTestState 100 100
        
        // Case 1: 1-second exposure with 0.1s subframes should yield 10 subframes
        let state1 = { state with Camera = { state.Camera with ExposureTime = 1.0 } }
        let params1 = { Defaults.simulationParameters with SubframeDuration = 0.1 }
        
        // Case 2: 0.05-second exposure with 0.1s subframes should yield 1 subframe
        let state2 = { state with Camera = { state.Camera with ExposureTime = 0.05 } }
        let params2 = { Defaults.simulationParameters with SubframeDuration = 0.1 }
        
        // Act
        let subframes1 = EnhancedSubframeProcessor.generateSubframes state1 params1
        let subframes2 = EnhancedSubframeProcessor.generateSubframes state2 params2
        
        // Assert
        subframes1.Length |> should equal 10
        subframes2.Length |> should equal 1
        
    [<Fact>]
    let ``Virtual sensor processor should produce brighter images for longer exposures`` () =
        // Arrange
        let state = createTestState 100 100
        
        // Create two states with different exposure times
        let shortExpState = { state with Camera = { state.Camera with ExposureTime = 1.0 } }
        let longExpState = { state with Camera = { state.Camera with ExposureTime = 5.0 } }
        
        // Act
        let generator = EnhancedVirtualAstrophotographySensor() :> IImageGenerator
        let shortExpImage = generator.GenerateImage shortExpState
        let longExpImage = generator.GenerateImage longExpState
        
        // Calculate total brightness
        let shortExpBrightness = shortExpImage |> Array.sum
        let longExpBrightness = longExpImage |> Array.sum
        
        // Assert - longer exposure should be approximately 5x brighter
        let ratio = longExpBrightness / shortExpBrightness
        let ratioIsWithinTolerance = Math.Abs(ratio - 5.0) / 5.0 < 0.2
        
        ratioIsWithinTolerance |> should equal true
        
    [<Fact>]
    let ``Virtual sensor should generate image with correct dimensions`` () =
        // Arrange
        let width, height = 200, 150
        let state = createTestState width height
        let generator = EnhancedVirtualAstrophotographySensor() :> IImageGenerator
        
        // Act
        let image = generator.GenerateImage state
        
        // Assert
        image.Length |> should equal (width * height)