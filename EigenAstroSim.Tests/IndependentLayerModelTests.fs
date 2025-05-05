namespace EigenAstroSim.Tests

open System
open Xunit
open EigenAstroSim.Domain
open EigenAstroSim.Domain.Types
open EigenAstroSim.Domain.VirtualSensorSimulation.Components
open EigenAstroSim.Domain.VirtualSensorSimulation.Components.DomainTypes
open EigenAstroSim.Domain.VirtualSensorSimulation.Components.StarProjection
open EigenAstroSim.Domain.VirtualSensorSimulation.Components.PhotonGeneration
open EigenAstroSim.Domain.VirtualSensorSimulation.Components.PsfGeneration
open EigenAstroSim.Domain.VirtualSensorSimulation.Components.AtmosphericEvolution
open EigenAstroSim.Domain.VirtualSensorSimulation.Components.MountTracking
open EigenAstroSim.Domain.VirtualSensorSimulation.Components.SensorPhysics
open EigenAstroSim.Domain.VirtualSensorSimulation.Components.BufferOperations

/// Test utilities
module TestUtils =
    /// Create a test star
    let createTestStar ra dec magnitude color =
        {
            Id = Guid.NewGuid()
            RA = ra
            Dec = dec
            Magnitude = magnitude
            Color = color
        }
        
    /// Create a test mount state
    let createTestMountState ra dec =
        {
            RA = ra
            Dec = dec
            FocalLength = 1000.0
            TrackingRate = 15.0 / 3600.0 // sidereal rate in deg/sec
            PeriodicErrorAmplitude = 5.0
            PeriodicErrorPeriod = 600.0
            PolarAlignmentError = 0.5
            PeriodicErrorHarmonics = []
            IsSlewing = false
            SlewRate = 3.0 // degrees per second
        }
        
    /// Create a test camera state
    let createTestCameraState width height =
        {
            Width = width
            Height = height
            PixelSize = 3.8
            ExposureTime = 30.0
            ReadNoise = 2.0
            DarkCurrent = 0.01
            Binning = 1
            IsExposing = false
        }
        
    /// Create a test atmospheric state
    let createTestAtmosphericState seeingCondition =
        {
            SeeingCondition = seeingCondition
            CloudCoverage = 0.0
            Transparency = 1.0
        }
        
    /// Approximately equal for floating point
    let approxEqual (precision:float) (expected:float) (actual:float) =
        Math.Abs(expected - actual) < precision

