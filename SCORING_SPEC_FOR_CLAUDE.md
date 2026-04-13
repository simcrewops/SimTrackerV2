# SimCrewOps Tracker Scoring Spec

This document is the current source of truth for flight scoring behavior in SimCrewOps Tracker.

It is written as an engineering handoff: what must be scored, how thresholds behave, what counts as a section fail, and what telemetry the system needs.

## Core Principles

- Scoring is phase-based.
- Thresholds should be gradual whenever possible, not binary, unless explicitly called out as a section fail.
- A section can receive an `F` without failing the whole flight.
- Whole-flight automatic fail should be reserved for severe safety outcomes such as crash/reset unless policy changes later.
- Point values should remain configurable.
- Thresholds and detection logic should remain stable and explicit.

## Sections

The tracker scores these ten sections:

1. `Preflight`
2. `Taxi Out`
3. `Takeoff`
4. `Climb`
5. `Cruise`
6. `Descent`
7. `Approach`
8. `Landing`
9. `Taxi In`
10. `Arrival`

There is also a separate layer of global safety deductions.

## Grade Scale

Use `A` through `F` only. There is no `A+`.

- `A`: `90–100`
- `B`: `80–89`
- `C`: `70–79`
- `D`: `60–69`
- `F`: below `60`

## Section Weights

The section weights total `100` points:

- `Preflight`: `5`
- `Taxi Out`: `8`
- `Takeoff`: `10`
- `Climb`: `10`
- `Cruise`: `10`
- `Descent`: `10`
- `Approach`: `12`
- `Landing`: `20`
- `Taxi In`: `8`
- `Arrival`: `7`

## Global Safety Deductions

- `Overspeed event`: `3` points each, max `9`
- `Sustained overspeed (30s+)`: `5` points each, max `10`
- `Stall warning`: `3` points each, max `9`
- `GPWS alert`: `3` points each, max `9`
- `Engine shutdown in flight`: `5` points each, no cap
- `Crash / sim reset`: whole-flight `F`

## Important Clarifications

These are confirmed user decisions and should be treated as locked unless changed later.

- `Takeoff bounce` exists to detect when the aircraft lifts off and comes back down during takeoff.
- `Tail strike` is detected when the wheels are still on the ground and pitch is `>= 10 degrees`.
- `Stabilized by 500 AGL` is the correct approach rule.
- Landing vertical speed:
  - `<= 300 fpm` = full credit
  - `300 to 600 fpm` = gradual deduction
  - `> 600 fpm` = `F` for the `Landing` section only
- Landing G-force:
  - `<= 1.2G` = full credit
  - `1.2G to 2.0G` = gradual deduction
  - `> 2.0G` = `F` for the `Landing` section only

## Section Fail vs Flight Fail

### Section Fail

A section fail means:

- that section receives an `F`
- that section can be scored as zero or otherwise forced to fail by policy
- the rest of the flight still scores normally

Current confirmed section-fail rules:

- `Landing` section fails if touchdown vertical speed is `> 600 fpm`
- `Landing` section fails if touchdown G-force is `> 2.0G`

### Flight Fail

A full-flight fail should be reserved for severe safety events such as:

- crash
- sim reset

This document does not currently require a hard whole-flight fail for hard landing alone.

## Scoring Model

The recommended implementation model is:

- each section has one or more scoring items
- each scoring item has:
  - a rule
  - one or more thresholds
  - a deduction style
- the section score is calculated from its item-level results
- section scores roll into the final score
- global safety deductions are applied separately

For threshold-based gradual deductions, use linear interpolation unless a better curve is explicitly required later.

Example linear rule:

- no deduction at the full-credit threshold
- full deduction at the fail threshold
- linearly interpolate between them

## Phase-by-Phase Rules

### 1. Preflight

#### Items

- Beacon light on before taxi

#### Rule

- If the aircraft begins taxi before the beacon light has been on, deduct points in `Preflight`

#### Detection

- Track whether beacon was seen on before the first valid taxi-out movement

### 2. Taxi Out

#### Items

- Ground speed less than `40 knots`
- High-speed turns must be less than `15 knots` on turns greater than `45 degrees`
- Taxi lights on

#### Rules

- Penalize taxi speed above `40 knots`
- Penalize a turn event when:
  - heading change is `> 45 degrees`
  - ground speed is `> 15 knots`
- Taxi lights must be on during taxi out

#### Recommended Detection

- `MaxGroundSpeedTaxiOut`
- `TaxiOutTurnSpeedEventCount`
- `TaxiLightsValidDuringTaxiOut`

### 3. Takeoff

#### Items

- Takeoff bounce count
- Tail strike
- Bank angle no more than `30 degrees`
- Pitch no more than `20 degrees`
- Landing lights on before takeoff
- Landing lights off before `FL180`
- Strobes on from takeoff to landing

#### Rules

