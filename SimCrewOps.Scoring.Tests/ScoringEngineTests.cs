using SimCrewOps.Scoring.Models;
using SimCrewOps.Scoring.Scoring;

namespace SimCrewOps.Scoring.Tests;

public sealed class ScoringEngineTests
{
    private readonly ScoringEngine _engine = new();

    [Fact]
    public void PerfectFlightScoresAPlus()
    {
        var result = _engine.Calculate(new FlightScoreInput
        {
            Preflight = new PreflightMetrics { BeaconOnBeforeTaxi = true },
            TaxiOut = new TaxiMetrics { MaxGroundSpeedKnots = 18, ExcessiveTurnSpeedEvents = 0, TaxiLightsOn = true },
            Takeoff = new TakeoffMetrics
            {
                BounceCount = 0,
                TailStrikeDetected = false,
                MaxBankAngleDegrees = 12,
                MaxPitchAngleDegrees = 14,
                LandingLightsOnBeforeTakeoff = true,
                LandingLightsOffByFl180 = true,
                StrobesOnFromTakeoffToLanding = true,
            },
            Climb = new ClimbMetrics
            {
                HeavyFourEngineAircraft = false,
                MaxIasBelowFl100Knots = 248,
                MaxBankAngleDegrees = 20,
                MaxGForce = 1.30,
            },
            Cruise = new CruiseMetrics
            {
                MaxAltitudeDeviationFeet = 60,
                NewFlightLevelCaptureSeconds = 35,
                SpeedInstabilityEvents = 0,
                MaxBankAngleDegrees = 6,
                MaxGForce = 1.08,
            },
            Descent = new DescentMetrics
            {
                MaxIasBelowFl100Knots = 246,
                MaxBankAngleDegrees = 18,
                MaxPitchAngleDegrees = 5,
                MaxGForce = 1.20,
                LandingLightsOnByFl180 = true,
            },
            Approach = new ApproachMetrics
            {
                GearDownBy1000Agl = true,
                FlapsHandleIndexAt500Agl = 3,
                VerticalSpeedAt500AglFpm = 720,
                BankAngleAt500AglDegrees = 4,
                PitchAngleAt500AglDegrees = 3,
                GearDownAt500Agl = true,
            },
            Landing = new LandingMetrics
            {
                TouchdownZoneExcessDistanceFeet = 0,
                TouchdownVerticalSpeedFpm = 180,
                TouchdownGForce = 1.15,
                BounceCount = 0,
            },
            TaxiIn = new TaxiInMetrics
            {
                LandingLightsOff = true,
                StrobesOff = true,
                MaxGroundSpeedKnots = 16,
                ExcessiveTurnSpeedEvents = 0,
                TaxiLightsOn = true,
            },
            Arrival = new ArrivalMetrics
            {
                ParkingBrakeSetAtGate = true,
                GateArrivalDistanceFeet = 12,
            },
            Safety = new SafetyMetrics(),
        });

        Assert.Equal(100, result.FinalScore);
        Assert.Equal("A+", result.Grade);
        Assert.False(result.AutomaticFail);
    }

    [Fact]
    public void CrashCausesAutomaticFail()
    {
        var result = _engine.Calculate(new FlightScoreInput
        {
            Preflight = new PreflightMetrics { BeaconOnBeforeTaxi = true },
            TaxiOut = new TaxiMetrics { MaxGroundSpeedKnots = 20, TaxiLightsOn = true },
            Takeoff = new TakeoffMetrics { LandingLightsOnBeforeTakeoff = true, LandingLightsOffByFl180 = true, StrobesOnFromTakeoffToLanding = true },
            Climb = new ClimbMetrics(),
            Cruise = new CruiseMetrics(),
            Descent = new DescentMetrics { LandingLightsOnByFl180 = true },
            Approach = new ApproachMetrics { GearDownBy1000Agl = true, FlapsHandleIndexAt500Agl = 2, GearDownAt500Agl = true },
            Landing = new LandingMetrics(),
            TaxiIn = new TaxiInMetrics { LandingLightsOff = true, StrobesOff = true, TaxiLightsOn = true },
            Arrival = new ArrivalMetrics { ParkingBrakeSetAtGate = true },
            Safety = new SafetyMetrics { CrashDetected = true },
        });

        Assert.True(result.AutomaticFail);
        Assert.Equal("F", result.Grade);
        Assert.Contains(result.GlobalFindings, finding => finding.Code == "SAFETY_CRASH");
    }