/// Tests for the StarProjection module
module StarProjectionTests =
    open TestUtils
    
    [<Fact>]
    let ``toRadians converts degrees to radians correctly``() =
        // Arrange
        let degrees = 180.0
        
        // Act
        let radians = toRadians degrees
        
        // Assert
        Assert.Equal(Math.PI, radians, 6)
    
    [<Fact>]
    let ``calculatePlateScale returns correct value``() =
        // Arrange
        let focalLength = 1000.0 // mm
        let pixelSize = 3.8 // microns
        
        // Act
        let plateScale = calculatePlateScale focalLength pixelSize
        
        // Assert
        // Plate scale should be approximately 0.784 arcsec/pixel
        let expectedPlateScale = 206.265 * pixelSize / focalLength
        Assert.Equal(expectedPlateScale, plateScale, 6)
    
    [<Fact>]
    let ``projectStar maps star at center of field correctly``() =
        // Arrange
        let star = createTestStar 100.0 20.0 5.0 0.5
        let mountState = createTestMountState 100.0 20.0 // Mount pointing at star
        let cameraState = createTestCameraState 4096 2160
        
        // Act
        let pixelStar = projectStar star mountState cameraState
        
        // Assert
        // Star should project to center of sensor
        let expectedX = float cameraState.Width / 2.0
        let expectedY = float cameraState.Height / 2.0
        Assert.True(approxEqual 0.1 expectedX pixelStar.X)
        Assert.True(approxEqual 0.1 expectedY pixelStar.Y)
        Assert.Equal(star.Magnitude, pixelStar.Star.Magnitude)
        Assert.Equal(star.Color, pixelStar.Star.Color)
        Assert.Equal(None, pixelStar.PhotonFlux)
    
    [<Fact>]
    let ``projectStar maps star offset from center correctly``() =
        // Arrange
        // Star offset by 0.5 degrees in RA and Dec
        let star = createTestStar 100.5 20.5 5.0 0.5
        let mountState = createTestMountState 100.0 20.0
        let cameraState = createTestCameraState 4096 2160
        
        // Act
        let pixelStar = projectStar star mountState cameraState
        
        // Assert
        // Calculate expected position
        // 0.5 degree = 1800 arcseconds
        let plateScale = calculatePlateScale mountState.FocalLength cameraState.PixelSize
        let deltaRAPixels = -1800.0 * Math.Cos(toRadians mountState.Dec) / plateScale
        let deltaDecPixels = 1800.0 / plateScale
        let expectedX = (float cameraState.Width / 2.0) + deltaRAPixels
        let expectedY = (float cameraState.Height / 2.0) + deltaDecPixels
        
        Assert.True(approxEqual 1.0 expectedX pixelStar.X)
        Assert.True(approxEqual 1.0 expectedY pixelStar.Y)
    
    [<Fact>]
    let ``isStarVisible returns true for star in field``() =
        // Arrange
        let pixelStar = {
            Star = createTestStar 100.0 20.0 5.0 0.5
            X = 2000.0
            Y = 1080.0
            PhotonFlux = None
        }
        let width = 4096
        let height = 2160
        
        // Act
        let isVisible = isStarVisible pixelStar width height
        
        // Assert
        Assert.True(isVisible)
    
    [<Fact>]
    let ``isStarVisible returns false for star outside field``() =
        // Arrange
        let pixelStar = {
            Star = createTestStar 100.0 20.0 5.0 0.5
            X = 5000.0 // Beyond width
            Y = 1080.0
            PhotonFlux = None
        }
        let width = 4096
        let height = 2160
        
        // Act
        let isVisible = isStarVisible pixelStar width height
        
        // Assert
        Assert.False(isVisible)
    
    [<Fact>]
    let ``isStarVisible returns true for star just outside field within margin``() =
        // Arrange
        let pixelStar = {
            Star = createTestStar 100.0 20.0 5.0 0.5
            X = -10.0 // Negative but within margin
            Y = 1080.0
            PhotonFlux = None
        }
        let width = 4096
        let height = 2160
        
        // Act
        let isVisible = isStarVisible pixelStar width height
        
        // Assert
        Assert.True(isVisible) // Should be visible due to margin
    
    // Additional test would be for getVisibleStars, but it requires a StarFieldState

