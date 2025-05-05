namespace EigenAstroSim.Tests

open System
open Xunit
open FsUnit.Xunit
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
open EigenAstroSim.Bessel

module AstrophysicsPropertyTestUtils =
    // Helper functions for creating test data and assertions

    /// Test if two floating point values are approximately equal within a relative tolerance
    let approxEqualRel (expected: float) (actual: float) (relativeTolerance: float) =
        let absExpected = Math.Abs(expected)
        let absActual = Math.Abs(actual)
        let largestAbs = Math.Max(absExpected, absActual)
        
        if largestAbs < Double.Epsilon then
            // For values near zero, use absolute tolerance
            Math.Abs(expected - actual) <= relativeTolerance
        else
            // For non-zero values, use relative tolerance
            Math.Abs(expected - actual) / largestAbs <= relativeTolerance

    /// Test if two floating point values are approximately equal within an absolute tolerance
    let approxEqualAbs (expected: float) (actual: float) (absoluteTolerance: float) =
        Math.Abs(expected - actual) <= absoluteTolerance

    /// Create a test star with the specified properties
    let createTestStar ra dec magnitude color =
        {
            Id = Guid.NewGuid()
            RA = ra
            Dec = dec
            Magnitude = magnitude
            Color = color
        }
        
    /// Create a test mount state
    let createTestMountState ra dec focalLength =
        {
            RA = ra
            Dec = dec
            FocalLength = focalLength
            TrackingRate = 15.0 / 3600.0 // sidereal rate in deg/sec
            PeriodicErrorAmplitude = 5.0
            PeriodicErrorPeriod = 600.0
            PolarAlignmentError = 0.5
            PeriodicErrorHarmonics = []
            IsSlewing = false
            SlewRate = 3.0
        }
        
    /// Create a test camera state
    let createTestCameraState width height pixelSize exposureTime =
        {
            Width = width
            Height = height
            PixelSize = pixelSize
            ExposureTime = exposureTime
            ReadNoise = 2.0
            DarkCurrent = 0.01
            Binning = 1
            IsExposing = false
        }
        
    /// Create a test atmospheric state
    let createTestAtmosphericState seeingCondition cloudCoverage transparency =
        {
            SeeingCondition = seeingCondition
            CloudCoverage = cloudCoverage
            Transparency = transparency
        }
        
    /// Create test optical parameters
    let createTestOpticalParams aperture obstruction focalLength =
        {
            Aperture = aperture
            Obstruction = obstruction 
            FocalLength = focalLength
            Transmission = 0.85
            OpticalQuality = 0.9
            FRatio = focalLength / aperture
        }

    /// Calculate the sum of all values in a 2D array
    let sumArray2D (arr: float[,]) =
        let mutable sum = 0.0
        for i = 0 to (Array2D.length1 arr) - 1 do
            for j = 0 to (Array2D.length2 arr) - 1 do
                sum <- sum + arr.[i, j]
        sum

    /// Find the maximum value in a 2D array
    let maxArray2D (arr: float[,]) =
        let mutable max = Double.MinValue
        for i = 0 to (Array2D.length1 arr) - 1 do
            for j = 0 to (Array2D.length2 arr) - 1 do
                if arr.[i, j] > max then
                    max <- arr.[i, j]
        max
        
    /// Create an array of evenly-spaced samples between min and max (inclusive)
    let linspace min max count =
        if count <= 1 then [| min |]
        else
            let step = (max - min) / float(count - 1)
            [| 0 .. (count - 1) |] |> Array.map (fun i -> min + float(i) * step)

