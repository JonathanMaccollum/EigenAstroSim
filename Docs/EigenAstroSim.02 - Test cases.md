# EigenAstroSim Test Documentation

## Overview

This document summarizes the test suite for the EigenAstroSim project, including the existing tests for the Star Field and Image Generation components, as well as proposed tests for the upcoming Mount Simulation component.

## 1. Star Field Tests

The star field component has extensive unit and property-based tests to ensure accurate and realistic star field generation.

### 1.1 Unit Tests (StarFieldTests)

| Test | Description | Verification |
|------|-------------|--------------|
| `CreateEmpty should return empty star field with specified reference coordinates` | Tests initialization of empty star field | Verifies emptiness and correct reference coordinates |
| `Star magnitudes should be within valid astronomical range` | Tests magnitude constraints | Ensures all stars have magnitudes between 1.0 and limit magnitude |
| `Star colors should be within valid B-V index range` | Tests color index constraints | Verifies B-V indices between -0.3 and 2.0 |
| `Star coordinates should be within specified field radius` | Tests spatial distribution | Confirms all stars within specified radius |
| `Star density should decrease with galactic latitude` | Tests density variation | Higher galactic latitudes have fewer stars |
| `Star count should increase with limiting magnitude` | Tests magnitude distribution | Higher limiting magnitude results in more stars |
| `Expanding star field should add new stars for uncovered regions` | Tests field expansion logic | New regions add new stars |
| `Stars should persist when revisiting an area` | Tests star persistence | Stars remain in memory when returning to a previously visited area |
| `Star field should generate a reasonable number of stars` | Tests star count reasonability | Ensures star count is reasonable |
| `Calculate covered region should return None for empty star field` | Tests empty field behavior | Covered region is None for empty fields |
| `Calculate covered region should return valid region for non-empty star field` | Tests region calculation | Returns valid region containing all stars |
| `Region contains should correctly identify contained regions` | Tests region containment | Correctly determines if one region is within another |
| `Stars should have unique IDs` | Tests star ID uniqueness | Each star has a unique identifier |
| `Brighter stars should tend to be bluer` | Tests color-magnitude relationship | Brighter stars have lower B-V indices on average |

### 1.2 Property-Based Tests (PropertyBasedTests)

| Test | Description | Verification |
|------|-------------|--------------|
| `Star magnitudes should be within valid astronomical range` | Tests magnitude constraints (multiple parameters) | Stars have magnitudes between 1.0 and limit magnitude |
| `Star colors should be within valid B-V index range` | Tests color index constraints (multiple parameters) | B-V indices between -0.3 and 2.0 |
| `Star coordinates should be within specified field radius` | Tests spatial distribution (multiple parameters) | All stars within specified radius |
| `Stars should have unique IDs` | Tests star ID uniqueness (multiple parameters) | Each star has a unique identifier |
| `Higher limiting magnitude should result in more stars` | Tests magnitude scaling (multiple parameters) | Higher limiting magnitude yields more stars |
| `Stars persist when revisiting an area` | Tests star persistence (multiple parameters) | Stars remain in memory when returning to a region |
| `Star density should decrease with galactic latitude` | Tests density variation (specific galactic coordinates) | Areas near galactic poles have fewer stars |

## 2. Image Generation Tests

The image generation component has comprehensive tests for realistic image synthesis, including camera simulation, atmospheric effects, and astronomical phenomena.

### 2.1 Unit Tests (ImageGenerationTests)

| Test | Description | Verification |
|------|-------------|--------------|
| `Project star should convert star coordinates to pixel coordinates` | Tests star projection | Star at center of field projects to center of image |
| `Project star with rotation should rotate star position` | Tests rotator effects | 90° rotation properly repositions stars |
| `Get visible stars should return stars in field of view` | Tests field of view calculation | Returns only stars visible in current FOV |
| `Apply seeing to star should spread star light based on seeing` | Tests atmospheric seeing | Poorer seeing increases FWHM proportionally |
| `Render star should add light to image matrix` | Tests PSF rendering | Star light spreads correctly to surrounding pixels |
| `Apply sensor noise should add realistic noise` | Tests sensor noise | Noise patterns are realistic |
| `Apply binning should combine adjacent pixels` | Tests binning | Properly combines pixels at specified binning factor |
| `Generate image should create realistic synthetic image` | Tests full image generation | Produces realistic star field image |

### 2.2 Property-Based Tests (ImageGenerationPropertyTests)