/// Tests for the PhotonGeneration module
module PhotonGenerationTests =
    open TestUtils
    
    [<Fact>]
    let ``colorIndexToWavelength maps B-V indices to appropriate wavelengths``() =
        // Arrange
        let blueIndex = -0.3  // Hot blue star
        let solarIndex = 0.6  // Sun-like star
        let redIndex = 1.5    // Cool red star
        
        // Act
        let blueWavelength = colorIndexToWavelength blueIndex
        let solarWavelength = colorIndexToWavelength solarIndex
        let redWavelength = colorIndexToWavelength redIndex
        
        // Assert - using the expected values from the formula
        Assert.True(approxEqual 1.0 450.0 blueWavelength)  // Blue around 450nm
        Assert.True(approxEqual 1.0 540.0 solarWavelength) // Solar around 540nm (was 550nm)
        Assert.True(approxEqual 1.0 630.0 redWavelength)   // Red around 630nm (was 650nm)
    
    [<Fact>]
    let ``apertureArea calculates correct area with no obstruction``() =
        // Arrange
        let aperture = 200.0  // 200mm aperture
        let obstruction = 0.0 // No obstruction
        
        // Act
        let area = apertureArea aperture obstruction
        
        // Assert
        let expectedArea = Math.PI * (aperture / 2.0) * (aperture / 2.0) / 1000000.0 // in m²
        Assert.Equal(expectedArea, area, 6)
    
    [<Fact>]
    let ``apertureArea calculates correct area with central obstruction``() =
        // Arrange
        let aperture = 200.0   // 200mm aperture
        let obstruction = 60.0 // 60mm central obstruction
        
        // Act
        let area = apertureArea aperture obstruction
        
        // Assert
        let apertureRadius = aperture / 2.0
        let obstructionRadius = obstruction / 2.0
        let expectedArea = Math.PI * (apertureRadius * apertureRadius - obstructionRadius * obstructionRadius) / 1000000.0 // in m²
        Assert.Equal(expectedArea, area, 6)
    
    [<Fact>]
    let ``createOpticalParameters generates reasonable parameters``() =
        // Arrange
        let mountState = createTestMountState 100.0 20.0
        
        // Act
        let optics = createOpticalParameters mountState
        
        // Assert
        Assert.True(optics.Aperture > 0.0)
        Assert.True(optics.Obstruction >= 0.0)
        Assert.Equal(mountState.FocalLength, optics.FocalLength)
        Assert.True(optics.Transmission > 0.0 && optics.Transmission <= 1.0)
        Assert.True(optics.OpticalQuality > 0.0 && optics.OpticalQuality <= 1.0)
        Assert.Equal(optics.FocalLength / optics.Aperture, optics.FRatio)
    
    [<Fact>]
    let ``calculatePhotonFlux scales with magnitude``() =
        // Arrange
        let brightStar = {
            Star = createTestStar 100.0 20.0 2.0 0.5
            X = 2000.0
            Y = 1080.0
            PhotonFlux = None
        }
        
        let dimStar = {
            Star = createTestStar 100.0 20.0 7.0 0.5 // 5 magnitudes dimmer
            X = 2000.0
            Y = 1080.0
            PhotonFlux = None
        }
        
        let optics = {
            Aperture = 200.0
            Obstruction = 60.0
            FocalLength = 1000.0
            Transmission = 0.8
            OpticalQuality = 0.9
            FRatio = 5.0
        }
        
        let exposureTime = 60.0 // 1 minute
        
        // Act
        let brightStarWithFlux = calculatePhotonFlux brightStar optics exposureTime
        let dimStarWithFlux = calculatePhotonFlux dimStar optics exposureTime
        
        // Assert
        Assert.True(brightStarWithFlux.PhotonFlux.IsSome)
        Assert.True(dimStarWithFlux.PhotonFlux.IsSome)
        
        // 5 magnitude difference should be about 100x flux difference
        let brightFlux = brightStarWithFlux.PhotonFlux.Value
        let dimFlux = dimStarWithFlux.PhotonFlux.Value
        let ratio = brightFlux / dimFlux
        Assert.True(approxEqual 10.0 100.0 ratio)