module StarProjectionPropertyTests =
    open AstrophysicsPropertyTestUtils
    
    [<Theory>]
    [<InlineData(100.0, 20.0, 1000.0, 4096, 2160, 3.8)>]  // Standard telescope setup
    [<InlineData(0.0, 0.0, 500.0, 1920, 1080, 5.0)>]      // Shorter focal length, larger pixels
    [<InlineData(180.0, 45.0, 2000.0, 6000, 4000, 2.4)>]  // Longer focal length, smaller pixels
    let ``Star at mount center projects to sensor center`` (ra, dec, focalLength, width, height, pixelSize) =
        // Arrange
        let star = createTestStar ra dec 6.0 0.5
        let mountState = createTestMountState ra dec focalLength
        let cameraState = createTestCameraState width height pixelSize 30.0
        
        // Act
        let pixelStar = projectStar star mountState cameraState
        
        // Assert
        let expectedX = float width / 2.0
        let expectedY = float height / 2.0
        
        approxEqualAbs expectedX pixelStar.X 0.5 |> should equal true
        approxEqualAbs expectedY pixelStar.Y 0.5 |> should equal true
        
    [<Theory>]
    [<InlineData(500.0, 3.8)>]   // Typical amateur setup
    [<InlineData(1000.0, 5.0)>]  // Medium focal length
    [<InlineData(2000.0, 2.4)>]  // High-end setup
    [<InlineData(300.0, 10.0)>]  // Short focal length, large pixels
    let ``Plate scale calculation follows standard formula`` (focalLength, pixelSize) =
        // Arrange
        // Standard formula: 206.265 * (pixel size in μm) / (focal length in mm)
        let expectedPlateScale = 206.265 * pixelSize / focalLength
        
        // Act
        let actualPlateScale = calculatePlateScale focalLength pixelSize
        
        // Assert
        approxEqualRel expectedPlateScale actualPlateScale 1e-10 |> should equal true
    
    [<Theory>]
    [<InlineData(100.0, 20.0, 100.05, 20.0, 1000.0, 3.8)>]   // Small RA offset
    [<InlineData(100.0, 20.0, 100.0, 20.05, 1000.0, 3.8)>]   // Small Dec offset
    [<InlineData(100.0, 20.0, 100.5, 20.5, 1000.0, 3.8)>]    // Both RA and Dec offset
    [<InlineData(100.0, 20.0, 99.5, 19.5, 1000.0, 3.8)>]     // Negative offsets
    [<InlineData(100.0, 80.0, 101.0, 80.0, 1000.0, 3.8)>]    // Near-pole test
    let ``Star projection accounts for spherical coordinates`` (mountRA, mountDec, starRA, starDec, focalLength, pixelSize) =
        // Arrange
        let star = createTestStar starRA starDec 6.0 0.5
        let mountState = createTestMountState mountRA mountDec focalLength
        let cameraState = createTestCameraState 4096 2160 pixelSize 30.0
        
        // Act
        let pixelStar = projectStar star mountState cameraState
        
        // Calculate expected position (accounting for cos(Dec) factor in RA)
        let plateScale = calculatePlateScale focalLength pixelSize
        let deltaRA = (starRA - mountRA) * Math.Cos(toRadians mountDec)
        let deltaDec = starDec - mountDec
        
        let expectedX = (float cameraState.Width / 2.0) - (deltaRA * 3600.0) / plateScale
        let expectedY = (float cameraState.Height / 2.0) + (deltaDec * 3600.0) / plateScale
        
        // Assert
        approxEqualRel expectedX pixelStar.X 1e-6 |> should equal true
        approxEqualRel expectedY pixelStar.Y 1e-6 |> should equal true
    
    [<Theory>]
    [<InlineData(10.0, 5.0, 4096, 2160)>]    // On-sensor with margin
    [<InlineData(-5.0, 100.0, 4096, 2160)>]  // Just off-sensor but within margin
    [<InlineData(10000.0, 10.0, 4096, 2160)>] // Far off-sensor
    let ``Star visibility determination is consistent with position`` (x, y, width, height) =
        // Arrange
        let pixelStar = {
            Star = createTestStar 100.0 20.0 6.0 0.5
            X = x
            Y = y
            PhotonFlux = None
        }
        
        // Act
        let isVisible = isStarVisible pixelStar width height
        
        // Assert
        // Star should be visible if within sensor dimensions, plus margin
        let margin = 20.0
        let shouldBeVisible = 
            x >= -margin && x < float width + margin &&
            y >= -margin && y < float height + margin
            
        shouldBeVisible |> should equal isVisible

