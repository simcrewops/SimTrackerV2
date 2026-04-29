using SimCrewOps.Persistence.Models;
using SimCrewOps.Runtime.Models;
using SimCrewOps.Scoring.Models;
using SimCrewOps.Sync.Models;
using SimCrewOps.Sync.Sync;
using Xunit;

namespace SimCrewOps.Sync.Tests;

public sealed class SimSessionPayloadTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    // ── Golden shape ──────────────────────────────────────────────────────────

    [Fact]
    public void GoldenPayload_KeyFieldsMatchFixture()
    {
        var session = BuildGoldenSession();
        var mapper = new SimSessionUploadRequestMapper();
        var request = mapper.Map(session, "3.0.0");

        // Top-level fields
        Assert.Equal("3.0.0",     request.TrackerVersion);
        Assert.Equal("career",    request.FlightMode);
        Assert.Null(request.BidId);
        Assert.Equal("KORD",      request.Departure);
        Assert.Equal("KBTV",      request.Arrival);
        Assert.Equal("C700",      request.Aircraft);
        Assert.Equal("regional",  request.AircraftCategory);
        Assert.Equal(1.967,       request.BlockTimeActual);
        Assert.Equal(2.0,         request.BlockTimeScheduled);

        // Landing scoring
        var landing = request.ScoringInput.Landing;
        Assert.Equal(-187, landing.TouchdownRateFpm);
        Assert.Equal(4.2,  landing.TouchdownPitchDeg);
        Assert.Equal(6.1,  landing.MaxPitchWhileWowDeg);
        Assert.Equal(0.8,  landing.TouchdownBankDeg);
        Assert.Equal(1.19, landing.TouchdownGForce);
        Assert.Equal(0,    landing.BounceCount);
        Assert.False(landing.GearUpAtTouchdown);

        // Safety
        var safety = request.ScoringInput.Safety;
        Assert.False(safety.CrashDetected);
        Assert.Equal(0, safety.OverspeedWarningCount);
        Assert.Equal(0, safety.StallWarningCount);
        Assert.Equal(0, safety.GpwsAlertCount);

        // Landing analysis
        var la = request.LandingAnalysis;
        Assert.Equal(44.4683,  la.TouchdownLat);
        Assert.Equal(-73.1532, la.TouchdownLon);
        Assert.Equal(168.4,    la.TouchdownHeadingDeg);
        Assert.Equal(335.0,    la.TouchdownAltFt);
        Assert.Equal(131.0,    la.TouchdownIAS);
        Assert.Equal(12.0,     la.WindSpeedAtTouchdownKnots);
        Assert.Equal(210.0,    la.WindDirectionAtTouchdownDegrees);
        Assert.Equal(5,        la.ApproachPath.Length);

        // Flight path
        Assert.Equal(8, request.FlightPath.Length);
        Assert.Equal(41.9742, request.FlightPath[0].Lat);
        Assert.Equal(0.0,     request.FlightPath[0].TMin);
    }

    // ── Touchdown rate FPM ────────────────────────────────────────────────────

    [Fact]
    public void TouchdownRateFpm_IsNegativeInPayload()
    {
        var session = BuildMinimalSession(touchdownVsFpm: 188);
        var request = new SimSessionUploadRequestMapper().Map(session, "1.0.0");
        Assert.True(request.ScoringInput.Landing.TouchdownRateFpm < 0,
            $"Expected negative touchdownRateFpm but got {request.ScoringInput.Landing.TouchdownRateFpm}");
        Assert.Equal(-188, request.ScoringInput.Landing.TouchdownRateFpm);
    }

    // ── Heading is magnetic ───────────────────────────────────────────────────

    [Fact]
    public void TouchdownHeadingDeg_IsMagneticInPayload()
    {
        // The tracker captures magnetic heading from the touchdown frame; the mapper
        // must forward it to touchdownHeadingDeg without substituting true heading.
        const double magneticHeading = 178.5;
        var session = BuildMinimalSession(touchdownHeadingMag: magneticHeading);
        var request = new SimSessionUploadRequestMapper().Map(session, "1.0.0");
        Assert.Equal(magneticHeading, request.LandingAnalysis.TouchdownHeadingDeg);
    }

    // ── Wind fields ───────────────────────────────────────────────────────────

    [Fact]
    public void LandingAnalysis_IncludesWindFields()
    {
        var session = BuildMinimalSession(windSpeed: 15.0, windDir: 270.0);
        var request = new SimSessionUploadRequestMapper().Map(session, "1.0.0");
        Assert.Equal(15.0,  request.LandingAnalysis.WindSpeedAtTouchdownKnots);
        Assert.Equal(270.0, request.LandingAnalysis.WindDirectionAtTouchdownDegrees);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static PendingCompletedSession BuildGoldenSession()
    {
        var blocksOff = new DateTimeOffset(2026, 4, 29, 22,  0, 0, TimeSpan.Zero);
        var wheelsOff = new DateTimeOffset(2026, 4, 29, 22, 14, 0, TimeSpan.Zero);
        var wheelsOn  = new DateTimeOffset(2026, 4, 29, 23, 51, 0, TimeSpan.Zero);
        var blocksOn  = new DateTimeOffset(2026, 4, 29, 23, 58, 0, TimeSpan.Zero);

        return new PendingCompletedSession
        {
            SessionId = "golden-1",
            SavedUtc  = blocksOn,
            State = new FlightSessionRuntimeState
            {
                Context = new FlightSessionContext
                {
                    DepartureAirportIcao = "KORD",
                    ArrivalAirportIcao   = "KBTV",
                    FlightMode           = "career",
                    AircraftType         = "C700",
                    AircraftCategory     = "regional",
                    ScheduledBlockHours  = 2.0,
                },
                CurrentPhase = FlightPhase.Arrival,
                BlockTimes = new FlightSessionBlockTimes
                {
                    BlocksOffUtc = blocksOff,
                    WheelsOffUtc = wheelsOff,
                    WheelsOnUtc  = wheelsOn,
                    BlocksOnUtc  = blocksOn,
                },
                ScoreInput = new FlightScoreInput
                {
                    Landing = new LandingMetrics
                    {
                        // Stored as positive magnitude; mapper negates → payload = -187
                        TouchdownVerticalSpeedFpm           = 187,
                        TouchdownPitchAngleDegrees          = 4.2,
                        MaxPitchWhileWowDegrees             = 6.1,
                        TouchdownBankAngleDegrees           = 0.8,
                        TouchdownGForce                     = 1.19,
                        BounceCount                         = 0,
                        GearUpAtTouchdown                   = false,
                    },
                    Safety = new SafetyMetrics
                    {
                        CrashDetected   = false,
                        OverspeedEvents = 0,
                        StallEvents     = 0,
                        GpwsEvents      = 0,
                    },
                    LandingAnalysis = new LandingAnalysisData
                    {
                        TouchdownLat               = 44.4683,
                        TouchdownLon               = -73.1532,
                        TouchdownHeadingMagneticDeg = 168.4,
                        TouchdownAltFt             = 335,
                        TouchdownIAS               = 131,
                        WindSpeedKnots             = 12.0,
                        WindDirectionDegrees       = 210.0,
                    },
                    ApproachPath = new[]
                    {
                        new ApproachPathPoint { Lat = 44.52, Lon = -73.21, AltFt = 3200, IasKts = 190, VsFpm = -820 },
                        new ApproachPathPoint { Lat = 44.50, Lon = -73.19, AltFt = 2400, IasKts = 175, VsFpm = -740 },
                        new ApproachPathPoint { Lat = 44.49, Lon = -73.18, AltFt = 1400, IasKts = 155, VsFpm = -680 },
                        new ApproachPathPoint { Lat = 44.48, Lon = -73.17, AltFt =  680, IasKts = 142, VsFpm = -620 },
                        new ApproachPathPoint { Lat = 44.47, Lon = -73.16, AltFt =  380, IasKts = 136, VsFpm = -540 },
                    },
                    FlightPath = new[]
                    {
                        new FlightPathPoint { Lat = 41.9742, Lon = -87.9073, AltFt =   672, TMin =   0.0 },
                        new FlightPathPoint { Lat = 42.31,   Lon = -87.52,   AltFt =  8400, TMin =  10.0 },
                        new FlightPathPoint { Lat = 42.89,   Lon = -86.74,   AltFt = 22000, TMin =  22.0 },
                        new FlightPathPoint { Lat = 43.45,   Lon = -82.10,   AltFt = 37000, TMin =  55.0 },
                        new FlightPathPoint { Lat = 43.98,   Lon = -76.30,   AltFt = 37000, TMin =  88.0 },
                        new FlightPathPoint { Lat = 44.31,   Lon = -74.85,   AltFt = 18000, TMin = 105.0 },
                        new FlightPathPoint { Lat = 44.47,   Lon = -73.62,   AltFt =  6200, TMin = 113.0 },
                        new FlightPathPoint { Lat = 44.47,   Lon = -73.15,   AltFt =   335, TMin = 118.0 },
                    },
                },
                ScoreResult = new ScoreResult(100, 91.4, "A", false, Array.Empty<PhaseScoreResult>(), Array.Empty<ScoreFinding>()),
            },
        };
    }

    private static PendingCompletedSession BuildMinimalSession(
        double touchdownVsFpm = 150,
        double? touchdownHeadingMag = null,
        double? windSpeed = null,
        double? windDir = null)
    {
        return new PendingCompletedSession
        {
            SessionId = "test-1",
            SavedUtc  = DateTimeOffset.UtcNow,
            State = new FlightSessionRuntimeState
            {
                Context = new FlightSessionContext
                {
                    FlightMode = "free_flight",
                },
                CurrentPhase = FlightPhase.Arrival,
                BlockTimes   = new FlightSessionBlockTimes(),
                ScoreInput   = new FlightScoreInput
                {
                    Landing = new LandingMetrics
                    {
                        TouchdownVerticalSpeedFpm = touchdownVsFpm,
                    },
                    LandingAnalysis = new LandingAnalysisData
                    {
                        TouchdownHeadingMagneticDeg = touchdownHeadingMag,
                        WindSpeedKnots              = windSpeed,
                        WindDirectionDegrees        = windDir,
                    },
                },
                ScoreResult = new ScoreResult(100, 0, "C", false, Array.Empty<PhaseScoreResult>(), Array.Empty<ScoreFinding>()),
            },
        };
    }
}