/// Tests for the PsfGeneration module
module PsfGenerationTests =
    open TestUtils
    open EigenAstroSim.Bessel
    
    [<Fact>]
    let ``besselJ1 approximation is accurate for common values``() =
        // Test cases: [(x, expected J1(x))]
        let testCases = [
            (0.0, 0.0)        // Special case at x=0
            (1.0, 0.44005058) // Standard case
            (3.83171, 0.0)    // First zero of J1
        ]
        
        for (x, expected) in testCases do
            // Act
            let result = besselJ1 x
            
            // Calculate difference
            let diff = Math.Abs(expected - result)
            let precision = 0.01
            
            // Assert with detailed output
            Assert.True(
                diff < precision,
                sprintf "BesselJ1(%f) = %f, expected %f, diff = %f, precision = %f" 
                    x result expected diff precision)
    
    [<Fact>]
    let ``generateAiryDiskPSF creates normalized PSF``() =
        // Arrange
        let optics = {
            Aperture = 200.0
            Obstruction = 0.0
            FocalLength = 1000.0
            Transmission = 0.8
            OpticalQuality = 0.9
            FRatio = 5.0
        }
        let wavelength = 550.0 // nm
        let pixelSize = 3.8    // microns
        let size = 21          // pixels
        
        // Act
        let psf = generateAiryDiskPSF optics wavelength pixelSize size
        
        // Assert
        Assert.Equal(size, psf.Size)
        Assert.True(approxEqual 0.01 1.0 psf.Sum) // PSF should be normalized
        
        // Check that center has highest value
        let centerValue = psf.Values.[size/2, size/2]
        for x = 0 to size - 1 do
            for y = 0 to size - 1 do
                Assert.True(psf.Values.[x, y] <= centerValue)
    
    [<Fact>]
    let ``generateGaussianPSF creates normalized PSF``() =
        // Arrange
        let fwhmPixels = 5.0 // 5 pixel FWHM
        let size = 21        // pixels
        
        // Act
        let psf = generateGaussianPSF fwhmPixels size
        
        // Assert
        Assert.Equal(size, psf.Size)
        Assert.True(approxEqual 0.01 1.0 psf.Sum) // PSF should be normalized
        
        // Check that center has highest value
        let centerValue = psf.Values.[size/2, size/2]
        for x = 0 to size - 1 do
            for y = 0 to size - 1 do
                Assert.True(psf.Values.[x, y] <= centerValue)
    
    [<Fact>]
    let ``convolvePSFs preserves normalization``() =
        // Arrange
        let fwhm1 = 3.0
        let fwhm2 = 4.0
        let size = 21
        
        let psf1 = generateGaussianPSF fwhm1 size
        let psf2 = generateGaussianPSF fwhm2 size
        
        // Act
        let combinedPsf = convolvePSFs psf1 psf2
        
        // Assert
        Assert.True(approxEqual 0.01 1.0 combinedPsf.Sum) // Combined PSF should be normalized
    
    [<Fact>]
    let ``calculatePsfSize returns appropriate size for seeing conditions``() =
        // Arrange
        let seeingFwhm = 2.0   // 2 arcsec seeing
        let plateScale = 0.5   // 0.5 arcsec/pixel
        
        // Act
        let psfSize = calculatePsfSize seeingFwhm plateScale
        
        // Assert
        // Should be at least 5 times the FWHM in pixels and odd
        let fwhmPixels = seeingFwhm / plateScale
        let minimumSize = int (Math.Ceiling(fwhmPixels * 5.0))
        let expectedSize = if minimumSize % 2 = 0 then minimumSize + 1 else minimumSize
        
        Assert.Equal(expectedSize, psfSize)
        Assert.True(psfSize % 2 = 1) // Should be odd

/// Tests for the AtmosphericEvolution module
module AtmosphericEvolutionTests =
    open TestUtils
    
    [<Fact>]
    let ``generateAtmosphericLayers creates appropriate number of layers``() =
        // Arrange
        let seeing = 2.0 // 2 arcsec seeing
        
        // Act
        let layers = generateAtmosphericLayers seeing
        
        // Assert
        Assert.Equal(3, layers.Length) // Should create 3 layers
        
        // Check layer properties
        for layer in layers do
            Assert.True(layer.Height >= 0.0)
            Assert.True(layer.Direction >= 0.0 && layer.Direction < 360.0)
            Assert.True(layer.Speed > 0.0)
            Assert.True(layer.Strength > 0.0 && layer.Strength < 1.0)
            Assert.True(layer.TimeScale > 0.0)
        
        // Check total strength = 1.0
        let totalStrength = layers |> List.sumBy (fun l -> l.Strength)
        Assert.True(approxEqual 0.01 1.0 totalStrength)
    
    [<Fact>]
    let ``calculateJitter returns reasonable values``() =
        // Arrange
        let seeing = 2.0 // 2 arcsec seeing
        let layers = generateAtmosphericLayers seeing
        let timestamp = 10.0 // 10 seconds into simulation
        
        // Act
        let jitter = calculateJitter layers seeing timestamp
        
        // Assert
        // Jitter should be within seeing range
        Assert.True(Math.Abs(jitter.X) <= seeing)
        Assert.True(Math.Abs(jitter.Y) <= seeing)
    
    [<Fact>]
    let ``evolveSeeingCondition maintains reasonable values``() =
        // Arrange
        let initialSeeing = 2.0 // 2 arcsec seeing
        let elapsedTime = 60.0  // 1 minute
        let timestep = 0.1      // 0.1 second
        
        // Act
        let newSeeing = evolveSeeingCondition initialSeeing elapsedTime timestep
        
        // Assert
        // New seeing should be within reasonable range of initial
        Assert.True(newSeeing >= 0.5 && newSeeing <= 5.0)
        // Shouldn't change dramatically in short time
        Assert.True(Math.Abs(newSeeing - initialSeeing) < initialSeeing)
    
    [<Fact>]
    let ``evolveCloudCoverage maintains values between 0 and 1``() =
        // Arrange
        let initialCoverage = 0.5 // 50% coverage
        let elapsedTime = 60.0    // 1 minute
        let timestep = 0.1        // 0.1 second
        
        // Act
        let newCoverage = evolveCloudCoverage initialCoverage elapsedTime timestep
        
        // Assert
        Assert.True(newCoverage >= 0.0 && newCoverage <= 1.0)
    
    [<Fact>]
    let ``evolveAtmosphereState produces valid atmosphere state``() =
        // Arrange
        let initialState = createTestAtmosphericState 2.0
        let elapsedTime = 60.0 // 1 minute
        let timestep = 0.1     // 0.1 second
        
        // Act
        let newState = evolveAtmosphereState initialState elapsedTime timestep
        
        // Assert
        Assert.True(newState.SeeingCondition >= 0.5 && newState.SeeingCondition <= 5.0)
        Assert.True(newState.CloudCoverage >= 0.0 && newState.CloudCoverage <= 1.0)
        Assert.True(newState.Transparency >= 0.0 && newState.Transparency <= 1.0)
        
        // Transparency should be inversely related to cloud coverage
        if newState.CloudCoverage > 0.5 then
            Assert.True(newState.Transparency < 0.5)

