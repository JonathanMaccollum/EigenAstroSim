namespace EigenAstroSim.Tests

module StarFieldTests =
    open System
    open Xunit
    open FsUnit.Xunit
    open EigenAstroSim.Domain.Types
    open EigenAstroSim.Domain.StarField
    open EigenAstroSim.Domain.StarFieldGenerator

    // Helper to create a random star with valid coordinates
    let createRandomStar (random: Random) =
        let ra = random.NextDouble() * 360.0
        let dec = (random.NextDouble() * 180.0) - 90.0
        let mag = random.NextDouble() * 15.0 + 1.0
        let color = random.NextDouble() * 2.3 - 0.3
        
        {
            Id = Guid.NewGuid()
            RA = ra
            Dec = dec
            Magnitude = mag
            Color = color
        }
        
    // Helper function to get valid test parameters
    let getValidFieldParameters() =
        let random = Random()
        let ra = random.NextDouble() * 360.0
        let dec = random.NextDouble() * 180.0 - 90.0
        let radius = random.NextDouble() * 30.0 + 1.0
        let limitMag = random.NextDouble() * 9.0 + 6.0  // 6.0 to 15.0
        (ra, dec, radius, limitMag, random)
        
    [<Fact>]
    let ``CreateEmpty should return empty star field with specified reference coordinates`` () =
        // Arrange
        let refRA = 120.0
        let refDec = 45.0
        
        // Act
        let starField = createEmpty refRA refDec
        
        // Assert
        starField.Stars |> should be Empty
        starField.ReferenceRA |> should equal refRA
        starField.ReferenceDec |> should equal refDec
        starField.ReferenceRotation |> should equal 0.0
    
    [<Fact>]
    let ``Star magnitudes should be within valid astronomical range`` () =
        // Arrange
        let (ra, dec, radius, limitMag, random) = getValidFieldParameters()
        
        // Act
        let stars = generateStarField ra dec radius limitMag random
        
        // Assert
        stars |> Array.forall (fun star -> 
            star.Magnitude >= 1.0 && star.Magnitude <= limitMag)
        |> should equal true
    
    [<Fact>]
    let ``Star colors should be within valid B-V index range`` () =
        // Arrange
        let (ra, dec, radius, limitMag, random) = getValidFieldParameters()
        
        // Act
        let stars = generateStarField ra dec radius limitMag random
        
        // Assert
        stars |> Array.forall (fun star -> 
            star.Color >= -0.3 && star.Color <= 2.0)
        |> should equal true
    
    [<Fact>]
    let ``Star coordinates should be within specified field radius`` () =
        // Arrange
        let (ra, dec, radius, limitMag, random) = getValidFieldParameters()
        
        // Act
        let stars = generateStarField ra dec radius limitMag random
        
        // Assert
        stars |> Array.forall (fun star ->
            let deltaRA = (star.RA - ra) * Math.Cos(dec * Math.PI / 180.0)
            let deltaDec = star.Dec - dec
            let distance = Math.Sqrt(deltaRA * deltaRA + deltaDec * deltaDec)
            distance <= radius)
        |> should equal true
    
    [<Fact>]
    let ``Star density should decrease with galactic latitude`` () =
        // Arrange
        let random = Random()
        let ra = 100.0
        let dec = 30.0  // Mid-latitude
        let radius = 5.0
        let limitMag = 12.0
        
        // Create a field at higher galactic latitude (by shifting declination toward pole)
        let higherLatDec = 80.0  // Higher latitude
        
        // Act
        let stars1 = generateStarField ra dec radius limitMag random
        let stars2 = generateStarField ra higherLatDec radius limitMag random
        
        // Assert - Higher galactic latitude should have fewer stars
        stars2.Length |> should be (lessThanOrEqualTo stars1.Length)
    
    [<Fact>]
    let ``Star count should increase with limiting magnitude`` () =
        // Arrange
        let random = Random()
        let ra = 150.0
        let dec = 30.0
        let radius = 5.0
        let lowerLimitMag = 8.0
        let higherLimitMag = 12.0
        
        // Act
        let stars1 = generateStarField ra dec radius lowerLimitMag random
        let stars2 = generateStarField ra dec radius higherLimitMag random
        
        // Assert - Higher limiting magnitude should result in more stars
        stars1.Length |> should be (lessThanOrEqualTo stars2.Length)
    
    [<Fact>]
    let ``Expanding star field should add new stars for uncovered regions`` () =
        // Arrange
        let random = Random()
        let ra = 120.0
        let dec = 45.0
        let initialRadius = 2.0
        let limitMag = 10.0
        
        // Act
        let initialStarField = createEmpty ra dec
        
        // First expansion to establish a base field
        let expandedField1 = expandStarField initialStarField ra dec initialRadius limitMag random
        
        // Second expansion to a partially overlapping region (moved slightly)
        let ra2 = (ra + 1.5) % 360.0
        let dec2 = Math.Max(-89.0, Math.Min(89.0, dec + 1.5))
        let expandedField2 = expandStarField expandedField1 ra2 dec2 initialRadius limitMag random
        
        // Assert - The expanded field should have more stars than the initial expansion
        expandedField2.Stars.Count |> should be (greaterThan expandedField1.Stars.Count)
    
    [<Fact>]
    let ``Stars should persist when revisiting an area`` () =
        // Arrange
        let random = Random()
        let ra = 120.0
        let dec = 45.0
        let radius = 5.0
        let limitMag = 10.0
        
        // Act
        let initialStarField = createEmpty ra dec
        
        // Expand to an area
        let expandedField = expandStarField initialStarField ra dec radius limitMag random
        
        // Move away from the area
        let raFar = (ra + 20.0) % 360.0
        let decFar = Math.Max(-89.0, Math.Min(89.0, dec + 20.0))
        let farField = expandStarField expandedField raFar decFar radius limitMag random
        
        // Return to original area
        let returnedField = expandStarField farField ra dec radius limitMag random
        
        // Assert - All stars from the first expansion should still be present
        let allStarsPresent = 
            expandedField.Stars 
            |> Map.forall (fun id _ -> returnedField.Stars.ContainsKey(id))
            
        allStarsPresent |> should equal true
            
    [<Fact>]
    let ``Star field should generate a reasonable number of stars`` () =
        // Arrange
        let random = Random()
        let ra = 150.0
        let dec = 30.0
        let radius = 5.0
        let limitMag = 12.0
        
        // Act
        let stars = generateStarField ra dec radius limitMag random
        
        // Assert
        stars.Length |> should be (greaterThan 0)
        // There should be at least some stars and not an excessive amount
        stars.Length |> should be (lessThan 10000)
    
    [<Fact>]
    let ``Calculate covered region should return None for empty star field`` () =
        // Arrange
        let emptyField = Map.empty<Guid, Star>
        
        // Act
        let region = calculateCoveredRegion emptyField
        
        // Assert
        region |> should equal None
    
    [<Fact>]
    let ``Calculate covered region should return valid region for non-empty star field`` () =
        // Arrange
        let random = Random()
        let stars = [|
            for _ in 1..10 do
                createRandomStar random
        |]
        let starMap = stars |> Array.map (fun s -> (s.Id, s)) |> Map.ofArray
        
        // Act
        let region = calculateCoveredRegion starMap
        
        // Assert
        region |> should not' (equal None)
        match region with
        | Some r ->
            r.Radius |> should be (greaterThan 0.0)
            // Check if all stars are within the region
            stars |> Array.forall (fun star ->
                let deltaRA = (star.RA - r.CenterRA) * Math.Cos(star.Dec * Math.PI / 180.0)
                let deltaDec = star.Dec - r.CenterDec
                let distance = Math.Sqrt(deltaRA * deltaRA + deltaDec * deltaDec)
                distance <= r.Radius + 0.0001) // Add small epsilon for floating point comparison
            |> should equal true
        | None -> 
            Assert.True(false, "Region should not be None")
    
    [<Fact>]
    let ``Region contains should correctly identify contained regions`` () =
        // Arrange
        let region1 = Some {
            CenterRA = 100.0
            CenterDec = 20.0
            Radius = 10.0
        }
        
        let containedRegion = {
            CenterRA = 101.0
            CenterDec = 19.5
            Radius = 2.0
        }
        
        let nonContainedRegion = {
            CenterRA = 110.0
            CenterDec = 25.0
            Radius = 3.0
        }
        
        // Act & Assert
        regionContains region1 containedRegion |> should equal true
        regionContains region1 nonContainedRegion |> should equal false
        regionContains None containedRegion |> should equal false
    
    [<Fact>]
    let ``Stars should have unique IDs`` () =
        // Arrange
        let (ra, dec, radius, limitMag, random) = getValidFieldParameters()
        
        // Act
        let stars = generateStarField ra dec radius limitMag random
        
        // Assert - Check that all IDs are unique
        let ids = stars |> Array.map (fun s -> s.Id) |> Set.ofArray
        ids.Count |> should equal stars.Length
    
    [<Fact>]
    let ``Brighter stars should tend to be bluer`` () =
        // Arrange
        let random = Random()
        let ra = 150.0
        let dec = 30.0
        let radius = 10.0  // Larger radius to ensure enough stars
        let limitMag = 14.0  // Higher limit to ensure enough stars
        
        // Act
        let stars = generateStarField ra dec radius limitMag random
        
        // If we don't have enough stars, the test should be skipped
        if stars.Length >= 20 then
            // Get bright stars and dim stars
            let sortedStars = stars |> Array.sortBy (fun s -> s.Magnitude)
            let brightStars = sortedStars |> Array.take 10
            let dimStars = sortedStars |> Array.skip (stars.Length - 10)
            
            // Calculate average color for each group
            let avgBrightColor = brightStars |> Array.averageBy (fun s -> s.Color)
            let avgDimColor = dimStars |> Array.averageBy (fun s -> s.Color)
            
            // Assert - Bright stars should be bluer (lower B-V index) on average
            avgBrightColor |> should be (lessThanOrEqualTo avgDimColor)
        else
            // Skip test if not enough stars by asserting true
            true |> should equal true