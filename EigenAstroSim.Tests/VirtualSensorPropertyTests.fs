namespace EigenAstroSim.Tests

open System
open Xunit
open FsUnit.Xunit
open EigenAstroSim.Domain
open EigenAstroSim.Domain.Types
open EigenAstroSim.Domain.StarField
open EigenAstroSim.Domain.StarFieldGenerator
open EigenAstroSim.Domain.VirtualSensorSimulation

module VirtualSensorPropertyTests =
    // Import test helpers
    let createTestStar = ImageGeneratorTests.createTestStar
    let createTestMountState = ImageGeneratorTests.createTestMountState
    let createTestCameraState = ImageGeneratorTests.createTestCameraState
    let createTestRotatorState = ImageGeneratorTests.createTestRotatorState
    let createTestAtmosphericState = ImageGeneratorTests.createTestAtmosphericState
    let createTestState = ImageGeneratorTests.createTestState
    
    // Helper to generate random values
    let random = Random()
    
    let randomRA() = random.NextDouble() * 360.0
    let randomDec() = random.NextDouble() * 180.0 - 90.0
    let randomMagnitude() = random.NextDouble() * 15.0 + 1.0 // 1.0 to 16.0
    let randomSeeing() = random.NextDouble() * 5.0 + 0.5 // 0.5 to 5.5 arcseconds
    let randomColor() = random.NextDouble() * 2.0 - 0.3 // -0.3 to 1.7 (B-V)
    
    // Helper to create a random simulation state
    let createRandomState width height =
        let ra = randomRA()
        let dec = randomDec()
        
        // Create a starfield with a single bright star at the center
        let starField = createEmpty ra dec
        let centralStar = { 
            Id = Guid.NewGuid()
            RA = ra
            Dec = dec
            Magnitude = randomMagnitude()
            Color = randomColor()
        }
        let stars = Map.empty |> Map.add centralStar.Id centralStar
        
        {
            StarField = { starField with Stars = stars }
            Mount = { 
                createTestMountState ra dec with 
                    FocalLength = 400.0 + random.NextDouble() * 1600.0
            }
            Camera = { 
                createTestCameraState width height with 
                    ExposureTime = 1.0 + random.NextDouble() * 9.0  // 1-10 seconds
                    PixelSize = 3.0 + random.NextDouble() * 6.0     // 3-9 microns
            }
            Rotator = createTestRotatorState (random.NextDouble() * 360.0)
            Atmosphere = createTestAtmosphericState (randomSeeing()) (random.NextDouble())
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
    let ``Property: Star magnitude difference corresponds to flux ratio`` () =
        testProperty 5 (fun () ->
            // Arrange
            let magnitude1 = 5.0
            let magnitude2 = 10.0
            
            let star1 = createTestStar (Guid.NewGuid()) 0.0 0.0 magnitude1
            let star2 = createTestStar (Guid.NewGuid()) 0.0 0.0 magnitude2
            
            let optics = {
                Aperture = 100.0 + random.NextDouble() * 900.0
                Obstruction = random.NextDouble() * 50.0
                Transmission = 0.7 + random.NextDouble() * 0.3
            }
            
            let exposure = 0.1 + random.NextDouble() * 9.9
            
            // Act
            let flux1 = PhotonFlux.calculatePhotonFlux star1 optics exposure
            let flux2 = PhotonFlux.calculatePhotonFlux star2 optics exposure
            
            // Assert: 5 magnitude difference ≈ 100x flux difference
            let expectedRatio = 100.0
            let actualRatio = flux1 / flux2
            
            Math.Abs(actualRatio - expectedRatio) / expectedRatio < 0.15
        )
    
    [<Fact>]
    let ``Property: Better seeing produces sharper star images`` () =
        testProperty 5 (fun () ->
            // Arrange
            let width, height = 100, 100
            
            // Create two states with different seeing conditions
            let state = createTestState width height
            let goodSeeingState = { 
                state with 
                    Atmosphere = { state.Atmosphere with SeeingCondition = 1.0 }
            }
            let badSeeingState = { 
                state with 
                    Atmosphere = { state.Atmosphere with SeeingCondition = 5.0 }
            }
            
            // Act
            let generator = EnhancedVirtualAstrophotographySensor() :> IImageGenerator
            let goodSeeingImage = generator.GenerateImage goodSeeingState
            let badSeeingImage = generator.GenerateImage badSeeingState
            
            // Find peak brightness values
            let goodSeeingPeak = goodSeeingImage |> Array.max
            let badSeeingPeak = badSeeingImage |> Array.max
            
            // Calculate total light (should be similar)
            let goodSeeingTotal = goodSeeingImage |> Array.sum
            let badSeeingTotal = badSeeingImage |> Array.sum
            
            // Assert: Better seeing should have higher peak but similar total
            let peakRatio = goodSeeingPeak / badSeeingPeak
            let totalRatioIsClose = 
                Math.Abs(goodSeeingTotal / badSeeingTotal - 1.0) < 0.2
            
            peakRatio > 2.0 && totalRatioIsClose
        )
    
    [<Fact>]
    let ``Property: Exposure time scales linearly with total light`` () =
        testProperty 5 (fun () ->
            // Arrange
            let width, height = 100, 100
            let state = createTestState width height
            
            // Create states with different exposure times
            let shortExposureState = { 
                state with 
                    Camera = { state.Camera with ExposureTime = 1.0 }
            }
            let longExposureState = { 
                state with 
                    Camera = { state.Camera with ExposureTime = 4.0 }
            }
            
            // Act
            let generator = EnhancedVirtualAstrophotographySensor() :> IImageGenerator
            let shortExposureImage = generator.GenerateImage shortExposureState
            let longExposureImage = generator.GenerateImage longExposureState
            
            // Calculate total brightness
            let shortExposureBrightness = shortExposureImage |> Array.sum
            let longExposureBrightness = longExposureImage |> Array.sum
            
            // Assert: 4x exposure time should give ~4x total light
            let ratio = longExposureBrightness / shortExposureBrightness
            
            Math.Abs(ratio - 4.0) / 4.0 < 0.2
        )