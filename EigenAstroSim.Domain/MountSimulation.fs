namespace EigenAstroSim.Domain

module MountSimulation =
    open System
    open Types
    
    /// Mount slew status
    type SlewStatus =
        | Idle
        | Slewing of targetRA: float * targetDec: float * progress: float
    
    /// Side of pier
    type PierSide =
        | East
        | West
    
    /// Mount tracking mode
    type TrackingMode =
        | Sidereal
        | Lunar
        | Solar
        | Custom of rate: float
        | Off
    
    /// Backlash configuration for an axis
    type AxisBacklash = {
        Amount: float           // Amount of backlash in arcseconds
        Compensation: float     // Percentage of compensation (0-100%)
        LastDirection: int      // Last direction (-1, 0, or 1)
        RemainingBacklash: float // Remaining backlash to be taken up
    }
    
    /// Detailed mount state including tracking and error characteristics
    type DetailedMountState = {
        BaseState: MountState
        SlewStatus: SlewStatus
        PierSide: PierSide
        TrackingMode: TrackingMode
        LastTrackingUpdate: DateTime
        PeriodicErrorPhase: float    // Current phase of periodic error (0-360 degrees)
        RABacklash: AxisBacklash
        DecBacklash: AxisBacklash
        LastRADirection: int         // -1 = East, 0 = None, 1 = West
        LastDecDirection: int        // -1 = South, 0 = None, 1 = North
        HasMeridianFlipped: bool     // Whether the mount has performed a meridian flip
        TimeOfLastPulseGuide: DateTime option // Time of the last pulse guide command
    }
    
    /// Sidereal rate in degrees per second
    let siderealRate = 360.0 / (23.0 * 3600.0 + 56.0 * 60.0 + 4.09) // ~0.004178 degrees/sec
    
    /// Creates a new default backlash configuration
    let createDefaultBacklash amount compensation =
        {
            Amount = amount
            Compensation = compensation
            LastDirection = 0
            RemainingBacklash = 0.0
        }
    
    /// Creates a new detailed mount state with default values
    let createDefaultDetailedMountState baseState =
        {
            BaseState = baseState
            SlewStatus = Idle
            PierSide = West
            TrackingMode = Sidereal
            LastTrackingUpdate = DateTime.UtcNow
            PeriodicErrorPhase = 0.0
            RABacklash = createDefaultBacklash 5.0 50.0 // 5 arcsec backlash, 50% compensation
            DecBacklash = createDefaultBacklash 8.0 80.0 // 8 arcsec backlash, 80% compensation
            LastRADirection = 0
            LastDecDirection = 0
            HasMeridianFlipped = false
            TimeOfLastPulseGuide = None
        }
    
    /// Determines the pier side based on hour angle
    let calculatePierSide ra localSiderealTime =
        let hourAngle = localSiderealTime - ra
        // Normalize hour angle to 0-24 range
        let normalizedHA = 
            (hourAngle + 24.0) % 24.0
        
        // If hour angle is between 0 and 12 hours, we're on the west side
        // Otherwise, we're on the east side
        if normalizedHA < 12.0 then West else East
    
    /// Calculates the Local Sidereal Time given the date/time and longitude
    let calculateLocalSiderealTime (dateTime: DateTime) longitude =
        // This is a simplified calculation that is not perfectly accurate
        // For precise calculations, more detailed astronomical formulas should be used
        
        // Convert the date to Julian Date
        let d = dateTime.ToOADate() + 2415018.5
        let jd = d - 2451545.0
        
        // Calculate Greenwich Mean Sidereal Time (GMST) in hours
        let t = jd / 36525.0
        let gmst = 
            6.697374558 + 
            0.06570982441908 * jd + 
            1.00273781191135448 * ((float dateTime.Hour) + (float dateTime.Minute) / 60.0 + (float dateTime.Second) / 3600.0) + 
            0.000026 * t * t
        
        // Convert longitude to hours and add to GMST to get Local Sidereal Time
        let lst = (gmst + longitude / 15.0) % 24.0
        
        // Ensure LST is positive
        if lst < 0.0 then lst + 24.0 else lst
    
    /// Updates the mount state for the passage of time
    let updateMountForTime (state: DetailedMountState) (currentTime: DateTime) =
        if state.BaseState.TrackingRate <= 0.0 then
            // Not tracking, no update needed
            state
        else
            let timeSpan = currentTime - state.LastTrackingUpdate
            let elapsedSeconds = timeSpan.TotalSeconds
            
            // Calculate tracking movement
            let trackingRate = 
                match state.TrackingMode with
                | Sidereal -> siderealRate
                | Lunar -> siderealRate * 0.966
                | Solar -> siderealRate * 0.9863
                | Custom rate -> rate
                | Off -> 0.0
            
            let trackingMovement = trackingRate * elapsedSeconds
            
            // Update RA position (tracking is in RA only)
            let newRA = state.BaseState.RA + trackingMovement
            
            // Calculate periodic error
            // Convert elapsed time to phase angle based on the period
            let periodSeconds = state.BaseState.PeriodicErrorPeriod
            if periodSeconds > 0.0 then
                let phaseChange = (elapsedSeconds / periodSeconds) * 360.0
                let newPhase = (state.PeriodicErrorPhase + phaseChange) % 360.0
                
                // Calculate periodic error contribution (sinusoidal)
                let periodicErrorContribution = 
                    state.BaseState.PeriodicErrorAmplitude * 
                    Math.Sin(newPhase * Math.PI / 180.0)
                
                // Apply periodic error to RA
                let raWithError = newRA + periodicErrorContribution / 3600.0 // Convert arcseconds to degrees
                
                // Update the mount state
                let newBaseState = { state.BaseState with RA = raWithError }
                { state with 
                    BaseState = newBaseState
                    LastTrackingUpdate = currentTime
                    PeriodicErrorPhase = newPhase 
                }
            else
                // No periodic error
                let newBaseState = { state.BaseState with RA = newRA }
                { state with 
                    BaseState = newBaseState
                    LastTrackingUpdate = currentTime 
                }
    
    /// Applies backlash to a movement based on backlash configuration and direction
    let applyBacklash (backlash: AxisBacklash) direction (movementAmount:float) =
        if direction = backlash.LastDirection || direction = 0 || backlash.LastDirection = 0 then
            // No direction change or no movement, no backlash to apply
            let newBacklash = { backlash with LastDirection = if direction <> 0 then direction else backlash.LastDirection }
            newBacklash, movementAmount
        else
            let absMovementAmount = Math.Abs(movementAmount)
            // Direction change, apply backlash
            let backlashAmount = backlash.Amount * (1.0 - backlash.Compensation / 100.0)
            let newRemainingBacklash = backlashAmount
            
            // All movement goes toward taking up backlash first
            let actualMovement = 
                if absMovementAmount <= newRemainingBacklash then
                    // All movement consumed by backlash
                    0.0
                else
                    // Partial movement after backlash
                    let signOfMovementAmount = Math.Sign(movementAmount)
                    (float signOfMovementAmount) * (absMovementAmount - newRemainingBacklash)
            
            let newBacklash = { 
                backlash with 
                    LastDirection = direction
                    RemainingBacklash = 
                        if absMovementAmount <= newRemainingBacklash then
                            newRemainingBacklash - absMovementAmount
                        else
                            0.0 
            }
            
            newBacklash, actualMovement

    /// Minimum threshold for guide pulses in degrees
    let minGuideThreshold = 1.0e-6

    let processPulseGuide (state: DetailedMountState) raRate decRate durationSec =
        // Calculate the movement amounts
        let rawRAMovement = raRate * durationSec * state.BaseState.SlewRate / 3600.0 // Convert to degrees
        let rawDecMovement = decRate * durationSec * state.BaseState.SlewRate / 3600.0 // Convert to degrees
        
        // Check if movements are below threshold
        let effectiveRAMovement = 
            if Math.Abs(rawRAMovement) < minGuideThreshold then 0.0
            else rawRAMovement
            
        let effectiveDecMovement = 
            if Math.Abs(rawDecMovement) < minGuideThreshold then 0.0
            else rawDecMovement
        
        // If both movements are zero, return original state UNCHANGED
        if effectiveRAMovement = 0.0 && effectiveDecMovement = 0.0 then
            state
        else
            // Determine direction based on EFFECTIVE movements, not raw movements
            let raDirection = Math.Sign(effectiveRAMovement)
            let decDirection = Math.Sign(effectiveDecMovement)
            
            // Apply backlash using EFFECTIVE movements, not raw movements
            let newRABacklash, actualRAMovement = applyBacklash state.RABacklash raDirection effectiveRAMovement
            let newDecBacklash, actualDecMovement = applyBacklash state.DecBacklash decDirection effectiveDecMovement
            
            // Rest of the function continues as before...
            // Adjust Dec movement based on pier side
            let adjustedDecMovement = 
                match state.PierSide with
                | East -> -actualDecMovement  // Invert direction on East pier
                | West -> actualDecMovement   // Standard direction on West pier
            
            // Apply the movements to the mount position
            let newRA = state.BaseState.RA + actualRAMovement
            let newDec = state.BaseState.Dec + adjustedDecMovement
            
            // Create new base state
            let newBaseState = { 
                state.BaseState with 
                    RA = newRA
                    Dec = newDec
            }
            
            // Return updated state
            { state with 
                BaseState = newBaseState
                RABacklash = newRABacklash
                DecBacklash = newDecBacklash
                LastRADirection = if raDirection <> 0 then raDirection else state.LastRADirection
                LastDecDirection = if decDirection <> 0 then decDirection else state.LastDecDirection
                TimeOfLastPulseGuide = Some DateTime.UtcNow
            }        
    /// Begins a slew to the specified coordinates
    let beginSlew (state: DetailedMountState) targetRA targetDec =
        // Set mount to slewing state
        let newBaseState = { state.BaseState with IsSlewing = true }
        { state with 
            BaseState = newBaseState
            SlewStatus = Slewing(targetRA, targetDec, 0.0) 
        }
    
    /// Updates the progress of an ongoing slew
    let updateSlew (state: DetailedMountState) elapsedTimeSec =
        match state.SlewStatus with
        | Idle -> state
        | Slewing(targetRA, targetDec, progress) ->
            // Calculate how long the slew should take
            let raDistance = Math.Abs(targetRA - state.BaseState.RA)
            let decDistance = Math.Abs(targetDec - state.BaseState.Dec)
            let maxDistance = Math.Max(raDistance, decDistance)
            
            // Estimate time to complete (simplistic model)
            let totalTimeNeeded = 
                if state.BaseState.SlewRate > 0.0 then 
                    maxDistance / state.BaseState.SlewRate
                else
                    100.0 // Default if slew rate is zero
            // For very small distances or extremely fast slew rates, ensure a minimum slew time
            // This prevents immediate completion in test scenarios
            let adjustedTimeNeeded = Math.Max(totalTimeNeeded, 0.1)
            // Calculate new progress
            let newProgress = progress + (elapsedTimeSec / adjustedTimeNeeded)
            
            if newProgress >= 1.0 then
                // Slew complete
                let newBaseState = { 
                    state.BaseState with 
                        RA = targetRA
                        Dec = targetDec
                        IsSlewing = false
                }
                
                { state with 
                    BaseState = newBaseState
                    SlewStatus = Idle 
                }
            else
                // Calculate intermediate position
                let newRA = state.BaseState.RA + (targetRA - state.BaseState.RA) * newProgress
                let newDec = state.BaseState.Dec + (targetDec - state.BaseState.Dec) * newProgress
                
                let newBaseState = { 
                    state.BaseState with 
                        RA = newRA
                        Dec = newDec
                        IsSlewing = true
                }
                
                { state with 
                    BaseState = newBaseState
                    SlewStatus = Slewing(targetRA, targetDec, newProgress) 
                }
    
    /// Checks if a meridian flip is needed for the given position
    let isMeridianFlipNeeded (state: DetailedMountState) (currentTime: DateTime) longitude =
        // Calculate Local Sidereal Time
        let lst = calculateLocalSiderealTime currentTime longitude
        
        // Determine current pier side based on hour angle
        let currentPierSide = calculatePierSide state.BaseState.RA lst
        
        // If current side is different from state's side, a flip is needed
        currentPierSide <> state.PierSide
    
    /// Performs a meridian flip
    let performMeridianFlip (state: DetailedMountState) =
        // Flip the pier side
        let newPierSide = match state.PierSide with
                          | East -> West
                          | West -> East
        
        // Reset backlash values as the direction mechanics change
        let resetRABacklash = { 
            state.RABacklash with 
                LastDirection = 0
                RemainingBacklash = 0.0 
        }
        
        let resetDecBacklash = { 
            state.DecBacklash with 
                LastDirection = 0
                RemainingBacklash = 0.0 
        }
        
        // Return updated state
        { state with 
            PierSide = newPierSide
            RABacklash = resetRABacklash
            DecBacklash = resetDecBacklash
            HasMeridianFlipped = true
        }
    
    /// Apply polar alignment error to the mount state
    let applyPolarAlignmentError (state: DetailedMountState) (elapsedSeconds: float) =
        if state.BaseState.PolarAlignmentError <= 0.0 then
            // No polar alignment error
            state
        else
            // Calculate the amount of field rotation due to polar misalignment
            // This is a simplified model; real-world behavior is more complex
            
            // Field rotation rate depends on declination and polar alignment error
            let decRadians = state.BaseState.Dec * Math.PI / 180.0
            let polarErrorRadians = state.BaseState.PolarAlignmentError * Math.PI / 180.0
            
            // Calculate field rotation rate (simplified formula)
            let fieldRotationRate = 
                state.BaseState.TrackingRate * 
                Math.Sin(polarErrorRadians) * 
                Math.Cos(decRadians) / 3600.0 // Convert to degrees/sec
            
            // Apply field rotation effect to Dec (simplified)
            let decDrift = fieldRotationRate * elapsedSeconds
            let newDec = state.BaseState.Dec + decDrift
            
            // Update the mount state
            let newBaseState = { state.BaseState with Dec = newDec }
            { state with BaseState = newBaseState }
    
    /// Simulate a cable snag
    let simulateCableSnag (state: DetailedMountState) snagAmountRA snagAmountDec =
        // Apply sudden movement to simulate a cable snag
        let newRA = state.BaseState.RA + snagAmountRA
        let newDec = state.BaseState.Dec + snagAmountDec
        
        // Update the mount state
        let newBaseState = { 
            state.BaseState with 
                RA = newRA
                Dec = newDec
        }
        
        { state with BaseState = newBaseState }