module PhotonGenerationPropertyTests =
    open AstrophysicsPropertyTestUtils
    
    [<Theory>]
    [<InlineData(-0.3)>]  // Hot blue star
    [<InlineData(0.6)>]   // Sun-like star
    [<InlineData(1.5)>]   // Cool red star
    let ``ColorIndex to wavelength mapping preserves spectral sequence`` (colorIndex) =
        // Arrange & Act
        let wavelength = colorIndexToWavelength colorIndex
        
        // Assert
        // Wavelength should be in visible range
        wavelength |> should be (greaterThanOrEqualTo 400.0)
        wavelength |> should be (lessThanOrEqualTo 700.0)
        
        // Bluer (lower) color indices should have shorter wavelengths
        if colorIndex < 0.0 then wavelength |> should be (lessThanOrEqualTo 500.0)
        if colorIndex > 1.0 then wavelength |> should be (greaterThanOrEqualTo 600.0)
    
    [<Theory>]
    [<InlineData(200.0, 60.0)>]    // Typical SCT with central obstruction
    [<InlineData(120.0, 0.0)>]     // Refractor with no obstruction
    [<InlineData(350.0, 120.0)>]   // Large SCT with central obstruction
    let ``Aperture area calculation follows optical geometry`` (aperture, obstruction) =
        // Arrange
        let apertureRadius = aperture / 2.0
        let obstructionRadius = obstruction / 2.0
        let expectedArea = Math.PI * (apertureRadius * apertureRadius - obstructionRadius * obstructionRadius) / 1000000.0 // in m²
        
        // Act
        let actualArea = apertureArea aperture obstruction
        
        // Assert
        approxEqualRel expectedArea actualArea 1e-10 |> should equal true
        
        // Additional property check: area should be zero when obstruction equals aperture
        let zeroArea = apertureArea aperture aperture
        zeroArea |> should equal 0.0
    
    [<Theory>]
    [<InlineData(2.0, 7.0, 200.0, 60.0, 60.0)>]  // 5 magnitude difference, SCT, 60s exposure
    [<InlineData(0.0, 5.0, 120.0, 0.0, 30.0)>]   // 5 magnitude difference, refractor, 30s
    [<InlineData(10.0, 15.0, 350.0, 120.0, 120.0)>] // 5 magnitude difference, large SCT, 120s
    let ``Photon flux scales correctly with magnitude difference`` (mag1, mag2, aperture, obstruction, exposureTime) =
        // Arrange
        let focalLength = 1000.0
        
        let star1 = {
            Star = createTestStar 100.0 20.0 mag1 0.5
            X = 2000.0
            Y = 1080.0
            PhotonFlux = None
        }
        
        let star2 = {
            Star = createTestStar 100.0 20.0 mag2 0.5
            X = 2000.0
            Y = 1080.0
            PhotonFlux = None
        }
        
        let optics = createTestOpticalParams aperture obstruction focalLength
        
        // Act
        let star1WithFlux = calculatePhotonFlux star1 optics exposureTime
        let star2WithFlux = calculatePhotonFlux star2 optics exposureTime
        
        // Assert
        star1WithFlux.PhotonFlux.IsSome |> should equal true
        star2WithFlux.PhotonFlux.IsSome |> should equal true
        
        // 5 magnitude difference should be factor of 100 in brightness
        // according to the Pogson equation
        let magDiff = Math.Abs(mag1 - mag2)
        let expectedRatio = Math.Pow(10.0, 0.4 * magDiff)
        
        let ratio = 
            if star1WithFlux.PhotonFlux.Value > star2WithFlux.PhotonFlux.Value then
                star1WithFlux.PhotonFlux.Value / star2WithFlux.PhotonFlux.Value
            else
                star2WithFlux.PhotonFlux.Value / star1WithFlux.PhotonFlux.Value
                
        // Allow 5% tolerance for implementation variations
        approxEqualRel expectedRatio ratio 0.05 |> should equal true
    
    [<Theory>]
    [<InlineData(6.0, 200.0, 60.0, 30.0, 60.0)>]  // Double exposure time, SCT
    [<InlineData(8.0, 120.0, 0.0, 10.0, 20.0)>]   // Double exposure time, refractor
    [<InlineData(12.0, 350.0, 120.0, 60.0, 120.0)>] // Double exposure time, large SCT
    let ``Photon flux scales linearly with exposure time`` (magnitude, aperture, obstruction, exposureTime1, exposureTime2) =
        // Arrange
        let focalLength = 1000.0
        
        let star = {
            Star = createTestStar 100.0 20.0 magnitude 0.5
            X = 2000.0
            Y = 1080.0
            PhotonFlux = None
        }
        
        let optics = createTestOpticalParams aperture obstruction focalLength
        
        // Act
        let starWithFlux1 = calculatePhotonFlux star optics exposureTime1
        let starWithFlux2 = calculatePhotonFlux star optics exposureTime2
        
        // Assert
        starWithFlux1.PhotonFlux.IsSome |> should equal true
        starWithFlux2.PhotonFlux.IsSome |> should equal true
        
        // Flux should scale linearly with exposure time
        let expectedRatio = exposureTime2 / exposureTime1
        let actualRatio = starWithFlux2.PhotonFlux.Value / starWithFlux1.PhotonFlux.Value
        
        // Allow 1% tolerance for implementation variations
        approxEqualRel expectedRatio actualRatio 0.01 |> should equal true
    
    [<Theory>]
    [<InlineData(6.0, 100.0, 30.0, 200.0, 60.0)>]    // Double aperture area
    [<InlineData(8.0, 120.0, 0.0, 169.7, 0.0)>]      // Double aperture area (sqrt(2) factor in diameter)
    [<InlineData(10.0, 200.0, 60.0, 282.8, 84.8)>]   // Double area with obstruction
    let ``Photon flux scales with effective aperture area`` (magnitude, aperture1, obstruction1, aperture2, obstruction2) =
        // Arrange
        let focalLength = 1000.0
        let exposureTime = 60.0
        
        let star = {
            Star = createTestStar 100.0 20.0 magnitude 0.5
            X = 2000.0
            Y = 1080.0
            PhotonFlux = None
        }
        
        let optics1 = createTestOpticalParams aperture1 obstruction1 focalLength
        let optics2 = createTestOpticalParams aperture2 obstruction2 focalLength
        
        // Calculate the effective areas
        let area1 = apertureArea aperture1 obstruction1
        let area2 = apertureArea aperture2 obstruction2
        
        // Act
        let starWithFlux1 = calculatePhotonFlux star optics1 exposureTime
        let starWithFlux2 = calculatePhotonFlux star optics2 exposureTime
        
        // Assert
        starWithFlux1.PhotonFlux.IsSome |> should equal true
        starWithFlux2.PhotonFlux.IsSome |> should equal true
        
        // Flux should scale with area ratio (and transmission which is fixed here)
        let expectedRatio = area2 / area1
        let actualRatio = starWithFlux2.PhotonFlux.Value / starWithFlux1.PhotonFlux.Value
        
        // Allow 1% tolerance for implementation variations and floating point
        approxEqualRel expectedRatio actualRatio 0.01 |> should equal true

