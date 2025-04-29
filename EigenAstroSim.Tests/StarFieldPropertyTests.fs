namespace EigenAstroSim.Tests

module PropertyBasedTests =
    open System
    open Xunit
    open FsUnit.Xunit
    open EigenAstroSim.Domain.Types
    open EigenAstroSim.Domain.StarField
    open EigenAstroSim.Domain.StarFieldGenerator

    // Helper to generate random astronomical values
    let random = Random()
    
    let randomRA() = random.NextDouble() * 360.0
    let randomDec() = random.NextDouble() * 180.0 - 90.0
    let randomRadius() = random.NextDouble() * 29.0 + 1.0 // 1.0 to 30.0
    let randomLimitMag() = random.NextDouble() * 9.0 + 6.0 // 6.0 to 15.0
    
    // Test multiple property instances (similar to property-based testing)
    let testProperty iterations property =
        for _ in 1..iterations do
            let result = property()
            result |> should equal true

    // Helper to generate a random set of parameters
    let randomParameters() =
        (randomRA(), randomDec(), randomRadius(), randomLimitMag())
    
    [<Fact>]
    let ``Star magnitudes should be within valid astronomical range`` () =
        testProperty 10 (fun () ->
            let (ra, dec, radius, limitMag) = randomParameters()
            
            // Generate a star field
            let stars = generateStarField ra dec radius limitMag random
            
            // Check that all star magnitudes are within valid range
            stars |> Array.forall (fun star -> 
                star.Magnitude >= 1.0 && star.Magnitude <= limitMag)
        )
    
    [<Fact>]
    let ``Star colors should be within valid B-V index range`` () =
        testProperty 10 (fun () ->
            let (ra, dec, radius, limitMag) = randomParameters()
            
            // Generate a star field
            let stars = generateStarField ra dec radius limitMag random
            
            // Check that all star colors are within valid B-V index range (-0.3 to 2.0)
            stars |> Array.forall (fun star -> 
                star.Color >= -0.3 && star.Color <= 2.0)
        )
    
    [<Fact>]
    let ``Star coordinates should be within specified field radius`` () =
        testProperty 10 (fun () ->
            let (ra, dec, radius, limitMag) = randomParameters()
            
            // Generate a star field
            let stars = generateStarField ra dec radius limitMag random
            
            // Check that all stars are within the specified field radius
            stars |> Array.forall (fun star ->
                let deltaRA = (star.RA - ra) * Math.Cos(dec * Math.PI / 180.0)
                let deltaDec = star.Dec - dec
                let distance = Math.Sqrt(deltaRA * deltaRA + deltaDec * deltaDec)
                distance <= radius)
        )
    
    [<Fact>]
    let ``Stars should have unique IDs`` () =
        testProperty 10 (fun () ->
            let (ra, dec, radius, limitMag) = randomParameters()
            
            // Generate a star field
            let stars = generateStarField ra dec radius limitMag random
            
            // Check that all IDs are unique
            let ids = stars |> Array.map (fun s -> s.Id) |> Set.ofArray
            ids.Count = stars.Length
        )
    
    [<Fact>]
    let ``Higher limiting magnitude should result in more stars`` () =
        testProperty 5 (fun () ->
            let ra = randomRA()
            let dec = randomDec()
            let radius = 5.0 // Fixed radius
            
            let lowerMag = 8.0
            let higherMag = 12.0
            
            let starsWithLowerMag = generateStarField ra dec radius lowerMag random
            let starsWithHigherMag = generateStarField ra dec radius higherMag random
            
            starsWithLowerMag.Length <= starsWithHigherMag.Length
        )
    
    [<Fact>]
    let ``Stars persist when revisiting an area`` () =
        testProperty 5 (fun () ->
            let ra = randomRA()
            let dec = randomDec()
            
            let radius = 5.0
            let limitMag = 10.0
            
            // Create and expand the initial field
            let initialField = createEmpty ra dec
            let expandedField = expandStarField initialField ra dec radius limitMag random
            
            // Move away to a different area
            let farRa = (ra + 20.0) % 360.0
            let farDec = Math.Max(-89.0, Math.Min(89.0, dec + 20.0))
            let farField = expandStarField expandedField farRa farDec radius limitMag random
            
            // Return to the original area
            let returnedField = expandStarField farField ra dec radius limitMag random
            
            // All original stars should still be present
            expandedField.Stars |> Map.forall (fun id _ -> returnedField.Stars.ContainsKey(id))
        )
        
    [<Fact>]
    let ``Star density should decrease with galactic latitude`` () =
        testProperty 5 (fun () ->
            // Use fixed coordinates where we KNOW the galactic latitude relationship
            // Point near galactic equator (low galactic latitude)
            let lowLatRA = 282.0
            let lowLatDec = 0.0
            
            // Point near galactic pole (high galactic latitude)
            let highLatRA = 192.85
            let highLatDec = 27.13
            
            let radius = 5.0
            let limitMag = 10.0
            
            let lowLatStars = generateStarField lowLatRA lowLatDec radius limitMag random
            let highLatStars = generateStarField highLatRA highLatDec radius limitMag random
            
            // Higher galactic latitude should have fewer stars
            highLatStars.Length <= lowLatStars.Length
        )