    [Fact]
    public void UnstableApproachAndHardLandingReduceScore()
    {
        var result = _engine.Calculate(new FlightScoreInput
        {
            Preflight = new PreflightMetrics { BeaconOnBeforeTaxi = true },
            TaxiOut = new TaxiMetrics { MaxGroundSpeedKnots = 22, TaxiLightsOn = true },
            Takeoff = new TakeoffMetrics
            {
                MaxBankAngleDegrees = 16,
                MaxPitchAngleDegrees = 16,
                LandingLightsOnBeforeTakeoff = true,
                LandingLightsOffByFl180 = true,
                StrobesOnFromTakeoffToLanding = true,
            },
            Climb = new ClimbMetrics { MaxIasBelowFl100Knots = 249, MaxBankAngleDegrees = 18, MaxGForce = 1.25 },
            Cruise = new CruiseMetrics { MaxAltitudeDeviationFeet = 80, MaxBankAngleDegrees = 5, MaxGForce = 1.10 },
            Descent = new DescentMetrics { MaxIasBelowFl100Knots = 248, MaxBankAngleDegrees = 16, MaxPitchAngleDegrees = 4, MaxGForce = 1.15, LandingLightsOnByFl180 = true },
            Approach = new ApproachMetrics
            {
                GearDownBy1000Agl = false,
                FlapsHandleIndexAt500Agl = 1,
                VerticalSpeedAt500AglFpm = 1200,
                BankAngleAt500AglDegrees = 14,
                PitchAngleAt500AglDegrees = 12,
                GearDownAt500Agl = false,
            },
            Landing = new LandingMetrics
            {
                TouchdownZoneExcessDistanceFeet = 900,
                TouchdownVerticalSpeedFpm = 500,   // in 400–600 progressive penalty zone
                TouchdownGForce = 1.40,            // in 1.3–1.5 progressive penalty zone
                BounceCount = 1,
            },
            TaxiIn = new TaxiInMetrics
            {
                LandingLightsOff = true,
                StrobesOff = true,
                MaxGroundSpeedKnots = 18,
                TaxiLightsOn = true,
            },
            Arrival = new ArrivalMetrics { ParkingBrakeSetAtGate = true, GateArrivalDistanceFeet = 18 },
            Safety = new SafetyMetrics(),
        });

        Assert.True(result.FinalScore < 85);
        Assert.False(result.AutomaticFail);
        Assert.Contains(result.PhaseScores.Single(phase => phase.Phase == FlightPhase.Approach).Findings, finding => finding.Code == "APPROACH_GEAR_1000");
        Assert.Contains(result.PhaseScores.Single(phase => phase.Phase == FlightPhase.Landing).Findings, finding => finding.Code == "LANDING_BOUNCE");
    }

    [Fact]
    public void HardLanding_ExcessiveVerticalSpeed_CausesAutomaticFail()
    {
        var result = _engine.Calculate(new FlightScoreInput
        {
            Preflight = new PreflightMetrics { BeaconOnBeforeTaxi = true },
            TaxiOut = new TaxiMetrics { MaxGroundSpeedKnots = 20, TaxiLightsOn = true },
            Takeoff = new TakeoffMetrics { LandingLightsOnBeforeTakeoff = true, LandingLightsOffByFl180 = true, StrobesOnFromTakeoffToLanding = true },
            Climb = new ClimbMetrics(),
            Cruise = new CruiseMetrics(),
            Descent = new DescentMetrics { LandingLightsOnByFl180 = true },
            Approach = new ApproachMetrics { GearDownBy1000Agl = true, FlapsHandleIndexAt500Agl = 3, GearDownAt500Agl = true },
            Landing = new LandingMetrics
            {
                TouchdownZoneExcessDistanceFeet = 0,
                TouchdownVerticalSpeedFpm = 650,   // above 600 fpm auto-fail threshold
                TouchdownGForce = 1.20,
                BounceCount = 0,
            },
            TaxiIn = new TaxiInMetrics { LandingLightsOff = true, StrobesOff = true, TaxiLightsOn = true },
            Arrival = new ArrivalMetrics { ParkingBrakeSetAtGate = true },
            Safety = new SafetyMetrics(),
        });

        Assert.True(result.AutomaticFail);
        Assert.Equal("F", result.Grade);
        var landingFindings = result.PhaseScores.Single(ps => ps.Phase == FlightPhase.Landing).Findings;
        Assert.Contains(landingFindings, f => f.Code == "LANDING_VERTICAL_SPEED" && f.IsAutomaticFail);
    }

    [Fact]
    public void HardLanding_ExcessiveGForce_CausesAutomaticFail()
    {
        var result = _engine.Calculate(new FlightScoreInput
        {
            Preflight = new PreflightMetrics { BeaconOnBeforeTaxi = true },
            TaxiOut = new TaxiMetrics { MaxGroundSpeedKnots = 20, TaxiLightsOn = true },
            Takeoff = new TakeoffMetrics { LandingLightsOnBeforeTakeoff = true, LandingLightsOffByFl180 = true, StrobesOnFromTakeoffToLanding = true },
            Climb = new ClimbMetrics(),
            Cruise = new CruiseMetrics(),
            Descent = new DescentMetrics { LandingLightsOnByFl180 = true },
            Approach = new ApproachMetrics { GearDownBy1000Agl = true, FlapsHandleIndexAt500Agl = 3, GearDownAt500Agl = true },
            Landing = new LandingMetrics
            {
                TouchdownZoneExcessDistanceFeet = 0,
                TouchdownVerticalSpeedFpm = 200,
                TouchdownGForce = 1.6,             // above 1.5G auto-fail threshold
                BounceCount = 0,
            },
            TaxiIn = new TaxiInMetrics { LandingLightsOff = true, StrobesOff = true, TaxiLightsOn = true },
            Arrival = new ArrivalMetrics { ParkingBrakeSetAtGate = true },
            Safety = new SafetyMetrics(),
        });

        Assert.True(result.AutomaticFail);
        Assert.Equal("F", result.Grade);
        var landingFindings = result.PhaseScores.Single(ps => ps.Phase == FlightPhase.Landing).Findings;
        Assert.Contains(landingFindings, f => f.Code == "LANDING_GFORCE" && f.IsAutomaticFail);
    }
}