/// Tests for the MountTracking module
module MountTrackingTests =
    open TestUtils
    
    [<Fact>]
    let ``calculatePeriodicError produces expected periodic variation``() =
        // Arrange
        let amplitude = 5.0  // 5 arcsec amplitude
        let period = 120.0   // 120 second period
        // Use None for harmonics to test pure sine wave
        
        // Act
        // Check at 0, 1/4, 1/2, 3/4, and full period
        let error0 = calculatePeriodicError amplitude period 0.0 []
        let error30 = calculatePeriodicError amplitude period (period / 4.0) []
        let error60 = calculatePeriodicError amplitude period (period / 2.0) []
        let error90 = calculatePeriodicError amplitude period (period * 3.0 / 4.0) []
        let error120 = calculatePeriodicError amplitude period period []
        
        // Assert
        // At 0 should be 0
        Assert.True (approxEqual 0.1 0.0 error0)
        
        // At 1/4 period should be amplitude
        Assert.True (approxEqual (amplitude * 0.2) amplitude error30)
        
        // At 1/2 period should be 0 again
        Assert.True (approxEqual 0.1 0.0 error60)
        
        // At 3/4 period should be -amplitude
        Assert.True (approxEqual (amplitude * 0.2) -amplitude error90)
        
        // At full period should be 0 again
        Assert.True (approxEqual 0.1 0.0 error120)
    
    [<Fact>]
    let ``calculatePolarAlignmentEffects generates field rotation and drift``() =
        // Arrange
        let polarError = 1.0   // 1 degree polar alignment error
        let timestamp = 3600.0 // 1 hour into simulation
        let ra = 100.0         // RA of mount
        let dec = 20.0         // Dec of mount
        
        // Act
        let (rotationArcsec, driftArcsec) = calculatePolarAlignmentEffects polarError timestamp ra dec
        
        // Assert
        // Should produce non-zero rotation and drift for non-zero polar error
        Assert.NotEqual(0.0, rotationArcsec)
        Assert.NotEqual(0.0, driftArcsec)
    
    [<Fact>]
    let ``calculatePolarAlignmentEffects returns zero for perfect alignment``() =
        // Arrange
        let polarError = 0.0   // Perfect alignment
        let timestamp = 3600.0 // 1 hour
        let ra = 100.0
        let dec = 20.0
        
        // Act
        let (rotationArcsec, driftArcsec) = calculatePolarAlignmentEffects polarError timestamp ra dec
        
        // Assert
        Assert.Equal(0.0, rotationArcsec)
        Assert.Equal(0.0, driftArcsec)
    
    [<Fact>]
    let ``calculateRandomTrackingErrors scales with timestep``() =
        // Arrange
        let smallTimestep = 0.1  // 0.1 second
        let largeTimestep = 1.0  // 1.0 second
        
        // Act
        // Collect multiple samples to account for randomness
        let smallErrorsMagnitude = [1..100] |> List.map (fun _ -> 
            let (raError, decError) = calculateRandomTrackingErrors smallTimestep
            Math.Sqrt(raError * raError + decError * decError))
            
        let largeErrorsMagnitude = [1..100] |> List.map (fun _ -> 
            let (raError, decError) = calculateRandomTrackingErrors largeTimestep
            Math.Sqrt(raError * raError + decError * decError))
            
        // Calculate average magnitudes
        let avgSmallError = List.sum smallErrorsMagnitude / float smallErrorsMagnitude.Length
        let avgLargeError = List.sum largeErrorsMagnitude / float largeErrorsMagnitude.Length
        
        // Assert
        // Average magnitude should scale with square root of timestep
        let expectedRatio = Math.Sqrt(largeTimestep / smallTimestep) // √(1.0/0.1) = √10 ≈ 3.16
        let actualRatio = avgLargeError / avgSmallError
        
        Assert.True(approxEqual 0.5 expectedRatio actualRatio)
    
    [<Fact>]
    let ``checkForBindingEvent eventually triggers events``() =
        // Arrange
        let lastBindingTime = 0.0
        let meanInterval = 10.0 // Mean interval of 10 seconds for test
        
        // Act
        // Check if binding occurs after a long time
        let mutable bindingOccurred = false
        let mutable iterations = 0
        let mutable currentTime = lastBindingTime
        
        while not bindingOccurred && iterations < 1000 do
            currentTime <- currentTime + 1.0
            let (occurred, _, _, _) = checkForBindingEvent lastBindingTime currentTime meanInterval
            bindingOccurred <- occurred
            iterations <- iterations + 1
        
        // Assert
        Assert.True(bindingOccurred) // Event should eventually occur
    
    [<Fact>]
    let ``evolveMountState applies tracking rate correctly``() =
        // Arrange
        let initialState = createTestMountState 100.0 20.0
        let timestep = 60.0 // 1 minute
        let elapsedTime = 0.0
        let lastBindingTime = 0.0
        
        // Act
        let (newState, _) = evolveMountState initialState elapsedTime timestep lastBindingTime
        
        // Assert
        // RA should change by approximately sidereal rate
        let expectedRA = initialState.RA + initialState.TrackingRate * timestep / 3600.0
        Assert.True(approxEqual 0.1 expectedRA newState.RA)
        
        // Dec should be close to initial (with some variations)
        Assert.True(approxEqual 0.1 initialState.Dec newState.Dec)