- Penalize each takeoff bounce event
- Penalize any tail strike event
- Penalize bank angle above `30 degrees`
- Penalize pitch above `20 degrees`
- Takeoff G-force:
  - `<= 1.5G` = full credit
  - `1.5G to 2.5G` = gradual deduction
  - max takeoff G-force deduction = `3` points
- Landing lights must be on before takeoff roll
- Landing lights must be off by `FL180`
- Strobes must remain on from takeoff through landing

#### Detection

##### Takeoff Bounce

Takeoff bounce means:

- aircraft leaves the ground during takeoff
- then returns to the runway before establishing climb

Recommended detection:

- previous `OnGround = true`
- next `OnGround = false`
- then `OnGround = true` again
- still in takeoff context
- low AGL window, for example `< 100 ft`

##### Tail Strike

Tail strike means:

- wheels still on ground
- pitch `>= 10 degrees`

Recommended gating:

- runway/takeoff context
- significant speed, for example `IAS >= 40 knots`

### 4. Climb

#### Items

- Speed compliance below `FL100`
- Bank angle no more than `30 degrees`
- G-force

#### Rules

- Standard aircraft:
  - max `250 knots` below `FL100`
- Heavy four-engine aircraft:
  - max `300 knots` below `FL100`
- Penalize bank angle above `30 degrees`
- Climb G-force:
  - `<= 1.7G` = full credit
  - `1.7G to 2.5G` = gradual deduction
  - max climb G-force deduction = `3` points

#### Detection

- `MaxIASBelowFL100_Climb`
- `MaxBankClimb`
- `MaxGForceClimb`

### 5. Cruise

#### Items

- Altitude hold within `+/- 100 feet`
- Auto-detect new flight level after `60 seconds`
- Speed/Mach stability
- Bank angle
- G-force

#### Rules

- Penalize altitude drift outside `+/- 100 feet`
- If aircraft transitions to a new cruise altitude, treat it as the new target after `60 seconds`
- Cruise speed instability event:
  - trigger when `IAS delta > 15 knots` or `Mach delta > 0.03`
  - condition must be sustained for `5+ seconds`
  - use `10-second` cooldown between counted events
  - deduct `1` point per event
  - cap total cruise speed-instability deduction at `4` points
- Penalize excessive bank
- Cruise G-force:
  - `<= 1.4G` = full credit
  - `1.4G to 2.0G` = gradual deduction
  - max cruise G-force deduction = `2` points

#### Detection

- `MaxCruiseAltitudeDeviation`
- `FlightLevelCaptureTime`
- `CruiseSpeedInstabilityEventCount`
- `MaxCruiseBank`
- `MaxCruiseGForce`

### 6. Descent

#### Items

- Speed compliance below `FL100`
- Bank angle
- Pitch no more than `20 degrees`
- G-force
- Landing lights on from `FL180` to landing

#### Rules

- Max `250 knots` below `FL100`
- Penalize excessive bank
- Penalize pitch above `20 degrees`
- Descent G-force:
  - `<= 1.7G` = full credit
  - `1.7G to 2.5G` = gradual deduction
  - max descent G-force deduction = `3` points
- Landing lights must be on by `FL180` and remain on until landing

#### Detection

- `MaxIASBelowFL100_Descent`
- `MaxDescentBank`
- `MaxDescentPitch`
- `MaxDescentGForce`
- `LandingLightsValidFromFL180`

### 7. Approach

#### Definition

Approach begins at approximately `FL100` and runs down to `2000 AGL`.

#### Items

- Gear down by `1000 AGL`
- Flap position greater than `1`
- Stabilized by `500 AGL`
- Vertical speed not greater than `1000 fpm`
- Bank angle not greater than `10 degrees`
- Pitch not greater than `10 degrees`
- Gear down at stabilization gate

#### Rules

- Gear must be down by `1000 AGL`
- At `500 AGL`, the aircraft must be stabilized

#### Stabilized by 500 AGL means

- vertical speed `<= 1000 fpm`
- bank `<= 10 degrees`
- pitch `<= 10 degrees`
- gear down
- flaps `> 1`

#### Detection

At first crossing of `1000 AGL`:

- capture `GearDownBy1000Agl`

At first crossing of `500 AGL`:

- capture vertical speed
- capture bank
- capture pitch
- capture gear state
- capture flap state

### 8. Landing

#### Items

- Touchdown zone performance
- Touchdown vertical speed
- Touchdown G-force
- Bounce count

#### 8.1 Touchdown Zone

Rule:

- The farther outside the touchdown zone, the more points are lost

Implementation note:

- This should be based on excess distance outside the valid touchdown zone, not merely any nonzero distance from threshold

#### 8.2 Touchdown Vertical Speed

Confirmed thresholds:

- `<= 300 fpm` = full credit
- `300 to 600 fpm` = gradual deduction
- `> 600 fpm` = `F` for `Landing` section only

Recommended detection:

- Do not use only the exact touchdown frame
- Use the peak negative vertical speed from a short rolling buffer immediately before touchdown
- Recommended initial window: `2 seconds`

#### 8.3 Touchdown G-Force

Confirmed thresholds:

