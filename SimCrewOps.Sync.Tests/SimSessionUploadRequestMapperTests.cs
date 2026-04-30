using SimCrewOps.Persistence.Models;
using SimCrewOps.Runtime.Models;
using SimCrewOps.Scoring.Models;
using SimCrewOps.Sync.Sync;
using Xunit;

namespace SimCrewOps.Sync.Tests;

public sealed class SimSessionUploadRequestMapperTests
{
    private static readonly string TrackerVersion = "3.0.0";

    private static PendingCompletedSession Session(FlightSessionRuntimeState state) =>
        new()
        {
            SessionId = "test-session",
            SavedUtc  = new DateTimeOffset(2026, 4, 15, 12, 0, 0, TimeSpan.Zero),
            State     = state,
        };

    private static FlightSessionRuntimeState BaseState(
        FlightScoreInput? scoreInput = null,
        FlightSessionBlockTimes? blockTimes = null) =>
        new()
        {
            Context = new FlightSessionContext
            {
                DepartureAirportIcao = "KJFK",
                ArrivalAirportIcao   = "KMIA",
                FlightMode           = "career",
            },
            CurrentPhase = FlightPhase.Arrival,
            BlockTimes   = blockTimes ?? new FlightSessionBlockTimes
            {
                BlocksOffUtc = new DateTimeOffset(2026, 4, 15, 8, 0, 0, TimeSpan.Zero),
                BlocksOnUtc  = new DateTimeOffset(2026, 4, 15, 11, 0, 0, TimeSpan.Zero),
            },
            ScoreInput  = scoreInput ?? new FlightScoreInput(),
            ScoreResult = new ScoreResult(100, 91, "A", false, Array.Empty<PhaseScoreResult>(), Array.Empty<ScoreFinding>()),
        };

    // ── Tracker version ──────────────────────────────────────────────────────

    [Fact]
    public void Map_UsesSuppliedTrackerVersion()
    {
        var request = new SimSessionUploadRequestMapper().Map(Session(BaseState()), "3.0.0");
        Assert.Equal("3.0.0", request.TrackerVersion);
    }

    // ── Block times ──────────────────────────────────────────────────────────

    [Fact]
    public void Map_CalculatesBlockTimeActualFromBlocksOffAndBlocksOn()
    {
        var blocksOff = new DateTimeOffset(2026, 4, 15, 8, 0, 0, TimeSpan.Zero);
        var state = BaseState(blockTimes: new FlightSessionBlockTimes
        {
            BlocksOffUtc = blocksOff,
            BlocksOnUtc  = blocksOff.AddHours(2.5),
        });

        var request = new SimSessionUploadRequestMapper().Map(Session(state), TrackerVersion);

        Assert.Equal(2.5, request.BlockTimeActual);
    }

    [Fact]
    public void Map_BlockTimeActualIsNullWhenBlocksOnMissing()
    {
        var state = BaseState(blockTimes: new FlightSessionBlockTimes
        {
            BlocksOffUtc           = new DateTimeOffset(2026, 4, 15, 8, 0, 0, TimeSpan.Zero),
            SessionEndTriggeredUtc = new DateTimeOffset(2026, 4, 15, 11, 0, 0, TimeSpan.Zero),
        });

        var request = new SimSessionUploadRequestMapper().Map(Session(state), TrackerVersion);

        Assert.Null(request.BlockTimeActual);
    }

    [Fact]
    public void Map_MapsWheelTimesFromBlockTimes()
    {
        var blocksOff = new DateTimeOffset(2026, 4, 15, 8, 0, 0, TimeSpan.Zero);
        var state = BaseState(blockTimes: new FlightSessionBlockTimes
        {
            BlocksOffUtc = blocksOff,
            WheelsOffUtc = blocksOff.AddMinutes(15),
            WheelsOnUtc  = blocksOff.AddMinutes(175),
            BlocksOnUtc  = blocksOff.AddMinutes(185),
        });

        var request = new SimSessionUploadRequestMapper().Map(Session(state), TrackerVersion);

        Assert.Equal(blocksOff,                    request.ActualBlocksOff);
        Assert.Equal(blocksOff.AddMinutes(15),  request.ActualWheelsOff);
        Assert.Equal(blocksOff.AddMinutes(175), request.ActualWheelsOn);
        Assert.Equal(blocksOff.AddMinutes(185), request.ActualBlocksOn);
    }

