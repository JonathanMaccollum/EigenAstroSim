namespace EigenAstroSim.Domain.VirtualSensorSimulation

open System
open EigenAstroSim.Domain
open EigenAstroSim.Domain.Types

/// Models tracking errors and mount behavior with high fidelity
module EnhancedTracking =
    /// Random number generator
    let private random = Random()
    
    /// Properties related to periodic error
    type PeriodicErrorProperties = {
        /// Amplitude of the primary periodic error (arcseconds)
        PrimaryAmplitude: float
        
        /// Period of the primary error (seconds)
        PrimaryPeriod: float
        
        /// Amplitude of secondary harmonics (as fraction of primary)
        HarmonicAmplitudes: float[]
        
        /// Phase shifts of the harmonics (radians)
        HarmonicPhases: float[]
    }
    
    /// Properties related to polar alignment errors
    type PolarAlignmentProperties = {
        /// Azimuth error in degrees
        AzimuthError: float
        
        /// Altitude error in degrees
        AltitudeError: float
        
        /// Site latitude in degrees
        SiteLatitude: float
    }
    
    /// Properties related to mechanical tracking irregularities
    type MechanicalProperties = {
        /// Mean time between binding events (seconds)
        MeanBindingInterval: float
        
        /// Magnitude of binding jumps (arcseconds)
        BindingMagnitude: float
        
        /// Backlash in RA axis (arcseconds)
        RABacklash: float
        
        /// Backlash in Dec axis (arcseconds)
        DecBacklash: float
        
        /// Mount response time to direction changes (seconds)
        ResponseDelay: float
    }
    
    /// Enum for mount types
    type MountType =
        | GermanEquatorial
        | ForkEquatorial
        | AltAzimuth
    
    /// Complete mount model with tracking characteristics
    type EnhancedMountModel = {
        /// Base mount state
        BaseState: MountState
        
        /// Periodic error properties
        PeriodicError: PeriodicErrorProperties
        
        /// Polar alignment properties
        PolarAlignment: PolarAlignmentProperties
        
        /// Mechanical properties
        Mechanics: MechanicalProperties
        
        /// Mount type
        MountType: MountType
        
        /// Previous direction of Dec movement (for backlash)
        mutable PreviousDecDirection: float
        
        /// Previous direction of RA movement (for backlash)
        mutable PreviousRADirection: float
        
        /// Time of last binding event
        mutable LastBindingTime: float
        
        /// Current session time
        mutable CurrentTime: float
    }
    
    /// Convert degrees to radians
    let private toRadians degrees = degrees * Math.PI / 180.0
    
    /// Convert radians to degrees
    let private toDegrees radians = radians * 180.0 / Math.PI
    
    /// Create default mount model based on basic mount state
    let createDefaultMountModel (mountState: MountState) =
        {
            BaseState = mountState
            
            PeriodicError = {
                PrimaryAmplitude = mountState.PeriodicErrorAmplitude
                PrimaryPeriod = mountState.PeriodicErrorPeriod
                HarmonicAmplitudes = [| 0.3; 0.15; 0.05 |] // 2nd, 3rd, 4th harmonics
                HarmonicPhases = [| Math.PI / 4.0; Math.PI / 3.0; Math.PI / 2.0 |]
            }
            
            PolarAlignment = {
                AzimuthError = mountState.PolarAlignmentError * 0.707 // Randomly split error
                AltitudeError = mountState.PolarAlignmentError * 0.707
                SiteLatitude = 40.0 // Default to mid-latitude
            }
            
            Mechanics = {
                MeanBindingInterval = 300.0 // Average 5 minutes between binding events
                BindingMagnitude = 1.5 // 1.5 arcseconds
                RABacklash = 5.0 // 5 arcseconds
                DecBacklash = 7.0 // 7 arcseconds
                ResponseDelay = 0.5 // Half second response delay
            }
            
            MountType = GermanEquatorial
            PreviousDecDirection = 0.0
            PreviousRADirection = 1.0 // Tracking in positive RA initially
            LastBindingTime = 0.0
            CurrentTime = 0.0
        }
        
    /// Calculate periodic error at a specific time
    let calculatePeriodicError (model: EnhancedMountModel) (time: float) =
        // Base periodic error (from primary worm cycle)
        let primaryPhase = 2.0 * Math.PI * time / model.PeriodicError.PrimaryPeriod
        let primaryError = model.PeriodicError.PrimaryAmplitude * Math.Sin(primaryPhase)
        
        // Add harmonics
        let mutable totalError = primaryError
        for i = 0 to model.PeriodicError.HarmonicAmplitudes.Length - 1 do
            let harmonicNumber = i + 2 // 2nd, 3rd, 4th harmonics
            let harmonicPhase = primaryPhase * float harmonicNumber + model.PeriodicError.HarmonicPhases.[i]
            let harmonicError = 
                model.PeriodicError.PrimaryAmplitude * 
                model.PeriodicError.HarmonicAmplitudes.[i] * 
                Math.Sin(harmonicPhase)
            totalError <- totalError + harmonicError
        
        totalError
        
    /// Calculate field rotation rate due to polar misalignment
    let calculateFieldRotation (model: EnhancedMountModel) (time: float) =
        let azError = toRadians model.PolarAlignment.AzimuthError
        let altError = toRadians model.PolarAlignment.AltitudeError
        let latitude = toRadians model.PolarAlignment.SiteLatitude
        
        // Hour angle in radians (0 at meridian)
        let rightAscension = model.BaseState.RA
        let localSiderealTime = rightAscension + 6.0 // Assume 6 hours from meridian by default
        let hourAngle = toRadians((localSiderealTime - rightAscension) * 15.0)
        
        // Declination in radians
        let declination = toRadians model.BaseState.Dec
        
        // Field rotation rate formula (radians per hour)
        let rotationRate = 
            15.0 * (azError * Math.Cos(latitude) * Math.Cos(hourAngle) / Math.Cos(declination) - 
                    altError * Math.Sin(hourAngle))
                    
        // Convert to degrees per second
        toDegrees(rotationRate) / 3600.0
        
    /// Calculate declination drift due to polar misalignment
    let calculateDeclinationDrift (model: EnhancedMountModel) (time: float) =
        let azError = toRadians model.PolarAlignment.AzimuthError
        let altError = toRadians model.PolarAlignment.AltitudeError
        let latitude = toRadians model.PolarAlignment.SiteLatitude
        
        // Hour angle in radians (0 at meridian)
        let rightAscension = model.BaseState.RA
        let localSiderealTime = rightAscension + 6.0 // Assume 6 hours from meridian by default
        let hourAngle = toRadians((localSiderealTime - rightAscension) * 15.0)
        
        // Declination in radians
        let declination = toRadians model.BaseState.Dec
        
        // Declination drift rate formula (radians per hour)
        let driftRate = 
            15.0 * (azError * Math.Sin(hourAngle) + 
                    altError * Math.Cos(hourAngle) * Math.Sin(declination))
                    
        // Convert to arcseconds per second
        toDegrees(driftRate) * 3600.0 / 3600.0
        
    /// Check for and apply mechanical binding events
    let checkForBindingEvents (model: EnhancedMountModel) (time: float) =
        // Update time
        model.CurrentTime <- time
        
        // Time since last binding event
        let timeSinceLastBinding = time - model.LastBindingTime
        
        // Probability of binding increases with time since last event
        // Using an exponential distribution model
        let bindingProbability = 
            1.0 - Math.Exp(-timeSinceLastBinding / model.Mechanics.MeanBindingInterval)
            
        // Random check for binding event
        if random.NextDouble() < bindingProbability * 0.1 then // Scale down for reasonable frequency
            // Binding event occurred!
            model.LastBindingTime <- time
            
            // Random direction of binding jump
            let angle = random.NextDouble() * 2.0 * Math.PI
            let raJump = model.Mechanics.BindingMagnitude * Math.Cos(angle)
            let decJump = model.Mechanics.BindingMagnitude * Math.Sin(angle)
            
            (raJump, decJump)
        else
            (0.0, 0.0)
            
    /// Calculate backlash effects during direction changes
    let calculateBacklash (model: EnhancedMountModel) (raDirection: float) (decDirection: float) =
        // RA backlash
        let raBacklash = 
            if Math.Sign(raDirection) <> Math.Sign(model.PreviousRADirection) && 
               Math.Abs(raDirection) > 0.1 && Math.Abs(model.PreviousRADirection) > 0.1 then
                // Direction change - apply backlash
                model.Mechanics.RABacklash * float(Math.Sign(raDirection))
            else
                0.0
                
        // Dec backlash
        let decBacklash = 
            if Math.Sign(decDirection) <> Math.Sign(model.PreviousDecDirection) && 
               Math.Abs(decDirection) > 0.1 && Math.Abs(model.PreviousDecDirection) > 0.1 then
                // Direction change - apply backlash
                model.Mechanics.DecBacklash * float(Math.Sign(decDirection))
            else
                0.0
                
        // Update previous directions
        if Math.Abs(raDirection) > 0.1 then
            model.PreviousRADirection <- raDirection
        if Math.Abs(decDirection) > 0.1 then
            model.PreviousDecDirection <- decDirection
            
        (raBacklash, decBacklash)
        
    /// Calculate random tracking errors (wind, vibration, etc.)
    let calculateRandomErrors (model: EnhancedMountModel) (timestep: float) =
        // Random walk with inertia
        // Use smaller error for better mounts
        let errorScale = 0.2 // arcseconds per second
        
        let raError = errorScale * Math.Sqrt(timestep) * (random.NextDouble() * 2.0 - 1.0)
        let decError = errorScale * Math.Sqrt(timestep) * (random.NextDouble() * 2.0 - 1.0)
        
        (raError, decError)
        
    /// Generate mount state for a specific point in time
    let generateMountState 
            (model: EnhancedMountModel) 
            (elapsedTime: float) 
            (timestep: float) 
            (isTracking: bool) =
        
        // Start with base position
        let baseRA = model.BaseState.RA
        let baseDec = model.BaseState.Dec
        
        // Calculate tracking movement (if tracking)
        let trackingRA = 
            if isTracking then
                // Sidereal rate in RA (degrees per second)
                model.BaseState.TrackingRate * timestep / 3600.0
            else
                0.0
                
        // Apply periodic error (PE only affects RA during tracking)
        let periodicErrorRA = 
            if isTracking then
                calculatePeriodicError model elapsedTime / 3600.0
            else
                0.0
                
        // Calculate polar alignment effects
        let fieldRotation = calculateFieldRotation model elapsedTime
        let decDrift = calculateDeclinationDrift model elapsedTime
        
        // Check for binding events
        let (bindingRA, bindingDec) = checkForBindingEvents model elapsedTime
        
        // Calculate backlash
        let (backlashRA, backlashDec) = calculateBacklash model 1.0 0.0 // Assuming tracking in +RA
        
        // Calculate random errors
        let (randomRA, randomDec) = calculateRandomErrors model timestep
        
        // Combine all effects
        let newRA = baseRA + trackingRA + periodicErrorRA/3600.0 + bindingRA/3600.0 + backlashRA/3600.0 + randomRA/3600.0
        let newDec = baseDec + decDrift*timestep/3600.0 + bindingDec/3600.0 + backlashDec/3600.0 + randomDec/3600.0
        
        // Create updated mount state
        { model.BaseState with
            RA = newRA
            Dec = newDec
        }