- `<= 1.2G` = full credit
- `1.2G to 2.0G` = gradual deduction
- `> 2.0G` = `F` for `Landing` section only

Recommended detection:

- Use the highest G-force observed in the immediate touchdown window

#### 8.4 Landing Bounce

Rule:

- Penalize each landing bounce

Recommended detection:

- touchdown
- airborne again
- touchdown again
- within a short post-touchdown window such as `3 seconds`

### 9. Taxi In

#### Items

- Landing lights off
- Strobes off
- Ground speed less than `40 knots`
- High-speed turns less than `15 knots` on turns greater than `45 degrees`
- Taxi lights on

#### Rules

- Landing lights must be off during taxi in
- Strobes must be off during taxi in
- Penalize taxi speed above `40 knots`
- Penalize turn event when:
  - heading change is `> 45 degrees`
  - ground speed is `> 15 knots`
- Taxi lights must be on during taxi in

### 10. Arrival

#### Items

- Taxi lights off before parking brake set
- Parking brake set before all engines are shut down
- All engines off by end of session

#### Ordered Rules

- Taxi lights must be turned off before parking brake is set
- Parking brake must be set before all engines are off
- One engine may shut down before parking brake for single-engine taxi
- Violation occurs only if all engines are off before parking brake is set
- All engines must be shut down by the end of the session

## Bounce Logic Summary

There are two separate bounce concepts and they must not be merged.

### Takeoff Bounce

- liftoff
- then return to runway
- still in takeoff context

### Landing Bounce

- touchdown
- airborne again
- touchdown again
- during landing rollout window

## Global Safety Layer

These are not tied to one scoring section and should be tracked across the whole flight:

- crash / sim reset
- overspeed events
- sustained overspeed events
- stall warning events
- GPWS alert events
- engine shutdown in flight

### Current Recommendation

- `Crash` or `sim reset` may justify whole-flight fail
- the others should generally deduct points, not necessarily fail the whole flight

## Required Data Inputs

At minimum, the scoring system or telemetry accumulator needs:

- `OnGround`
- `Pitch`
- `Bank`
- `VerticalSpeed`
- `GroundSpeed`
- `IndicatedAirspeed`
- `Altitude`
- `AltitudeAGL`
- `Heading`
- `GForce`
- `GearState`
- `FlapState`
- `BeaconLight`
- `TaxiLight`
- `LandingLight`
- `StrobeLight`
- `CrashReset`
- `StallWarning`
- `GPWSAlert`
- `Overspeed`
- `EngineRunningState`
- `TouchdownZoneDistance` or touchdown-zone excess metric
- taxi-light, parking-brake, and engine-state sequencing at shutdown

## Implementation Notes for Claude

### Recommended Architecture

- Keep thresholds and point weights separate
- Keep `section fail` separate from `flight fail`
- Keep detection logic separate from scoring logic
- Keep all point values configurable

### Recommended Output Shape

For each section, return:

- section name
- raw metrics used
- findings / violations
- points awarded
- points deducted
- `SectionFailed` boolean
- optional section grade

For full flight, return:

- all section results
- global safety findings
- total score
- overall grade
- `FlightFailed` boolean

### Recommended Gradual Threshold Formula

For a metric with:

- good threshold `A`
- fail threshold `B`

Use:

- full credit if `value <= A`
- section fail if `value > B` when the metric is defined as section-fail capable
- otherwise linear deduction from `A` to `B`

Pseudo-form:

```text
if value <= A:
    deduction = 0
elif value > B and metric_can_fail_section:
    section_failed = true
else:
    deduction = max_metric_points * ((value - A) / (B - A))
```

## Open Items

These still need tuning or final definition:

- touchdown zone geometry method
- whether any non-crash safety items should become whole-flight fail conditions

## Flight Phase Engine

The repo already contains a `FlightPhaseEngine`, and it must continue to support:

- all `10` phase transitions
- `BlocksOff`
- `WheelsOff`
- `WheelsOn`
- `BlocksOn`
- go-around
- aborted takeoff
- touch-and-go

## One-Block Summary

SimCrewOps scoring is phase-based across ten sections weighted to 100 points total. Grade scale is A through F only, with A starting at 90 and F below 60. Use gradual deductions where possible. Takeoff bounce means liftoff followed by coming back down during takeoff. Tail strike means wheels still on ground with pitch at or above 10 degrees. Approach must be stabilized by 500 AGL, which means vertical speed at or below 1000 fpm, bank at or below 10 degrees, pitch at or below 10 degrees, gear down, and flaps greater than 1. Landing vertical speed gets full credit through 300 fpm, declines gradually to 600 fpm, and above 600 fpm fails the Landing section only. Landing G-force gets full credit through 1.2G, declines gradually to 2.0G, and above 2.0G fails the Landing section only. Section fail is not the same as whole-flight fail. Global safety deductions apply caps for overspeed, sustained overspeed, stall, and GPWS, while crash/reset is a whole-flight fail. Arrival now scores shutdown order rather than gate precision.