/// Tests for the SensorPhysics module
module SensorPhysicsTests =
    open TestUtils
    
    [<Fact>]
    let ``randomGaussian produces distribution with correct statistics``() =
        // Arrange
        let mean = 10.0
        let sigma = 2.0
        let sampleSize = 1000
        
        // Act
        let samples = [1..sampleSize] |> List.map (fun _ -> randomGaussian mean sigma)
        let sampleMean = List.sum samples / float sampleSize
        let sampleVariance = 
            samples 
            |> List.map (fun x -> (x - sampleMean) * (x - sampleMean)) 
            |> List.sum 
            |> fun sum -> sum / float sampleSize
        let sampleStdDev = Math.Sqrt(sampleVariance)
        
        // Assert
        // Sample mean should be close to the target mean
        Assert.True(approxEqual (sigma * 0.2) mean sampleMean)
        
        // Sample standard deviation should be close to the target
        Assert.True(approxEqual (sigma * 0.2) sigma sampleStdDev)
    
    [<Fact>]
    let ``randomPoisson produces distribution with correct statistics``() =
        // Arrange
        let lambda = 10.0
        let sampleSize = 1000
        
        // Act
        let samples = [1..sampleSize] |> List.map (fun _ -> randomPoisson lambda)
        let sampleMean = List.sum samples / float sampleSize
        let sampleVariance = 
            samples 
            |> List.map (fun x -> (x - sampleMean) * (x - sampleMean)) 
            |> List.sum 
            |> fun sum -> sum / float sampleSize
        
        // Assert
        // For Poisson distribution, mean = variance = lambda
        Assert.True(approxEqual (lambda * 0.2) lambda sampleMean)
        Assert.True(approxEqual (lambda * 0.3) lambda sampleVariance)
    
    [<Fact>]
    let ``calculateDarkCurrent doubles with 6.5°C increase``() =
        // Arrange
        let baseDarkCurrent = 0.01 // e-/pixel/sec at 0°C
        
        // Act
        let darkCurrent0C = calculateDarkCurrent baseDarkCurrent 0.0
        let darkCurrent65C = calculateDarkCurrent baseDarkCurrent 6.5
        
        // Assert
        // Dark current should double with 6.5°C increase
        Assert.True(approxEqual (baseDarkCurrent * 0.1) (2.0 * darkCurrent0C) darkCurrent65C)
    
    [<Fact>]
    let ``createSensorModel produces valid model``() =
        // Arrange
        let cameraState = createTestCameraState 4096 2160
        let temperature = -10.0 // -10°C
        
        // Act
        let sensorModel = createSensorModel cameraState temperature
        
        // Assert
        Assert.Equal(cameraState.Width, sensorModel.Width)
        Assert.Equal(cameraState.Height, sensorModel.Height)
        Assert.Equal(cameraState.PixelSize, sensorModel.PixelSize)
        Assert.True(sensorModel.QuantumEfficiency > 0.0 && sensorModel.QuantumEfficiency <= 1.0)
        Assert.Equal(cameraState.ReadNoise, sensorModel.ReadNoise)
        Assert.Equal(cameraState.DarkCurrent, sensorModel.DarkCurrent)
        Assert.True(sensorModel.Gain > 0.0)
        Assert.Equal(temperature, sensorModel.Temperature)
        Assert.True(sensorModel.FullWellCapacity > 0)
        Assert.True(sensorModel.BiasLevel >= 0)
        Assert.True(sensorModel.BitDepth > 0)
    
    [<Fact>]
    let ``applyQuantumEfficiency scales signal by QE``() =
        // Arrange
        let buffer = Array2D.create 10 10 100.0 // 100 photons per pixel
        let qe = 0.8 // 80% quantum efficiency
        
        // Act
        let result = applyQuantumEfficiency buffer qe
        
        // Assert
        // Check a sample of pixels
        for x = 0 to 9 do
            for y = 0 to 9 do
                // Should be approximately 80 electrons per pixel
                // But with statistical variation
                Assert.True(result.[x, y] > 50.0 && result.[x, y] < 110.0)
                
        // Average should be close to 80
        let mutable sum = 0.0
        for x = 0 to 9 do
            for y = 0 to 9 do
                sum <- sum + result.[x, y]
        let avg = sum / 100.0
        
        Assert.True(approxEqual 10.0 (qe * 100.0) avg)
    
    [<Fact>]
    let ``applyDarkCurrent adds appropriate noise``() =
        // Arrange
        let buffer = Array2D.create 10 10 100.0 // 100 electrons per pixel
        let darkCurrent = 0.1 // 0.1 e-/pixel/sec
        let exposureTime = 60.0 // 60 seconds
        
        // Expected dark electrons: 0.1 * 60 = 6 per pixel
        
        // Act
        let result = applyDarkCurrent buffer darkCurrent exposureTime
        
        // Assert
        // Buffer should be modified in place
        Assert.Same(buffer, result)
        
        // Check a sample of pixels
        for x = 0 to 9 do
            for y = 0 to 9 do
                // Should be approximately 106 electrons per pixel
                // But with statistical variation
                Assert.True(result.[x, y] > 90.0 && result.[x, y] < 120.0)