| Test | Description | Verification |
|------|-------------|--------------|
| `Image dimensions should match camera dimensions` | Tests image dimensions | Image size matches camera dimensions |
| `Stars in center of field should be in center of image` | Tests star positioning | Stars project to correct positions |
| `Poor seeing should reduce peak brightness of stars` | Tests seeing effects | Worse seeing reduces peak brightness |
| `Image brightness should be proportional to exposure time` | Tests exposure effects | Brightness scales with exposure time |
| `Cloud coverage should reduce image brightness` | Tests cloud effects | Clouds reduce overall brightness |
| `Binning should preserve total light` | Tests binning conservation | Total light is preserved during binning |
| `Satellite trail should create a line of bright pixels` | Tests satellite trail | Trail forms a linear pattern |
| `Adding sensor noise should not change mean pixel value significantly` | Tests noise bias | Noise doesn't significantly shift mean values |
| `Sensor noise should add appropriate variance` | Tests noise variance | Noise variance related to shot noise and read noise |

## 3. Mount Simulation Tests

The following test cases are defined for the Mount Simulation component. Tests that have been implemented are marked with ✅.

### 3.1 Basic Mount Functionality Tests

| Test | Status | Description | Verification |
|------|--------|-------------|--------------|
| `Mount initialization should have correct default values` | ✅ | Test initial state | Verify tracking rate, position, errors are properly initialized |
| `Slew to coordinates should update mount position` | ✅ | Test basic slewing | Mount reaches target coordinates |
| `Slew rate should affect time to reach target` | ✅ | Test slew speed | Higher rates complete slewing faster |
| `Tracking should maintain position relative to sky` | ✅ | Test tracking | Stars remain stationary during tracking |
| `Stopping tracking should cause stars to drift` | ✅ | Test tracking halt | Stars drift at sidereal rate when tracking stops |
| `Mount reports correct status during operations` | ✅ | Test status reporting | Status flags (slewing, tracking) are accurate |
| `Meridian flip should correctly reorient the mount` | ✅ | Test meridian crossing | Mount properly flips when crossing meridian |
| `Pointing model should be maintained after meridian flip` | 🔄 | Test post-flip accuracy | Pointing accuracy preserved after flip |

### 3.2 Mount Error Simulation Tests

| Test | Status | Description | Verification |
|------|--------|-------------|--------------|
| `Periodic error should oscillate around true position` | ✅ | Test periodic error | Position oscillates with specified amplitude and period |
| `Polar alignment error should cause field rotation` | 🔄 | Test alignment error | Field rotates at rate proportional to alignment error |
| `Polar alignment error should cause declination drift` | ✅ | Test alignment drift | Declination drifts over time with alignment error |
| `Cable snag should cause sudden position change` | ✅ | Test cable snags | Position jumps when cable snag occurs |
| `Guide commands should correctly adjust position` | ✅ | Test guiding | Mount responds correctly to pulse-guide commands |
| `Dec backlash should be properly modeled` | ✅ | Test Dec backlash | Direction changes in Dec show appropriate delay |
| `RA backlash should be minimized during normal tracking` | 🔄 | Test RA backlash during tracking | RA backlash effects should be minimal during west tracking |
| `RA backlash should appear when reversing direction` | 🔄 | Test RA directional backlash | Eastward movements show backlash effects |
| `Backlash compensation should operate correctly` | 🔄 | Test backlash compensation | Compensation algorithms reduce effective backlash |

### 3.3 Mount Property-Based Tests

| Test | Status | Description | Verification |
|------|--------|-------------|--------------|
| `Mount position error should be within acceptable limits` | ✅ | Test position accuracy | Position error within typical mount precision |
| `Periodic error should average to zero over complete period` | ✅ | Test error balance | Error sums to zero over full period |
| `Slewing to valid coordinates always succeeds` | ✅ | Test slew robustness | Slews succeed across coordinate range |
| `Multiple rapid slew commands handled appropriately` | 🔄 | Test command queueing | Command sequences execute correctly |
| `Guide pulses below minimum threshold have no effect` | ✅ | Test minimum pulse | Very small guide pulses ignored |
| `Guide pulses scale correctly with duration` | ✅ | Test pulse scaling | Movement proportional to pulse duration |
| `Simultaneous RA and Dec guide pulses produce correct vector movement` | ✅ | Test combined guiding | Diagonal movement correct with dual-axis guiding |
| `Dec backlash should be greater than RA backlash during tracking` | ✅ | Test differential backlash | Dec shows more pronounced backlash effects than RA during tracking |
| `Eastern RA guide pulses should show lag compared to western pulses` | 🔄 | Test directional guide response | Eastern pulses delayed by backlash gap, western pulses immediate |
| `Side of pier should affect Dec guide direction` | ✅ | Test pier side guiding | Dec guide directions flip after meridian flip |
| `Multiple RA direction reversals should consistently show backlash` | 🔄 | Test backlash repeatability | Consistent backlash gap observed in multiple direction changes |
| `Extended tracking should minimize RA backlash effects` | 🔄 | Test tracking backlash reduction | RA backlash effects diminish after extended tracking in one direction |
| `Consecutive west-to-east-to-west movements should show predictable backlash patterns` | 🔄 | Test cumulative backlash behavior | Direction change effects should be consistent and predictable |

