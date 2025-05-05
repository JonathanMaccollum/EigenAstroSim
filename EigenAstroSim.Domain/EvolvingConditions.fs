namespace EigenAstroSim.Domain.VirtualSensorSimulation

open System
open EigenAstroSim.Domain
open EigenAstroSim.Domain.Types

/// Models how conditions evolve over time during an exposure
module EvolvingConditions =
    /// Random number generator
    let mutable random = Random()
    
    /// Generate a random value with Gaussian distribution
    let private randomGaussian (mean: float) (sigma: float) =
        // Box-Muller transform
        let u1 = random.NextDouble()
        let u2 = random.NextDouble()
        
        let z0 = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2)
        mean + sigma * z0
        
    /// Simulate how atmospheric seeing evolves over time
    let evolveSeeingCondition 
            (initialSeeing: float) 
            (timestep: float) 
            (elapsedTime: float) 
            (totalDuration: float) : float =
        
        // Real atmospheric seeing has temporal correlation
        // - Short-term: rapid seeing fluctuations (seconds)
        // - Medium-term: seeing trends (minutes)
        // - Long-term: overall seeing conditions (hours)
        
        // Coefficient for seeing variation (larger = more variable)
        let variabilityCoefficient = 0.1
        
        // Calculate time-based trends
        let trendFactor = 
            Math.Sin(elapsedTime / 60.0 * Math.PI) * 0.05 + // ~5% sinusoidal variation over minutes
            Math.Sin(elapsedTime / 300.0 * Math.PI * 2.0) * 0.03 // ~3% faster oscillation
            
        // Add random component with temporal correlation
        // Short timesteps = small random changes, correlated with previous changes
        let randomComponent = randomGaussian 0.0 (variabilityCoefficient * Math.Sqrt(timestep))
        
        // Calculate new seeing value with constraints
        let newSeeing = initialSeeing * (1.0 + trendFactor + randomComponent)
        
        // Ensure seeing stays within reasonable bounds (0.5" to 5")
        Math.Max(0.5, Math.Min(4.999, newSeeing))
        
    /// Simulate how cloud coverage evolves over time
    let evolveCloudCoverage 
            (initialCoverage: float) 
            (timestep: float) 
            (elapsedTime: float) 
            (totalDuration: float) : float =
        
        // Cloud movement tends to be smoother and more directional
        // Usually clouds either move in or move out during an exposure
        
        // Determine overall trend (increasing or decreasing)
        let trendfactor = 
            let phase = (random.NextDouble() * 2.0 - 1.0) * Math.PI // Random phase
            Math.Sin(elapsedTime / totalDuration * Math.PI + phase) * 0.2
            
        // Add small random variations
        let randomComponent = randomGaussian 0.0 (0.05 * Math.Sqrt(timestep))
        
        // Calculate new coverage with constraints
        let newCoverage = initialCoverage + trendfactor * timestep / totalDuration + randomComponent
        
        // Ensure coverage stays within bounds [0, 1]
        Math.Max(0.0, Math.Min(1.0, newCoverage))
        
    /// Simulate how mount tracking errors evolve over time
    let evolveTrackingError 
            (mountState: MountState) 
            (timestep: float) 
            (elapsedTime: float) : float * float =
        
        // Calculate periodic error component
        // Periodic error follows a sinusoidal pattern with the worm period
        let periodicRA = 
            if mountState.PeriodicErrorPeriod > 0.0 then
                let phase = elapsedTime / mountState.PeriodicErrorPeriod * 2.0 * Math.PI
                mountState.PeriodicErrorAmplitude * Math.Sin(phase)
            else
                0.0
                
        // Add random errors (wind gusts, mount vibrations, etc)
        let randomRA = randomGaussian 0.0 0.2
        let randomDec = randomGaussian 0.0 0.1  // Dec errors usually smaller
        
        // Polar alignment error causes field rotation and drift
        // This is a simplified model - real field rotation is more complex
        let polarAlignmentRA = 
            if mountState.PolarAlignmentError > 0.0 then
                let hourAngle = elapsedTime / 3600.0 * 15.0  // Convert to degrees
                mountState.PolarAlignmentError * Math.Sin(hourAngle * Math.PI / 180.0)
            else
                0.0
                
        let polarAlignmentDec = 
            if mountState.PolarAlignmentError > 0.0 then
                let hourAngle = elapsedTime / 3600.0 * 15.0  // Convert to degrees
                mountState.PolarAlignmentError * Math.Cos(hourAngle * Math.PI / 180.0)
            else
                0.0
        
        // Random tracking irregularities (e.g., cable snags, gear teeth)
        // These are rare but can cause sudden jumps
        let trackingIrregularity = 
            if random.NextDouble() < 0.01 * timestep then  // ~1% chance per second
                (randomGaussian 0.0 1.0, randomGaussian 0.0 0.5)
            else
                (0.0, 0.0)
                
        // Combine all error sources
        let totalRAError = periodicRA + randomRA + polarAlignmentRA + fst trackingIrregularity
        let totalDecError = randomDec + polarAlignmentDec + snd trackingIrregularity
        
        (totalRAError, totalDecError)
        
    /// Generate atmospheric jitter for a specific time
    let generateAtmosphericJitter 
            (seeing: float)  // in arcseconds  
            (timestamp: float) : float * float =
        
        // Atmospheric jitter depends on:
        // - Seeing conditions (larger seeing = larger jitter)
        // - Telescope aperture (not modeled here - partial aperture averaging)
        // - Exposure time (short exposures have more jitter variation)
        
        // Short-timescale jitter (tip-tilt component of atmospheric turbulence)
        // Follows Kolmogorov statistics
        let jitterScale = seeing / 5.0  // Scale factor for jitter amplitude
        
        // Use multiple frequencies for more realistic turbulence
        let jitterX = 
            jitterScale * (
                Math.Sin(timestamp * 10.0) * 0.5 +
                Math.Sin(timestamp * 25.0 + 1.3) * 0.3 +
                Math.Sin(timestamp * 40.0 + 2.7) * 0.2
            )
            
        let jitterY = 
            jitterScale * (
                Math.Cos(timestamp * 12.0) * 0.5 +
                Math.Cos(timestamp * 28.0 + 0.8) * 0.3 +
                Math.Cos(timestamp * 45.0 + 3.1) * 0.2
            )
            
        (jitterX, jitterY)
        
    /// Create evolved mount state for a specific subframe
    let createEvolvedMountState 
            (initialMount: MountState) 
            (timestep: float) 
            (elapsedTime: float) : MountState =
        
        // Calculate tracking error for this timestep
        let (raError, decError) = evolveTrackingError initialMount timestep elapsedTime
        
        // Calculate new position based on tracking rate and errors
        let newRA = initialMount.RA + initialMount.TrackingRate * timestep / 3600.0 + raError / 3600.0
        let newDec = initialMount.Dec + decError / 3600.0
        
        // Create updated mount state
        { initialMount with 
            RA = newRA
            Dec = newDec
        }
        
    /// Create evolved atmospheric state for a specific subframe
    let createEvolvedAtmosphericState 
            (initialAtmosphere: AtmosphericState) 
            (timestep: float) 
            (elapsedTime: float) 
            (totalDuration: float) : AtmosphericState =
        
        // Evolve seeing conditions
        let newSeeing = 
            evolveSeeingCondition 
                initialAtmosphere.SeeingCondition 
                timestep 
                elapsedTime 
                totalDuration
                
        // Evolve cloud coverage
        let newCloudCoverage = 
            evolveCloudCoverage 
                initialAtmosphere.CloudCoverage 
                timestep 
                elapsedTime 
                totalDuration
                
        // Create updated atmospheric state
        { initialAtmosphere with 
            SeeingCondition = newSeeing
            CloudCoverage = newCloudCoverage
        }