/// Tests for the BufferOperations module
module BufferOperationTests =
    open TestUtils
    
    [<Fact>]
    let ``createEmptyBuffer creates zero-filled buffer of correct size``() =
        // Arrange
        let width = 10
        let height = 20
        
        // Act
        let buffer = createEmptyBuffer width height
        
        // Assert
        Assert.Equal(width, Array2D.length1 buffer)
        Assert.Equal(height, Array2D.length2 buffer)
        
        // Check all elements are zero
        for x = 0 to width - 1 do
            for y = 0 to height - 1 do
                Assert.Equal(0.0, buffer.[x, y])
    
    [<Fact>]
    let ``accumulatePhotons distributes photons using PSF``() =
        // Arrange
        let buffer = createEmptyBuffer 20 20
        let x = 10.0 // Center X
        let y = 10.0 // Center Y
        let photons = 1000.0 // 1000 photons
        
        // Create a simple Gaussian PSF
        let size = 5
        let psfValues = Array2D.create size size 0.0
        let mutable sum = 0.0
        
        // Fill with a simple pattern
        for i = 0 to size - 1 do
            for j = 0 to size - 1 do
                let value = if i = size/2 && j = size/2 then 0.5 else 0.5 / float((size-1) * (size-1))
                psfValues.[i, j] <- value
                sum <- sum + value
                
        let psf = { Values = psfValues; Size = size; Sum = sum }
        
        // Act
        accumulatePhotons buffer x y photons psf
        
        // Assert
        // Total photons should be preserved
        let mutable totalPhotons = 0.0
        for i = 0 to Array2D.length1 buffer - 1 do
            for j = 0 to Array2D.length2 buffer - 1 do
                totalPhotons <- totalPhotons + buffer.[i, j]
                
        Assert.True(approxEqual 1.0 photons totalPhotons)
        
        // Center pixel should have highest value
        let centerValue = buffer.[int x, int y]
        for i = 0 to Array2D.length1 buffer - 1 do
            for j = 0 to Array2D.length2 buffer - 1 do
                Assert.True(buffer.[i, j] <= centerValue)
    
    [<Fact>]
    let ``applyCloudCover attenuates signal``() =
        // Arrange
        let buffer = Array2D.create 10 10 100.0 // 100 photons per pixel
        let cloudCoverage = 0.5 // 50% cloud coverage
        let timestamp = 10.0
        
        // Act
        let result = applyCloudCover buffer cloudCoverage timestamp
        
        // Assert
        // Buffer should be modified in place
        Assert.Same(buffer, result)
        
        // Values should be attenuated
        for x = 0 to 9 do
            for y = 0 to 9 do
                Assert.True(result.[x, y] < 100.0) // All pixels should be attenuated
                Assert.True(result.[x, y] > 0.0)   // No pixel should be completely blocked
    
    [<Fact>]
    let ``combineBuffers sums all buffers correctly``() =
        // Arrange
        let buffer1 = Array2D.create 10 10 10.0
        let buffer2 = Array2D.create 10 10 20.0
        let buffer3 = Array2D.create 10 10 30.0
        
        // Act
        let result = combineBuffers [buffer1; buffer2; buffer3]
        
        // Assert
        Assert.Equal(10, Array2D.length1 result)
        Assert.Equal(10, Array2D.length2 result)
        
        // Each pixel should be the sum of corresponding pixels
        for x = 0 to 9 do
            for y = 0 to 9 do
                Assert.Equal(60.0, result.[x, y]) // 10 + 20 + 30
    
    [<Fact>]
    let ``bufferToArray correctly flattens 2D array to 1D``() =
        // Arrange
        let buffer = Array2D.create 3 2 0.0
        // Fill with distinct values
        buffer.[0, 0] <- 1.0
        buffer.[1, 0] <- 2.0
        buffer.[2, 0] <- 3.0
        buffer.[0, 1] <- 4.0
        buffer.[1, 1] <- 5.0
        buffer.[2, 1] <- 6.0
        
        // Act
        let array = bufferToArray buffer
        
        // Assert
        Assert.Equal(6, array.Length) // 3x2 = 6 elements
        
        // Check row-major order
        Assert.Equal(1.0, array.[0])
        Assert.Equal(2.0, array.[1])
        Assert.Equal(3.0, array.[2])
        Assert.Equal(4.0, array.[3])
        Assert.Equal(5.0, array.[4])
        Assert.Equal(6.0, array.[5])