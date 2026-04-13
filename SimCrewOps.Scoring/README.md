# SimCrewOps.Scoring

A simulator-agnostic C# scoring engine for phase-based airline-ops grading.

## What it covers

- 10 flight phases:
  - Preflight
  - Taxi Out
  - Takeoff
  - Climb
  - Cruise
  - Descent
  - Approach
  - Landing
  - Taxi In
  - Arrival
- Global safety deductions:
  - crash/reset
  - overspeed
  - sustained overspeed
  - stall warning
  - GPWS
  - engine shutdown in flight

## Main types

- `FlightScoreInput`
  - normalized metrics gathered from the tracker
- `ScoringEngine`
  - computes phase results, global deductions, final score, and grade
- `ScoreResult`
  - output model for UI, API, and persisted reports

## Current assumptions

- Taxi speed target is 40 knots, with full phase-speed penalty by 60 knots.
- Turn-speed penalties are event-based.
- Tail strike is a precomputed boolean from the telemetry engine.
- Takeoff bounce is separate from landing bounce.
- Approach stabilization is evaluated at 500 feet AGL.
- Non-landing G-force thresholds are conservative defaults and can be tuned.

## Example

```csharp
using SimCrewOps.Scoring.Models;
using SimCrewOps.Scoring.Scoring;

var engine = new ScoringEngine();

var result = engine.Calculate(new FlightScoreInput
{
    Preflight = new PreflightMetrics { BeaconOnBeforeTaxi = true },
    TaxiOut = new TaxiMetrics { MaxGroundSpeedKnots = 23, TaxiLightsOn = true },
    Takeoff = new TakeoffMetrics
    {
        MaxBankAngleDegrees = 8,
        MaxPitchAngleDegrees = 13,
        LandingLightsOnBeforeTakeoff = true,
        LandingLightsOffByFl180 = true,
        StrobesOnFromTakeoffToLanding = true
    },
    Climb = new ClimbMetrics { MaxIasBelowFl100Knots = 246, MaxBankAngleDegrees = 18, MaxGForce = 1.2 },
    Cruise = new CruiseMetrics { MaxAltitudeDeviationFeet = 45, MaxBankAngleDegrees = 4, MaxGForce = 1.05 },
    Descent = new DescentMetrics { MaxIasBelowFl100Knots = 247, MaxBankAngleDegrees = 14, MaxPitchAngleDegrees = 5, MaxGForce = 1.15, LandingLightsOnByFl180 = true },
    Approach = new ApproachMetrics { GearDownBy1000Agl = true, FlapsHandleIndexAt500Agl = 3, VerticalSpeedAt500AglFpm = 730, BankAngleAt500AglDegrees = 4, PitchAngleAt500AglDegrees = 3, GearDownAt500Agl = true },
    Landing = new LandingMetrics { TouchdownZoneExcessDistanceFeet = 120, TouchdownVerticalSpeedFpm = 145, TouchdownGForce = 1.18, BounceCount = 0 },
    TaxiIn = new TaxiInMetrics { LandingLightsOff = true, StrobesOff = true, MaxGroundSpeedKnots = 18, TaxiLightsOn = true },
    Arrival = new ArrivalMetrics { ParkingBrakeSetAtGate = true, GateArrivalDistanceFeet = 12 },
    Safety = new SafetyMetrics()
});

Console.WriteLine($"{result.FinalScore} / {result.MaximumScore} ({result.Grade})");
```
