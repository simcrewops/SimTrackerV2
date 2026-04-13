using SimCrewOps.Scoring.Models;
using SimCrewOps.Scoring.Scoring;
using Xunit;

namespace SimCrewOps.Scoring.Tests;

public sealed class ScoringEngineTests
{
    private readonly ScoringEngine _engine = new();

    [Fact]
    public void PerfectFlightScoresA()
    {
        var result = _engine.Calculate(BuildBaselineInput());

        Assert.Equal(100, result.FinalScore);
        Assert.Equal("A", result.Grade);
        Assert.False(result.AutomaticFail);
    }

    [Fact]
    public void CrashCausesAutomaticFail()
    {
        var result = _engine.Calculate(BuildBaselineInput() with
        {
            Safety = new SafetyMetrics { CrashDetected = true },
        });

        Assert.True(result.AutomaticFail);
        Assert.Equal("F", result.Grade);
        Assert.Contains(result.GlobalFindings, finding => finding.Code == "SAFETY_CRASH");
    }

    [Fact]
    public void UnstableApproachAndHardLandingReduceScore()
    {
        var result = _engine.Calculate(BuildBaselineInput() with
        {
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
                TouchdownZoneExcessDistanceFeet = 220,
                TouchdownVerticalSpeedFpm = 500,
                TouchdownGForce = 1.60,
                BounceCount = 1,
            },
        });

        Assert.True(result.FinalScore < 85);
        Assert.False(result.AutomaticFail);
        Assert.Contains(result.PhaseScores.Single(phase => phase.Phase == FlightPhase.Approach).Findings, finding => finding.Code == "APPROACH_GEAR_1000");
        Assert.Contains(result.PhaseScores.Single(phase => phase.Phase == FlightPhase.Landing).Findings, finding => finding.Code == "LANDING_BOUNCE");
    }

    [Fact]
    public void HardLanding_ExcessiveVerticalSpeed_FailsLandingSectionOnly()
    {
        var result = _engine.Calculate(BuildBaselineInput() with
        {
            Landing = new LandingMetrics
            {
                TouchdownZoneExcessDistanceFeet = 0,
                TouchdownVerticalSpeedFpm = 650,
                TouchdownGForce = 1.20,
                BounceCount = 0,
            },
        });

        var landing = result.PhaseScores.Single(ps => ps.Phase == FlightPhase.Landing);
        Assert.False(result.AutomaticFail);
        Assert.True(landing.SectionFailed);
        Assert.Equal(0, landing.AwardedPoints);
        Assert.Equal("B", result.Grade);
        Assert.Contains(landing.Findings, f => f.Code == "LANDING_VERTICAL_SPEED" && f.IsAutomaticFail);
    }

    [Fact]
    public void HardLanding_ExcessiveGForce_FailsLandingSectionOnly()
    {
        var result = _engine.Calculate(BuildBaselineInput() with
        {
            Landing = new LandingMetrics
            {
                TouchdownZoneExcessDistanceFeet = 0,
                TouchdownVerticalSpeedFpm = 200,
                TouchdownGForce = 2.10,
                BounceCount = 0,
            },
        });

        var landing = result.PhaseScores.Single(ps => ps.Phase == FlightPhase.Landing);
        Assert.False(result.AutomaticFail);
        Assert.True(landing.SectionFailed);
        Assert.Equal(0, landing.AwardedPoints);
        Assert.Equal("B", result.Grade);
        Assert.Contains(landing.Findings, f => f.Code == "LANDING_GFORCE" && f.IsAutomaticFail);
    }

    [Fact]
    public void LandingThresholdEdges_MaxOutDeductionsWithoutSectionFail()
    {
        var result = _engine.Calculate(BuildBaselineInput() with
        {
            Landing = new LandingMetrics
            {
                TouchdownZoneExcessDistanceFeet = 0,
                TouchdownVerticalSpeedFpm = 600,
                TouchdownGForce = 2.0,
                BounceCount = 0,
            },
        });

        var landing = result.PhaseScores.Single(ps => ps.Phase == FlightPhase.Landing);

        Assert.False(result.AutomaticFail);
        Assert.False(landing.SectionFailed);
        Assert.Equal(8, landing.AwardedPoints);
        Assert.DoesNotContain(landing.Findings, f => f.IsAutomaticFail);
    }

    [Fact]
    public void SafetyDeductionsRespectCaps()
    {
        var result = _engine.Calculate(BuildBaselineInput() with
        {
            Safety = new SafetyMetrics
            {
                OverspeedEvents = 5,
                SustainedOverspeedEvents = 5,
                StallEvents = 5,
                GpwsEvents = 5,
                EngineShutdownsInFlight = 2,
            },
        });

        Assert.Equal(9 + 10 + 9 + 9 + 10, result.GlobalDeductions);
        Assert.Equal(53, result.FinalScore);
        Assert.Equal("F", result.Grade);
    }

    private static FlightScoreInput BuildBaselineInput() =>
        new()
        {
            Preflight = new PreflightMetrics { BeaconOnBeforeTaxi = true },
            TaxiOut = new TaxiMetrics { MaxGroundSpeedKnots = 18, ExcessiveTurnSpeedEvents = 0, TaxiLightsOn = true },
            Takeoff = new TakeoffMetrics
            {
                BounceCount = 0,
                TailStrikeDetected = false,
                MaxBankAngleDegrees = 12,
                MaxPitchAngleDegrees = 14,
                MaxGForce = 1.20,
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
                TaxiLightsOffBeforeParkingBrakeSet = true,
                ParkingBrakeSetBeforeAllEnginesShutdown = true,
                AllEnginesOffByEndOfSession = true,
            },
            Safety = new SafetyMetrics(),
        };
}