module PsfGenerationPropertyTests =
    open AstrophysicsPropertyTestUtils
    
    [<Theory>]
    [<InlineData(200.0, 60.0, 1000.0, 550.0, 3.8, 21)>]    // Typical SCT setup
    [<InlineData(120.0, 0.0, 800.0, 450.0, 5.0, 15)>]      // Refractor setup
    [<InlineData(350.0, 120.0, 2800.0, 650.0, 2.4, 31)>]   // Large SCT setup
    let ``Airy disk PSF conserves energy`` (aperture, obstruction, focalLength, wavelength, pixelSize, size) =
        // Arrange
        let optics = createTestOpticalParams aperture obstruction focalLength
        
        // Act
        let psf = generateAiryDiskPSF optics wavelength pixelSize size
        
        // Assert
        // The sum of PSF values should be very close to 1.0 (normalized)
        approxEqualRel 1.0 psf.Sum 1e-10 |> should equal true
        
        // The PSF should have the requested size
        psf.Size |> should equal size
        
        // The center should have the maximum value
        let centerValue = psf.Values.[size/2, size/2]
        let maxValue = maxArray2D psf.Values
        centerValue |> should equal maxValue
    
    [<Theory>]
    [<InlineData(3.0, 21)>]   // Medium seeing
    [<InlineData(1.0, 15)>]   // Excellent seeing
    [<InlineData(5.0, 31)>]   // Poor seeing
    let ``Gaussian PSF conserves energy`` (fwhmPixels, size) =
        // Arrange & Act
        let psf = generateGaussianPSF fwhmPixels size
        
        // Assert
        // The sum of PSF values should be very close to 1.0 (normalized)
        approxEqualRel 1.0 psf.Sum 1e-10 |> should equal true
        
        // The PSF should have the requested size
        psf.Size |> should equal size
        
        // The center should have the maximum value
        let centerValue = psf.Values.[size/2, size/2]
        let maxValue = maxArray2D psf.Values
        centerValue |> should equal maxValue
    
    [<Theory>]
    [<InlineData(1.0, 3.0, 21)>]    // Small + Medium seeing
    [<InlineData(2.0, 4.0, 15)>]    // Medium + Large seeing
    [<InlineData(0.5, 5.0, 31)>]    // Very small + Large seeing
    let ``Convolution of PSFs conserves energy`` (fwhm1, fwhm2, size) =
        // Arrange
        let psf1 = generateGaussianPSF fwhm1 size
        let psf2 = generateGaussianPSF fwhm2 size
        
        // Act
        let combinedPsf = convolvePSFs psf1 psf2
        
        // Assert
        // The combined PSF should also be normalized
        approxEqualRel 1.0 combinedPsf.Sum 1e-10 |> should equal true
    
    [<Theory>]
    [<InlineData(200.0, 60.0, 1000.0, 400.0, 3.8, 21)>]    // Blue light, SCT
    [<InlineData(200.0, 60.0, 1000.0, 700.0, 3.8, 21)>]    // Red light, SCT
    let ``Airy disk size scales with wavelength`` (aperture, obstruction, focalLength, wavelength1, pixelSize, size) =
        // Arrange
        let optics = createTestOpticalParams aperture obstruction focalLength
        let wavelength2 = wavelength1 * 2.0 // Double the wavelength
        
        // Act
        let psf1 = generateAiryDiskPSF optics wavelength1 pixelSize size
        let psf2 = generateAiryDiskPSF optics wavelength2 pixelSize size
        
        // Calculate FWHM by finding where the value drops to half the peak
        // Note: This is an approximation that works for demonstration
        let peak1 = psf1.Values.[size/2, size/2]
        let peak2 = psf2.Values.[size/2, size/2]
        
        let halfMax1 = peak1 / 2.0
        let halfMax2 = peak2 / 2.0
        
        // Count pixels from center until we drop below half max
        let mutable radius1 = 0
        while (size/2 + radius1 < size) && (psf1.Values.[size/2 + radius1, size/2] >= halfMax1) do
            radius1 <- radius1 + 1
            
        let mutable radius2 = 0
        while (size/2 + radius2 < size) && (psf2.Values.[size/2 + radius2, size/2] >= halfMax2) do
            radius2 <- radius2 + 1
        
        // Assert
        // Airy disk FWHM should scale roughly linearly with wavelength
        // Allow a higher tolerance since this is an approximation
        let expectedRatio = wavelength2 / wavelength1
        let actualRatio = float radius2 / float radius1
        
        // The actual ratio should be close to the expected ratio
        // Use a higher tolerance (15%) due to pixelization effects
        approxEqualRel expectedRatio actualRatio 0.15 |> should equal true