    // ── Landing scoring input ────────────────────────────────────────────────

    [Fact]
    public void Map_TouchdownRateFpm_IsNegativeInPayload()
    {
        var state = BaseState(scoreInput: new FlightScoreInput
        {
            Landing = new LandingMetrics { TouchdownVerticalSpeedFpm = 188 },
        });

        var request = new SimSessionUploadRequestMapper().Map(Session(state), TrackerVersion);

        Assert.Equal(-188, request.ScoringInput.Landing.TouchdownRateFpm);
    }

    [Fact]
    public void Map_LandingScoring_MapsAllFields()
    {
        var state = BaseState(scoreInput: new FlightScoreInput
        {
            Landing = new LandingMetrics
            {
                TouchdownVerticalSpeedFpm  = 220,
                TouchdownPitchAngleDegrees = 3.5,
                MaxPitchWhileWowDegrees    = 5.1,
                TouchdownBankAngleDegrees  = 0.8,
                TouchdownGForce            = 1.3,
                BounceCount                = 1,
                GearUpAtTouchdown          = false,
            },
        });

        var l = new SimSessionUploadRequestMapper().Map(Session(state), TrackerVersion).ScoringInput.Landing;

        Assert.Equal(-220, l.TouchdownRateFpm);
        Assert.Equal(3.5,  l.TouchdownPitchDeg);
        Assert.Equal(5.1,  l.MaxPitchWhileWowDeg);
        Assert.Equal(0.8,  l.TouchdownBankDeg);
        Assert.Equal(1.3,  l.TouchdownGForce);
        Assert.Equal(1,    l.BounceCount);
        Assert.False(l.GearUpAtTouchdown);
    }

    // ── Flight path ──────────────────────────────────────────────────────────

    [Fact]
    public void Map_MapsFlightPathPoints()
    {
        var state = BaseState(scoreInput: new FlightScoreInput
        {
            FlightPath = new[]
            {
                new FlightPathPoint { Lat = 40.641, Lon = -73.778, AltFt = 350,    TMin = 0    },
                new FlightPathPoint { Lat = 38.852, Lon = -77.037, AltFt = 35_000, TMin = 30.0 },
            },
        });

        var request = new SimSessionUploadRequestMapper().Map(Session(state), TrackerVersion);

        Assert.Equal(2,       request.FlightPath.Length);
        Assert.Equal(40.641,  request.FlightPath[0].Lat);
        Assert.Equal(-73.778, request.FlightPath[0].Lon);
        Assert.Equal(350,     request.FlightPath[0].AltFt);
        Assert.Equal(0,       request.FlightPath[0].TMin);
        Assert.Equal(35_000,  request.FlightPath[1].AltFt);
    }

    [Fact]
    public void Map_FlightPathIsEmptyWhenNoPoints()
    {
        var state = BaseState(scoreInput: new FlightScoreInput
        {
            FlightPath = Array.Empty<FlightPathPoint>(),
        });

        var request = new SimSessionUploadRequestMapper().Map(Session(state), TrackerVersion);

        Assert.Empty(request.FlightPath);
    }

    // ── Landing analysis / approach path ────────────────────────────────────

