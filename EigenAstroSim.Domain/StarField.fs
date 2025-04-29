namespace EigenAstroSim.Domain

module Types =
    /// Represents a single star in the virtual sky
    type Star = {
        Id: System.Guid
        RA: float  // Right Ascension in degrees
        Dec: float // Declination in degrees
        Magnitude: float  // Visual magnitude
        Color: float  // B-V color index
    }

    /// Region in the sky
    type SkyRegion = {
        CenterRA: float
        CenterDec: float
        Radius: float
    }

    /// Represents the current state of the star field
    type StarFieldState = {
        Stars: Map<System.Guid, Star>
        ReferenceRA: float
        ReferenceDec: float
        ReferenceRotation: float
    }

module StarField =
    open Types
    
    /// Create a new empty star field
    let createEmpty referenceRA referenceDec =
        {
            Stars = Map.empty
            ReferenceRA = referenceRA
            ReferenceDec = referenceDec
            ReferenceRotation = 0.0
        }

module StarFieldGenerator =
    open Types
    open System
    
    /// Convert degrees to radians
    let private toRadians degrees = degrees * Math.PI / 180.0
    
    /// Convert radians to degrees
    let private toDegrees radians = radians * 180.0 / Math.PI
    
    /// Calculate the galactic latitude from RA/Dec coordinates
    let calculateGalacticLatitude ra dec =
        // Simplified calculation for testing purposes
        // Real implementation would use proper astronomical formulas
        let raRad = toRadians ra
        let decRad = toRadians dec
        
        // North Galactic Pole position (J2000)
        let ngpRA = toRadians 192.859508
        let ngpDec = toRadians 27.128336
        
        let sinb = 
            Math.Sin(decRad) * Math.Sin(ngpDec) + 
            Math.Cos(decRad) * Math.Cos(ngpDec) * Math.Cos(raRad - ngpRA)
        
        toDegrees (Math.Asin(sinb))
    
    /// Generate a star magnitude following realistic distribution
    let generateStarMagnitude minMag maxMag (random: Random) =
        // Stars follow approximately a power law distribution
        // Simplified model: Number of stars increases by ~4x per magnitude
        let exponent = 0.6  // log₁₀(4) ≈ 0.6
        
        // Generate uniform random number between 0 and 1
        let u = random.NextDouble()
        
        // Transform according to power law
        let magRange = maxMag - minMag
        let mag = minMag + magRange * Math.Pow(u, 1.0 / exponent)
        
        mag
    
    /// Generate a realistic star color based on magnitude
    let generateStarColor magnitude (random: Random) =
        // Simplified model: Brighter stars tend to be bluer
        // B-V color index typically ranges from -0.3 (blue) to 2.0 (red)
        
        // Base tendency: brighter stars are bluer
        let baseBV = -0.3 + (magnitude / 6.0) * 2.3
        
        // Add some randomness
        let randomComponent = (random.NextDouble() - 0.5) * 0.5
        
        // Constrain to realistic range
        Math.Max(-0.3, Math.Min(2.0, baseBV + randomComponent))
    
    /// Check if a star with similar position already exists
    let starExists (stars: Map<Guid, Star>) ra dec toleranceDegrees =
        stars 
        |> Map.exists (fun _ star -> 
            let deltaRA = Math.Abs(star.RA - ra) * Math.Cos(toRadians dec)
            let deltaDec = Math.Abs(star.Dec - dec)
            let distance = Math.Sqrt(deltaRA * deltaRA + deltaDec * deltaDec)
            distance < toleranceDegrees)
    
    /// Calculate what region is covered by existing stars
    let calculateCoveredRegion (stars: Map<Guid, Star>) =
        if Map.isEmpty stars then
            None
        else
            let allStars = stars |> Map.toSeq |> Seq.map snd |> Seq.toArray
            
            // Find min/max coordinates
            let minRA = allStars |> Array.map (fun s -> s.RA) |> Array.min
            let maxRA = allStars |> Array.map (fun s -> s.RA) |> Array.max
            let minDec = allStars |> Array.map (fun s -> s.Dec) |> Array.min
            let maxDec = allStars |> Array.map (fun s -> s.Dec) |> Array.max
            
            // Calculate center
            let centerRA = (minRA + maxRA) / 2.0
            let centerDec = (minDec + maxDec) / 2.0
            
            // Calculate radius (distance from center to furthest corner)
            let deltaRA = (maxRA - minRA) / 2.0
            let deltaDec = (maxDec - minDec) / 2.0
            let radius = Math.Sqrt(deltaRA * deltaRA + deltaDec * deltaDec)
            
            Some {
                CenterRA = centerRA
                CenterDec = centerDec
                Radius = radius
            }
    
    /// Check if region1 fully contains region2
    let regionContains region1 region2 =
        match region1 with
        | None -> false
        | Some r1 ->
            // Calculate distance between centers
            let deltaRA = (r1.CenterRA - region2.CenterRA) * Math.Cos(toRadians region2.CenterDec)
            let deltaDec = r1.CenterDec - region2.CenterDec
            let centerDistance = Math.Sqrt(deltaRA * deltaRA + deltaDec * deltaDec)
            
            // Region1 contains region2 if distance between centers plus region2 radius
            // is less than region1 radius
            centerDistance + region2.Radius <= r1.Radius
    
    /// Generate a realistic distribution of stars
    let generateStarField centerRA centerDec fieldRadius limitingMagnitude (random: Random) =
        // Calculate star density based on galactic latitude
        let galacticLatitude = calculateGalacticLatitude centerRA centerDec
        let densityFactor = Math.Exp(-Math.Abs(galacticLatitude) / 30.0)
        
        // Approximate number of stars in field based on limiting magnitude
        // Using a much smaller base coefficient (1.0 instead of 100.0) to get reasonable star counts
        let starCount = 
            int (densityFactor * 1.0 * 
                fieldRadius * fieldRadius * 
                Math.Pow(10.0, 0.4 * (limitingMagnitude - 6.0)))
        
        // Generate stars with realistic distribution
        [| for _ in 1 .. starCount do
               // Position within field (random but uniform distribution)
               let r = fieldRadius * Math.Sqrt(random.NextDouble())
               let theta = random.NextDouble() * 2.0 * Math.PI
               
               let ra = centerRA + r * Math.Cos(theta) / Math.Cos(toRadians centerDec)
               let dec = centerDec + r * Math.Sin(theta)
               
               // Magnitude follows a power law distribution
               let mag = generateStarMagnitude 1.0 limitingMagnitude random
               
               // Color correlates with magnitude
               let color = generateStarColor mag random
               
               yield {
                   Id = Guid.NewGuid()
                   RA = ra
                   Dec = dec
                   Magnitude = mag
                   Color = color
               }
        |]
    
    /// Expand the star field when telescope moves to a new area
    let expandStarField (starField: StarFieldState) centerRA centerDec fieldRadius limitingMagnitude (random: Random) =
        // Check what area is covered by existing stars
        let existingRegion = calculateCoveredRegion starField.Stars
        let newRegion = {
            CenterRA = centerRA
            CenterDec = centerDec
            Radius = fieldRadius
        }
        
        if regionContains existingRegion newRegion then
            // New region is already covered
            starField
        else
            // Generate additional stars to cover the new region
            let additionalStars = 
                generateStarField centerRA centerDec fieldRadius limitingMagnitude random
                |> Array.filter (fun star -> not (starExists starField.Stars star.RA star.Dec 0.01))
            
            // Add new stars to the existing collection
            let updatedStars = 
                (starField.Stars, additionalStars) 
                ||> Array.fold (fun map star -> Map.add star.Id star map)
            
            { starField with 
                Stars = updatedStars 
                ReferenceRA = centerRA
                ReferenceDec = centerDec
            }