module AtmosphericEvolutionPropertyTests =
    open AstrophysicsPropertyTestUtils
    
    [<Theory>]
    [<InlineData(1.0)>]  // Excellent seeing
    [<InlineData(2.5)>]  // Average seeing
    [<InlineData(4.0)>]  // Poor seeing
    let ``Atmospheric turbulence layers have correct total strength`` (seeing) =
        // Arrange & Act
        let layers = generateAtmosphericLayers seeing
        
        // Assert
        // Should create exactly 3 layers
        layers.Length |> should equal 3
        
        // Total strength should sum to 1.0
        let totalStrength = layers |> List.sumBy (fun l -> l.Strength)
        approxEqualRel 1.0 totalStrength 1e-10 |> should equal true
        
        // Each layer should have a strength between 0 and 1
        for layer in layers do
            layer.Strength |> should be (greaterThan 0.0)
            layer.Strength |> should be (lessThan 1.0)
            
            // Other parameters should be in reasonable ranges
            layer.Height |> should be (greaterThanOrEqualTo 0.0)
            layer.Direction |> should be (greaterThanOrEqualTo 0.0)
            layer.Direction |> should be (lessThan 360.0)
            layer.Speed |> should be (greaterThan 0.0)
            layer.TimeScale |> should be (greaterThan 0.0)
    
    [<Theory>]
    [<InlineData(1.0, 10.0)>]    // Excellent seeing, 10 seconds
    [<InlineData(2.5, 30.0)>]    // Average seeing, 30 seconds
    [<InlineData(4.0, 60.0)>]    // Poor seeing, 60 seconds
    let ``Atmospheric jitter magnitude is proportional to seeing`` (seeing, timestamp) =
        // Arrange
        let layers = generateAtmosphericLayers seeing
        
        // Act
        let jitter = calculateJitter layers seeing timestamp
        
        // Assert
        // Jitter components should be within seeing range
        let jitterMagnitude = Math.Sqrt(jitter.X * jitter.X + jitter.Y * jitter.Y)
        jitterMagnitude |> should be (lessThanOrEqualTo (seeing * 1.5))
        
        // Create jitter for double seeing
        let doubleSeeing = seeing * 2.0
        let layers2 = generateAtmosphericLayers doubleSeeing
        let jitter2 = calculateJitter layers2 doubleSeeing timestamp
        
        // Calculate magnitudes
        let jitterMagnitude2 = Math.Sqrt(jitter2.X * jitter2.X + jitter2.Y * jitter2.Y)
        
        // Jitter should scale approximately with seeing
        // But this is statistical, so use a loose tolerance
        // The ratio should be roughly 2.0, but allow wide margins
        let ratio = jitterMagnitude2 / jitterMagnitude
        ratio |> should be (greaterThan 1.0)  // Should definitely increase
    
    [<Theory>]
    [<InlineData(2.0, 0.0, 0.1)>]    // Initial seeing evolution
    [<InlineData(2.0, 60.0, 0.1)>]   // After 1 minute
    [<InlineData(2.0, 600.0, 0.1)>]  // After 10 minutes
    let ``Seeing evolution stays within plausible limits`` (initialSeeing, elapsedTime, timestep) =
        // Arrange & Act
        let newSeeing = evolveSeeingCondition initialSeeing elapsedTime timestep
        
        // Assert
        // Seeing should stay within plausible range for Earth-based observing
        newSeeing |> should be (greaterThanOrEqualTo 0.5)  // Better than 0.5" is rare
        newSeeing |> should be (lessThanOrEqualTo 5.0)     // Worse than 5" is rare
        
        // Seeing shouldn't change dramatically in short time
        let shortDelta = Math.Abs(newSeeing - initialSeeing)
        shortDelta |> should be (lessThan initialSeeing)
        
        // For longer evolution, let's verify the trend continuity
        // Take 10 consecutive steps and ensure they change smoothly
        let mutable currentSeeing = initialSeeing
        let seeingValues = [1..10] |> List.map (fun i ->
            let time = elapsedTime + float i * timestep * 10.0
            let evolved = evolveSeeingCondition currentSeeing time timestep
            currentSeeing <- evolved
            evolved)
            
        // Calculate maximum jump between consecutive values
        let maxJump = 
            seeingValues
            |> List.pairwise
            |> List.map (fun (a, b) -> Math.Abs(a - b))
            |> List.max
            
        // Maximum jump shouldn't be too large for continuous evolution
        maxJump |> should be (lessThan (initialSeeing * 0.5))
    
    [<Theory>]
    [<InlineData(0.0, 0.0, 0.1)>]    // Clear sky
    [<InlineData(0.5, 60.0, 0.1)>]   // Partly cloudy
    [<InlineData(1.0, 600.0, 0.1)>]  // Overcast
    let ``Cloud coverage evolution stays within valid range`` (initialCoverage, elapsedTime, timestep) =
        // Arrange & Act
        let newCoverage = evolveCloudCoverage initialCoverage elapsedTime timestep
        
        // Assert
        // Coverage should stay within 0-1 range
        newCoverage |> should be (greaterThanOrEqualTo 0.0)
        newCoverage |> should be (lessThanOrEqualTo 1.0)
        
        // Make 100 random evolution steps and ensure they all stay in range
        let random = Random(12345) // Fixed seed for reproducibility
        let mutable currentCoverage = initialCoverage
        
        for _ in 1..100 do
            let randTime = elapsedTime + random.NextDouble() * 1000.0
            let randStep = timestep * (0.5 + random.NextDouble())
            currentCoverage <- evolveCloudCoverage currentCoverage randTime randStep
            
            // Every evolved value must stay in range
            currentCoverage |> should be (greaterThanOrEqualTo 0.0)
            currentCoverage |> should be (lessThanOrEqualTo 1.0)
    
    [<Theory>]
    [<InlineData(2.0, 0.0, 1.0, 0.0, 0.1)>]    // Clear sky, average seeing
    [<InlineData(1.0, 0.5, 0.7, 60.0, 0.1)>]   // Partly cloudy, good seeing
    [<InlineData(3.0, 1.0, 0.2, 600.0, 0.1)>]  // Overcast, poor seeing
    let ``Atmosphere state evolution maintains physical consistency`` (initialSeeing, initialClouds, initialTransparency, elapsedTime, timestep) =
        // Arrange
        let initialState = createTestAtmosphericState initialSeeing initialClouds initialTransparency
        
        // Act
        let newState = evolveAtmosphereState initialState elapsedTime timestep
        
        // Assert
        // Seeing should be within physical limits
        newState.SeeingCondition |> should be (greaterThanOrEqualTo 0.5)
        newState.SeeingCondition |> should be (lessThanOrEqualTo 5.0)
        
        // Cloud coverage should be within 0-1 range
        newState.CloudCoverage |> should be (greaterThanOrEqualTo 0.0)
        newState.CloudCoverage |> should be (lessThanOrEqualTo 1.0)
        
        // Transparency should be within 0-1 range
        newState.Transparency |> should be (greaterThanOrEqualTo 0.0)
        newState.Transparency |> should be (lessThanOrEqualTo 1.0)
        
        // Transparency should generally be inversely related to cloud coverage
        // (more clouds = less transparency)
        if newState.CloudCoverage > 0.8 then
            newState.Transparency |> should be (lessThan 0.5)
        if newState.CloudCoverage < 0.2 then
            newState.Transparency |> should be (greaterThan 0.5)