    [Fact]
    public void Map_LandingAnalysis_MapsApproachPathPoints()
    {
        var state = BaseState(scoreInput: new FlightScoreInput
        {
            ApproachPath = new[]
            {
                new ApproachPathPoint { Lat = 25.90, Lon = -80.35, AltFt = 2_600, IasKts = 210, VsFpm = -1_400 },
                new ApproachPathPoint { Lat = 25.81, Lon = -80.32, AltFt = 150,   IasKts = 145, VsFpm = -650   },
            },
        });

        var analysis = new SimSessionUploadRequestMapper().Map(Session(state), TrackerVersion).LandingAnalysis;

        Assert.Equal(2,       analysis.ApproachPath.Length);
        Assert.Equal(25.90,   analysis.ApproachPath[0].Lat);
        Assert.Equal(-80.35,  analysis.ApproachPath[0].Lon);
        Assert.Equal(2_600,   analysis.ApproachPath[0].AltitudeFt);
        Assert.Equal(210,     analysis.ApproachPath[0].IasKts);
        Assert.Equal(-1_400,  analysis.ApproachPath[0].VsFpm);
    }

    [Fact]
    public void Map_LandingAnalysis_ApproachPathIsEmptyWhenNoPoints()
    {
        var state = BaseState(scoreInput: new FlightScoreInput
        {
            ApproachPath = Array.Empty<ApproachPathPoint>(),
        });

        var analysis = new SimSessionUploadRequestMapper().Map(Session(state), TrackerVersion).LandingAnalysis;

        Assert.Empty(analysis.ApproachPath);
    }

    [Fact]
    public void Map_LandingAnalysis_DistanceToThresholdIsNullWhenNoTouchdownCoordinates()
    {
        var state = BaseState(scoreInput: new FlightScoreInput
        {
            LandingAnalysis = new LandingAnalysisData(),
            ApproachPath = new[]
            {
                new ApproachPathPoint { Lat = 25.90, Lon = -80.35, AltFt = 2_600, IasKts = 210, VsFpm = -1_400 },
            },
        });

        var analysis = new SimSessionUploadRequestMapper().Map(Session(state), TrackerVersion).LandingAnalysis;

        Assert.Single(analysis.ApproachPath);
        Assert.Null(analysis.ApproachPath[0].DistanceToThresholdNm);
    }

    [Fact]
    public void Map_LandingAnalysis_DistanceToThresholdIsComputedWhenTouchdownCoordinatesPresent()
    {
        var state = BaseState(scoreInput: new FlightScoreInput
        {
            LandingAnalysis = new LandingAnalysisData
            {
                TouchdownLat = 25.796,
                TouchdownLon = -80.284,
            },
            ApproachPath = new[]
            {
                new ApproachPathPoint { Lat = 25.880, Lon = -80.350, AltFt = 2_600, IasKts = 210, VsFpm = -1_400 },
            },
        });

        var analysis = new SimSessionUploadRequestMapper().Map(Session(state), TrackerVersion).LandingAnalysis;

        Assert.Single(analysis.ApproachPath);
        Assert.NotNull(analysis.ApproachPath[0].DistanceToThresholdNm);
        Assert.True(analysis.ApproachPath[0].DistanceToThresholdNm > 0);
    }

    [Fact]
    public void Map_LandingAnalysis_MapsWindAndCoordinateFields()
    {
        var state = BaseState(scoreInput: new FlightScoreInput
        {
            LandingAnalysis = new LandingAnalysisData
            {
                TouchdownLat                = 25.796,
                TouchdownLon                = -80.284,
                TouchdownHeadingMagneticDeg = 100.0,
                TouchdownAltFt              = 8.0,
                TouchdownIAS                = 138.5,
                WindSpeedKnots              = 12.0,
                WindDirectionDegrees        = 270.0,
            },
        });

        var analysis = new SimSessionUploadRequestMapper().Map(Session(state), TrackerVersion).LandingAnalysis;

        Assert.Equal(25.796,  analysis.TouchdownLat);
        Assert.Equal(-80.284, analysis.TouchdownLon);
        Assert.Equal(100.0,   analysis.TouchdownHeadingDeg);
        Assert.Equal(8.0,     analysis.TouchdownAltFt);
        Assert.Equal(138.5,   analysis.TouchdownIAS);
        Assert.Equal(12.0,    analysis.WindSpeedAtTouchdownKnots);
        Assert.Equal(270.0,   analysis.WindDirectionAtTouchdownDegrees);
    }
}
