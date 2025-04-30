namespace EigenAstroSim.Tests

module MountSimulationTests =
    open System
    open Xunit
    open FsUnit.Xunit
    open EigenAstroSim.Domain
    open EigenAstroSim.Domain.Types
    open EigenAstroSim.Domain.MountSimulation
    
    // Helper function to create a standard test mount state
    let createTestMountState() =
        let baseState = {
            RA = 0.0
            Dec = 0.0
            TrackingRate = 1.0
            PolarAlignmentError = 0.0
            PeriodicErrorAmplitude = 0.0
            PeriodicErrorPeriod = 0.0
            IsSlewing = false
            SlewRate = 3.0 // 3 degrees per second
            FocalLength = 1000.0
        }
        
        createDefaultDetailedMountState baseState
    
    [<Fact>]
    let ``Mount initialization should have correct default values`` () =
        // Arrange
        let baseState = {
            RA = 10.0
            Dec = 20.0
            TrackingRate = 1.0
            PolarAlignmentError = 0.1
            PeriodicErrorAmplitude = 5.0
            PeriodicErrorPeriod = 300.0
            IsSlewing = false
            SlewRate = 2.0
            FocalLength = 1000.0
        }
        
        // Act
        let state = createDefaultDetailedMountState baseState
        
        // Assert
        state.BaseState |> should equal baseState
        state.SlewStatus |> should equal Idle
        state.PierSide |> should equal West
        state.TrackingMode |> should equal Sidereal
        state.PeriodicErrorPhase |> should equal 0.0
        state.RABacklash.Amount |> should equal 5.0
        state.DecBacklash.Amount |> should equal 8.0
        state.LastRADirection |> should equal 0
        state.LastDecDirection |> should equal 0
        state.HasMeridianFlipped |> should equal false
        state.TimeOfLastPulseGuide |> should equal None
    
    [<Fact>]
    let ``Slew to coordinates should update mount position`` () =
        // Arrange
        let state = createTestMountState()
        let targetRA = 15.0
        let targetDec = 30.0
        
        // Act
        let slewingState = beginSlew state targetRA targetDec
        // Simulate entire slew
        let elapsedTimeSec = 15.0 // More than enough time to complete slew
        let finalState = updateSlew slewingState elapsedTimeSec
        
        // Assert
        finalState.BaseState.RA |> should equal targetRA
        finalState.BaseState.Dec |> should equal targetDec
        finalState.BaseState.IsSlewing |> should equal false
        finalState.SlewStatus |> should equal Idle
    
    [<Fact>]
    let ``Slew rate should affect time to reach target`` () =
        // Arrange
        let fastState = 
            let baseState = {
                RA = 0.0
                Dec = 0.0
                TrackingRate = 1.0
                PolarAlignmentError = 0.0
                PeriodicErrorAmplitude = 0.0
                PeriodicErrorPeriod = 0.0
                IsSlewing = false
                SlewRate = 10.0 // Fast - 10 degrees per second
                FocalLength = 1000.0
            }
            createDefaultDetailedMountState baseState
            
        let slowState = 
            let baseState = {
                RA = 0.0
                Dec = 0.0
                TrackingRate = 1.0
                PolarAlignmentError = 0.0
                PeriodicErrorAmplitude = 0.0
                PeriodicErrorPeriod = 0.0
                IsSlewing = false
                SlewRate = 2.0 // Slow - 2 degrees per second
                FocalLength = 1000.0
            }
            createDefaultDetailedMountState baseState
            
        let targetRA = 50.0
        let targetDec = 50.0
        
        // Act
        let fastSlewing = beginSlew fastState targetRA targetDec
        let slowSlewing = beginSlew slowState targetRA targetDec
        
        let partialTimeElapsed = 1.0 // 1 second
        
        let fastStatePartial = updateSlew fastSlewing partialTimeElapsed
        let slowStatePartial = updateSlew slowSlewing partialTimeElapsed
        
        // Extract progress from slew status
        let fastProgress = 
            match fastStatePartial.SlewStatus with
            | Slewing(_, _, progress) -> progress
            | _ -> 0.0
            
        let slowProgress = 
            match slowStatePartial.SlewStatus with
            | Slewing(_, _, progress) -> progress
            | _ -> 0.0
        
        // Assert
        fastProgress |> should be (greaterThan slowProgress)
    
    [<Fact>]
    let ``Tracking should maintain position relative to sky`` () =
        // Arrange
        let state = createTestMountState()
        let initialRA = state.BaseState.RA
        
        // Act - simulate tracking for 1 hour
        let oneHourLater = state.LastTrackingUpdate.AddHours(1.0)
        let updatedState = updateMountForTime state oneHourLater
        
        // Assert - RA should have increased by ~15 degrees (sidereal rate for 1 hour)
        let expectedRAChange = siderealRate * 3600.0 // Approximately 15 degrees
        updatedState.BaseState.RA - initialRA |> should (equalWithin 0.1) expectedRAChange
    
    [<Fact>]
    let ``Stopping tracking should cause stars to drift`` () =
        // Arrange
        let state = createTestMountState()
        // Turn off tracking
        let stateWithTrackingOff = 
            let newBaseState = { state.BaseState with TrackingRate = 0.0 }
            { state with 
                BaseState = newBaseState 
                TrackingMode = Off
            }
        
        // Act - simulate passage of time
        let oneHourLater = state.LastTrackingUpdate.AddHours(1.0)
        let updatedState = updateMountForTime stateWithTrackingOff oneHourLater
        
        // Assert - RA should not change with tracking off
        updatedState.BaseState.RA |> should equal stateWithTrackingOff.BaseState.RA
    
    [<Fact>]
    let ``Mount reports correct status during operations`` () =
        // Arrange
        let state = createTestMountState()
        let targetRA = 15.0
        let targetDec = 30.0
        
        // Act
        let slewingState = beginSlew state targetRA targetDec
        // Partial slew
        let partialSlew = updateSlew slewingState 0.5
        
        // Assert
        partialSlew.BaseState.IsSlewing |> should equal true
        match partialSlew.SlewStatus with
        | Slewing _ -> true
        | _ -> false
        |> should equal true
    
    [<Fact>]
    let ``Periodic error should oscillate around true position`` () =
        // Arrange
        let baseState = {
            RA = 0.0
            Dec = 0.0
            TrackingRate = 1.0
            PolarAlignmentError = 0.0
            PeriodicErrorAmplitude = 10.0 // 10 arcseconds
            PeriodicErrorPeriod = 600.0   // 10 minutes
            IsSlewing = false
            SlewRate = 3.0
            FocalLength = 1000.0
        }
        let state = createDefaultDetailedMountState baseState
        
        // Act - capture positions at different phases
        let startTime = state.LastTrackingUpdate
        let quarterPeriod = state.BaseState.PeriodicErrorPeriod / 4.0
        
        let state1 = updateMountForTime state (startTime.AddSeconds(0.0))
        let state2 = updateMountForTime state (startTime.AddSeconds(quarterPeriod))
        let state3 = updateMountForTime state (startTime.AddSeconds(2.0 * quarterPeriod))
        let state4 = updateMountForTime state (startTime.AddSeconds(3.0 * quarterPeriod))
        let state5 = updateMountForTime state (startTime.AddSeconds(4.0 * quarterPeriod))
        
        // Assert - check that we see a full oscillation
        let phase1 = state1.PeriodicErrorPhase
        let phase2 = state2.PeriodicErrorPhase
        let phase3 = state3.PeriodicErrorPhase
        let phase4 = state4.PeriodicErrorPhase
        let phase5 = state5.PeriodicErrorPhase
        
        // Phases should advance by approximately 90 degrees each time
        phase2 - phase1 |> should (equalWithin 1.0) 90.0
        phase3 - phase2 |> should (equalWithin 1.0) 90.0
        phase4 - phase3 |> should (equalWithin 1.0) 90.0
        
        // After a full period, we should be back to the starting phase (give or take a small error)
        phase5 |> should (equalWithin 1.0) phase1
    
    [<Fact>]
    let ``Polar alignment error should cause declination drift`` () =
        // Arrange
        let baseState = {
            RA = 0.0
            Dec = 0.0
            TrackingRate = 1.0
            PolarAlignmentError = 1.0 // 1 degree error
            PeriodicErrorAmplitude = 0.0
            PeriodicErrorPeriod = 0.0
            IsSlewing = false
            SlewRate = 3.0
            FocalLength = 1000.0
        }
        let state = createDefaultDetailedMountState baseState
        let initialDec = state.BaseState.Dec
        
        // Act - simulate tracking for 1 hour with polar error
        let oneHourLater = state.LastTrackingUpdate.AddHours(1.0)
        let stateWithTracking = updateMountForTime state oneHourLater
        let stateWithPolarError = applyPolarAlignmentError stateWithTracking 3600.0 // 1 hour in seconds
        
        // Assert - Dec should drift due to polar alignment error
        stateWithPolarError.BaseState.Dec |> should not' (equal initialDec)
    
    [<Fact>]
    let ``Cable snag should cause sudden position change`` () =
        // Arrange
        let state = createTestMountState()
        let initialRA = state.BaseState.RA
        let initialDec = state.BaseState.Dec
        
        // Act - simulate a cable snag
        let snagAmountRA = 0.01 // 0.01 degrees
        let snagAmountDec = -0.02 // -0.02 degrees
        let stateAfterSnag = simulateCableSnag state snagAmountRA snagAmountDec
        
        // Assert
        stateAfterSnag.BaseState.RA |> should equal (initialRA + snagAmountRA)
        stateAfterSnag.BaseState.Dec |> should equal (initialDec + snagAmountDec)
    
    [<Fact>]
    let ``Guide commands should correctly adjust position`` () =
        // Arrange
        let state = createTestMountState()
        let initialRA = state.BaseState.RA
        let initialDec = state.BaseState.Dec
        
        // Act - apply a guide pulse
        let raRate = 1.0 // West
        let decRate = 1.0 // North
        let duration = 1.0 // 1 second
        let stateAfterGuide = processPulseGuide state raRate decRate duration
        
        // Assert
        stateAfterGuide.BaseState.RA |> should not' (equal initialRA)
        stateAfterGuide.BaseState.Dec |> should not' (equal initialDec)
        stateAfterGuide.TimeOfLastPulseGuide |> should not' (equal None)
    
    [<Fact>]
    let ``Dec backlash should be properly modeled`` () =
        // Arrange
        let state = createTestMountState()
        
        // Act - apply a north pulse
        let northState = processPulseGuide state 0.0 1.0 1.0
        // Then apply a south pulse of the same magnitude
        let reversedState = processPulseGuide northState 0.0 -1.0 1.0
        
        // Assert
        // Due to backlash, the south movement should be less than the north movement
        let northMovement = northState.BaseState.Dec - state.BaseState.Dec
        let southMovement = reversedState.BaseState.Dec - northState.BaseState.Dec
        
        Math.Abs(southMovement) |> should be (lessThan (Math.Abs(northMovement)))
    
    [<Fact>]
    let ``Meridian flip should correctly reorient the mount`` () =
        // Arrange
        let state = createTestMountState()
        let initialPierSide = state.PierSide
        
        // Act
        let stateAfterFlip = performMeridianFlip state
        
        // Assert
        stateAfterFlip.PierSide |> should not' (equal initialPierSide)
        stateAfterFlip.HasMeridianFlipped |> should equal true
        
        // Backlash should be reset after flip
        stateAfterFlip.RABacklash.LastDirection |> should equal 0
        stateAfterFlip.RABacklash.RemainingBacklash |> should equal 0.0
        stateAfterFlip.DecBacklash.LastDirection |> should equal 0
        stateAfterFlip.DecBacklash.RemainingBacklash |> should equal 0.0