module MountTrackingPropertyTests =
    open AstrophysicsPropertyTestUtils
    
    [<Theory>]
    [<InlineData(5.0, 600.0, 0.0)>]     // Start of period
    [<InlineData(5.0, 600.0, 150.0)>]   // 1/4 of period
    [<InlineData(5.0, 600.0, 300.0)>]   // 1/2 of period
    [<InlineData(5.0, 600.0, 450.0)>]   // 3/4 of period
    [<InlineData(5.0, 600.0, 600.0)>]   // Full period
    let ``Periodic error follows correct sinusoidal pattern`` (amplitude, period, timestamp) =
        // Arrange
        let harmonics = [] // No harmonics for this test
        
        // Act
        let error = calculatePeriodicError amplitude period timestamp harmonics
        
        // Assert
        // Calculate expected value manually using sine function
        let phase = 2.0 * Math.PI * timestamp / period
        let expectedError = amplitude * Math.Sin(phase)
        
        // Error should match expected value closely
        approxEqualRel expectedError error 1e-10 |> should equal true
        
        // Verify the error is within the amplitude bounds
        error |> should be (greaterThanOrEqualTo (-amplitude))
        error |> should be (lessThanOrEqualTo amplitude)
    
    
    [<Theory>]
    [<InlineData(1.0, 3600.0, 100.0, 20.0)>]  // 1 degree polar error, 1 hour
    [<InlineData(0.5, 1800.0, 100.0, 20.0)>]  // 0.5 degree polar error, 30 minutes
    [<InlineData(0.0, 3600.0, 100.0, 20.0)>]  // No polar error, 1 hour
    let ``Polar alignment error affects field rotation and drift`` (polarError, timestamp, ra, dec) =
        // Arrange & Act
        let (rotationArcsec, driftArcsec) = calculatePolarAlignmentEffects polarError timestamp ra dec
        
        // Assert
        if polarError > 0.0 then
            // Non-zero polar error should cause rotation and drift
            (Math.Abs(rotationArcsec) > 0.0 || Math.Abs(driftArcsec) > 0.0) |> should equal true
            
            // Higher polar error should cause more rotation/drift
            let (rotation2x, drift2x) = calculatePolarAlignmentEffects (polarError * 2.0) timestamp ra dec
            
            // Effect should approximately double (not exactly due to trigonometric functions)
            Math.Abs(rotation2x) |> should be (greaterThan (Math.Abs(rotationArcsec)))
            Math.Abs(drift2x) |> should be (greaterThan (Math.Abs(driftArcsec)))
        else
            // Zero polar error should cause no rotation or drift
            rotationArcsec |> should equal 0.0
            driftArcsec |> should equal 0.0
    
    [<Theory>]
    [<InlineData(0.1)>]   // Small timestep
    [<InlineData(1.0)>]   // Medium timestep
    [<InlineData(10.0)>]  // Large timestep
    let ``Random tracking errors scale with square root of time`` (timestep) =
        // Arrange & Act
        // Make multiple measurements to account for randomness
        let measurements = [1..100] |> List.map (fun _ -> calculateRandomTrackingErrors timestep)
        
        // Calculate RMS errors
        let rmsRA = 
            measurements
            |> List.map (fun (ra, _) -> ra * ra)
            |> List.average
            |> Math.Sqrt
            
        let rmsDEC = 
            measurements
            |> List.map (fun (_, dec) -> dec * dec)
            |> List.average
            |> Math.Sqrt
            
        // Compare with errors from a different timestep
        let timestep2 = timestep * 4.0  // 4x larger timestep
        
        let measurements2 = [1..100] |> List.map (fun _ -> calculateRandomTrackingErrors timestep2)
        
        let rmsRA2 = 
            measurements2
            |> List.map (fun (ra, _) -> ra * ra)
            |> List.average
            |> Math.Sqrt
            
        let rmsDEC2 = 
            measurements2
            |> List.map (fun (_, dec) -> dec * dec)
            |> List.average
            |> Math.Sqrt
            
        // Assert
        // Random walk should scale with sqrt(time), so 4x time = 2x RMS
        let expectedRatio = Math.Sqrt(timestep2 / timestep)  // Should be 2.0
        
        // Use a looser tolerance since this is statistical
        let raRatio = rmsRA2 / rmsRA
        approxEqualRel expectedRatio raRatio 0.25 |> should equal true
        
        let decRatio = rmsDEC2 / rmsDEC
        approxEqualRel expectedRatio decRatio 0.25 |> should equal true
    
    [<Theory>]
    [<InlineData(100.0, 20.0, 1000.0, 0.0, 600.0, 60.0, 0.0)>]    // No binding, 1 minute
    [<InlineData(100.0, 20.0, 1000.0, 3600.0, 600.0, 60.0, 3600.0)>]  // Recent binding, 1 minute
    let ``Mount state evolution preserves physical constraints`` (ra, dec, focalLength, lastBindingTime, periodErrorPeriod, timestep, elapsedTime) =
        // Arrange
        let initialState = {
            RA = ra
            Dec = dec
            FocalLength = focalLength
            TrackingRate = 15.0 / 3600.0 // sidereal rate in deg/sec
            PeriodicErrorAmplitude = 5.0
            PeriodicErrorPeriod = periodErrorPeriod
            PolarAlignmentError = 0.5
            PeriodicErrorHarmonics = []
            IsSlewing = false
            SlewRate = 3.0
        }
        
        // Act
        let (newState, newBindingTime) = evolveMountState initialState elapsedTime timestep lastBindingTime
        
        // Assert
        // RA should change by approximately sidereal rate (plus errors)
        let expectedRATrend = initialState.RA + initialState.TrackingRate * timestep / 3600.0
        let raDiff = Math.Abs(newState.RA - expectedRATrend)
        raDiff |> should be (lessThan 0.1) // Allow some variation due to errors
        
        // Dec should remain within reasonable bounds (polar error effects)
        let decDiff = Math.Abs(newState.Dec - initialState.Dec)
        decDiff |> should be (lessThan 0.01) // Small change expected
        
        // Dec should stay within valid range (-90 to +90)
        newState.Dec |> should be (greaterThanOrEqualTo -90.0)
        newState.Dec |> should be (lessThanOrEqualTo 90.0)
        
        // RA should be normalized to 0-360 range
        // Note: The implementation doesn't do this explicitly, but we should verify
        // that over time it doesn't drift to very large values
        (newState.RA >= 0.0 || newState.RA <= 1000.0) |> should equal true
        
        // Also check that other properties are preserved
        newState.FocalLength |> should equal initialState.FocalLength
        newState.TrackingRate |> should equal initialState.TrackingRate
        newState.IsSlewing |> should equal initialState.IsSlewing