### 3.4 Meridian Flip and Side-of-Pier Tests

| Test | Status | Description | Verification |
|------|--------|-------------|--------------|
| `Meridian flip should reverse Dec motor direction` | 🔄 | Test motor direction | Dec motor drives in opposite direction after flip |
| `Meridian flip should maintain pointing accuracy` | 🔄 | Test pointing continuity | Target object remains centered after flip |
| `Image orientation should rotate 180 degrees after flip` | 🔄 | Test image rotation | Image correctly rotated after meridian flip |
| `Guide calibration should adjust to side of pier` | 🔄 | Test guide calibration | Guide directions correctly adjusted after flip |
| `Mount should report correct side of pier` | 🔄 | Test pier side reporting | ASCOM property accurately reflects current configuration |
| `Repeated crossing of meridian should be handled stably` | 🔄 | Test meridian stability | Multiple crossings handled without issues |

### 3.5 Mount Integration Tests

| Test | Status | Description | Verification |
|------|--------|-------------|--------------|
| `Star field image shifts correctly during mount movement` | 🔄 | Test image-mount integration | Image position changes match mount movement |
| `Star trails form correctly during long exposures with tracking off` | 🔄 | Test star trailing | Stars trail proportionally to exposure and tracking differential |
| `Mount errors affect star positions in generated images` | 🔄 | Test error propagation | Mount errors visible in synthesized images |
| `ASCOM driver correctly handles client connections and disconnections` | 🔄 | Test client handling | Driver manages client state correctly |
| `ASCOM PulseGuide commands generate correct mount responses` | 🔄 | Test ASCOM guiding | Mount responds correctly to ASCOM guide commands |
| `ASCOM client can successfully control the mount` | 🔄 | Test ASCOM integration | Mount responds correctly to standard ASCOM clients |
| `Guide pulses show realistic backlash effect in images` | 🔄 | Test guiding in images | Images reflect proper guiding behavior including backlash |
| `Meridian flip should update camera/rotator orientation` | 🔄 | Test system integration during flip | All components maintain proper relationships after flip |

### 3.6 Additional Future Tests

| Test | Description | Verification |
|------|-------------|--------------|
| `Slewing near celestial poles` | Test slewing to coordinates near poles | Proper handling of coordinates near Dec = +/-90° |
| `Slewing across meridian` | Test slewing across meridian | Meridian flip triggered at appropriate point |
| `Lunar tracking mode` | Test lunar tracking | Moon centered during lunar tracking |
| `Solar tracking mode` | Test solar tracking | Sun centered during solar tracking |
| `Custom tracking rates` | Test custom tracking rates | Object centered with user-defined rates |
| `Periodic Error Correction recording` | Test PEC recording | Error patterns successfully recorded |
| `Periodic Error Correction playback` | Test PEC playback | Recorded patterns effectively reduce error |
| `Multiple cable snags in succession` | Test repeated cable snags | System recovers appropriately after multiple snags |
| `Mount imbalance effects` | Test imbalance on performance | Tracking affected by simulated imbalance |
| `Mount settling time after slewing` | Test mount settling | Oscillations dampen according to mount type |
| `Dithering patterns` | Test dithering for imaging | Small position shifts properly executed |
| `Long-duration tracking stability` | Test extended tracking | Position maintained accurately over hours |

Legend:
- ✅ Implemented and passing
- 🔄 Planned for future implementation

## 4. Recommended Test Strategies

### 4.1 Time Compression Testing

To effectively test behaviors that occur over long periods:

- Implement a time compression system to accelerate simulation
- Test hours of tracking in seconds of real time
- Verify long-period errors (polar alignment drift, periodic error cycles)
- Test backlash behavior over extended periods of tracking

### 4.2 Randomized Stress Testing

- Test with random sequences of commands (slew, guide, stop, start)
- Vary command timing to detect race conditions
- Inject random errors and verify graceful handling
- Test random patterns of east/west and north/south guide pulses

### 4.3 Real-World Scenario Testing

- Simulate typical astrophotography sessions (alignment, focusing, guiding)
- Test automated guiding scenarios with synthesized error patterns
- Compare with actual mount behavior logs from real equipment
- Simulate multi-hour sessions crossing the meridian

### 4.4 Differential Backlash Testing

- Test with varied backlash values in RA and Dec axes
- Measure response times for directional changes
- Test with differential backlash compensation settings
- Test backlash behavior during and after meridian flips

### 4.5 Side-of-Pier Behavior Testing

- Test mount behavior on both sides of pier
- Verify proper coordinate transformations after meridian flip
- Test guiding behavior before, during, and after meridian crossing
- Verify image orientation changes with side of pier