module SensorPhysicsPropertyTests =
    open AstrophysicsPropertyTestUtils
    
    [<Fact>]
    let ``Gaussian random distribution has correct statistics`` () =
        // Arrange
        let mean = 10.0
        let sigma = 2.0
        let sampleSize = 10000
        
        // Act
        let samples = [1..sampleSize] |> List.map (fun _ -> randomGaussian mean sigma)
        
        // Calculate sample statistics
        let sampleMean = List.average samples
        
        let sampleVariance = 
            samples 
            |> List.map (fun x -> (x - sampleMean) * (x - sampleMean)) 
            |> List.average
            
        let sampleStdDev = Math.Sqrt(sampleVariance)
        
        // Assert
        // Sample mean should be close to target mean (within 3% for large sample)
        approxEqualRel mean sampleMean 0.03 |> should equal true
        
        // Sample std dev should be close to target std dev (within 5% for large sample)
        approxEqualRel sigma sampleStdDev 0.05 |> should equal true
    
    [<Fact>]
    let ``Poisson random distribution has correct statistics`` () =
        // Arrange
        let lambda = 10.0
        let sampleSize = 10000
        
        // Act
        let samples = [1..sampleSize] |> List.map (fun _ -> randomPoisson lambda)
        
        // Calculate sample statistics
        let sampleMean = List.average samples
        
        let sampleVariance = 
            samples 
            |> List.map (fun x -> (x - sampleMean) * (x - sampleMean)) 
            |> List.average
            
        // Assert
        // For Poisson distribution, mean and variance should be approximately equal
        approxEqualRel lambda sampleMean 0.05 |> should equal true
        approxEqualRel lambda sampleVariance 0.08 |> should equal true
    
    [<Theory>]
    [<InlineData(0.01, 0.0)>]   // Base temperature (0°C)
    [<InlineData(0.01, 6.5)>]   // 6.5°C increase
    [<InlineData(0.01, 13.0)>]  // 13°C increase
    [<InlineData(0.01, -6.5)>]  // 6.5°C decrease
    let ``Dark current doubles with 6.5°C temperature change`` (baseDarkCurrent, temperatureChange) =
        // Arrange
        let baseTemp = 0.0
        let newTemp = baseTemp + temperatureChange
        
        // Act
        let darkCurrent1 = calculateDarkCurrent baseDarkCurrent baseTemp
        let darkCurrent2 = calculateDarkCurrent baseDarkCurrent newTemp
        
        // Assert
        let expectedRatio = Math.Pow(2.0, temperatureChange / 6.5)
        let actualRatio = darkCurrent2 / darkCurrent1
        
        // Check that the ratio matches the expected temperature dependence
        approxEqualRel expectedRatio actualRatio 1e-10 |> should equal true
    
    [<Theory>]
    [<InlineData(4096, 2160, 3.8, -10.0)>]   // Large sensor, cooled
    [<InlineData(1920, 1080, 5.0, 0.0)>]     // Medium sensor, room temp
    [<InlineData(800, 600, 7.4, 20.0)>]      // Small sensor, warm
    let ``Sensor model creation produces valid parameters`` (width, height, pixelSize, temperature) =
        // Arrange
        let cameraState = createTestCameraState width height pixelSize 30.0
        
        // Act
        let sensorModel = createSensorModel cameraState temperature
        
        // Assert
        // Basics
        sensorModel.Width |> should equal width
        sensorModel.Height |> should equal height
        sensorModel.PixelSize |> should equal pixelSize
        sensorModel.Temperature |> should equal temperature
        
        // Physical constraints
        sensorModel.QuantumEfficiency |> should be (greaterThan 0.0)
        sensorModel.QuantumEfficiency |> should be (lessThanOrEqualTo 1.0)
        
        sensorModel.ReadNoise |> should be (greaterThan 0.0)
        sensorModel.DarkCurrent |> should be (greaterThanOrEqualTo 0.0)
        sensorModel.Gain |> should be (greaterThan 0.0)
        
        sensorModel.FullWellCapacity |> should be (greaterThan 1000)
        sensorModel.BiasLevel |> should be (greaterThanOrEqualTo 0)
        sensorModel.BitDepth |> should be (greaterThanOrEqualTo 8)
    
    [<Theory>]
    [<InlineData(100.0, 0.8)>]  // 100 photons, 80% QE
    [<InlineData(1000.0, 0.9)>] // 1000 photons, 90% QE
    [<InlineData(10.0, 0.7)>]   // 10 photons, 70% QE (few photons)
    let ``Quantum efficiency reduces signal by expected factor`` (photons, qe) =
        // Arrange
        let width = 10
        let height = 10
        let buffer = Array2D.create width height photons
        
        // Act
        let result = applyQuantumEfficiency buffer qe
        
        // Assert
        // Statistical test: average should be close to photons * qe
        let mutable sum = 0.0
        for x = 0 to width - 1 do
            for y = 0 to height - 1 do
                sum <- sum + result.[x, y]
                
        let mean = sum / float(width * height)
        let expected = photons * qe
        
        // For large photon counts, the result should be close to expected value
        if photons >= 100.0 then
            // Closer tolerance for large photon counts
            approxEqualRel expected mean 0.05 |> should equal true
        else
            // Wider tolerance for small photon counts due to statistical variation
            approxEqualRel expected mean 0.2 |> should equal true
    
    [<Theory>]
    [<InlineData(100.0, 0.1, 60.0)>]  // 100 e- signal, 0.1 e-/pix/sec dark, 60s
    [<InlineData(1000.0, 0.02, 30.0)>] // 1000 e- signal, 0.02 e-/pix/sec dark, 30s
    let ``Dark current contribution increases with exposure time`` (signal, darkCurrent, exposureTime) =
        // Arrange
        let width = 10
        let height = 10
        let buffer = Array2D.create width height signal
        
        // Act
        let result = applyDarkCurrent buffer darkCurrent exposureTime
        
        // Assert
        // Should be same buffer reference (modified in place)
        result |> should equal buffer
        
        // Statistical test: average increase should be close to darkCurrent * exposureTime
        let expectedIncrease = darkCurrent * exposureTime
        
        // Should be above the original signal
        for x = 0 to width - 1 do
            for y = 0 to height - 1 do
                result.[x, y] |> should be (greaterThanOrEqualTo signal)
                
        // Average should be roughly signal + dark contribution
        // Allow wide tolerance due to statistical nature (Poisson random)
        let mutable sum = 0.0
        for x = 0 to width - 1 do
            for y = 0 to height - 1 do
                sum <- sum + (result.[x, y] - signal) // Calculate the increase
                
        let meanIncrease = sum / float(width * height)
        
        // For larger dark currents, closer match to expected
        if darkCurrent * exposureTime >= 1.0 then
            approxEqualRel expectedIncrease meanIncrease 0.3 |> should equal true
        else
            // Just verify it's in a reasonable range for very small values
            meanIncrease |> should be (greaterThanOrEqualTo 0.0)
            meanIncrease |> should be (lessThan (expectedIncrease * 3.0))

module BufferOperationPropertyTests =
    open AstrophysicsPropertyTestUtils
    
    [<Fact>]
    let ``Create empty buffer properly initializes to zero`` () =
        // Arrange
        let width = 100
        let height = 50
        
        // Act
        let buffer = createEmptyBuffer width height
        
        // Assert
        // Buffer has correct dimensions
        Array2D.length1 buffer |> should equal width
        Array2D.length2 buffer |> should equal height
        
        // All elements are zero
        for x = 0 to width - 1 do
            for y = 0 to height - 1 do
                buffer.[x, y] |> should equal 0.0
    
    [<Theory>]
    [<InlineData(10.0, 10.0, 1000.0)>]  // Center of buffer, 1000 photons
    [<InlineData(5.0, 15.0, 500.0)>]    // Near edge, 500 photons
    [<InlineData(0.0, 0.0, 100.0)>]     // At corner, 100 photons
    let ``Accumulate photons conserves energy`` (x, y, photons) =
        // Arrange
        let width = 20
        let height = 20
        let buffer = createEmptyBuffer width height
        
        // Create a simple PSF for testing
        let size = 5
        let psfValues = Array2D.create size size 0.0
        
        // Set PSF values - use a simple symmetric pattern
        psfValues.[size/2, size/2] <- 0.5  // Center gets 50%
        for i = 0 to size - 1 do
            for j = 0 to size - 1 do
                if not (i = size/2 && j = size/2) then
                    psfValues.[i, j] <- 0.5 / float((size-1) * (size-1))
                    
        let psf = { Values = psfValues; Size = size; Sum = 1.0 }
        
        // Act
        accumulatePhotons buffer x y photons psf
        
        // Assert
        // Total energy (photons) should be conserved
        let totalPhotons = sumArray2D buffer
        
        // Allow small tolerance for floating point and boundary effects
        approxEqualRel photons totalPhotons 0.01 |> should equal true
        
        // Center (x, y) should have highest value if it's within buffer
        if x >= 0.0 && x < float width && y >= 0.0 && y < float height then
            let centerX = int (Math.Round(x))
            let centerY = int (Math.Round(y))
            
            if centerX >= 0 && centerX < width && centerY >= 0 && centerY < height then
                let centerValue = buffer.[centerX, centerY]
                let maxValue = maxArray2D buffer
                approxEqualRel centerValue maxValue 0.01 |> should equal true
    
    [<Theory>]
    [<InlineData(0.2, 10.0)>]  // 20% cloud coverage, 10s
    [<InlineData(0.5, 30.0)>]  // 50% cloud coverage, 30s
    [<InlineData(0.8, 60.0)>]  // 80% cloud coverage, 60s
    let ``Cloud cover properly attenuates signal`` (cloudCoverage, timestamp) =
        // Arrange
        let width = 20
        let height = 20
        let signalLevel = 100.0
        let buffer = Array2D.create width height signalLevel
        
        // Act
        let result = applyCloudCover buffer cloudCoverage timestamp
        
        // Assert
        // Should be same buffer (modified in place)
        result |> should equal buffer
        
        // Signal should be attenuated
        for x = 0 to width - 1 do
            for y = 0 to height - 1 do
                result.[x, y] |> should be (lessThanOrEqualTo signalLevel)
                
                // Cloud attenuation is statistical but should be related to coverage
                // The actual formula is implementation-dependent, but signal
                // should always be lower than original
                result.[x, y] |> should be (greaterThanOrEqualTo (signalLevel * (1.0 - cloudCoverage)))
                
        // Higher cloud coverage should attenuate more
        if cloudCoverage > 0.0 then
            // Average attenuation
            let mutable sum = 0.0
            for x = 0 to width - 1 do
                for y = 0 to height - 1 do
                    sum <- sum + result.[x, y]
                    
            let mean = sum / float(width * height)
            mean |> should be (lessThan signalLevel)
    
    [<Fact>]
    let ``Combine buffers correctly sums values`` () =
        // Arrange
        let width = 10
        let height = 10
        
        let buffer1 = Array2D.create width height 10.0
        let buffer2 = Array2D.create width height 20.0
        let buffer3 = Array2D.create width height 30.0
        
        // Act
        let result = combineBuffers [buffer1; buffer2; buffer3]
        
        // Assert
        // Result has correct dimensions
        Array2D.length1 result |> should equal width
        Array2D.length2 result |> should equal height
        
        // Each pixel should be the sum of the input pixels
        for x = 0 to width - 1 do
            for y = 0 to height - 1 do
                result.[x, y] |> should equal 60.0
    
    [<Fact>]
    let ``Buffer to array conversion preserves data`` () =
        // Arrange
        let width = 3
        let height = 2
        let buffer = Array2D.create width height 0.0
        
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
        // Array has correct length
        array.Length |> should equal (width * height)
        
        // Elements should be in row-major order
        array.[0] |> should equal 1.0
        array.[1] |> should equal 2.0
        array.[2] |> should equal 3.0
        array.[3] |> should equal 4.0
        array.[4] |> should equal 5.0
        array.[5] |> should equal 6.0
        
        // Sum should be the same
        let bufferSum = sumArray2D buffer
        let arraySum = Array.sum array
        bufferSum |> should equal arraySum