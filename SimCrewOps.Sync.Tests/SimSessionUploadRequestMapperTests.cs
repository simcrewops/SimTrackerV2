using SimCrewOps.Persistence.Models;
using SimCrewOps.Runtime.Models;
using SimCrewOps.Scoring.Models;
using SimCrewOps.Sync.Sync;
using Xunit;

namespace SimCrewOps.Sync.Tests;

public sealed class SimSessionUploadRequestMapperTests
{
    private static readonly string TrackerVersion = "3.0.0";

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>Minimal valid session wired up for the subset being tested.</summary>
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

    // ── Session timing ───────────────────────────────────────────────────────

    [Fact]
    public void Map_MapsSessionTimingFields()
    {
        var t0 = new DateTimeOffset(2026, 4, 15, 8, 0, 0, TimeSpan.Zero);

        var state = BaseState(scoreInput: new FlightScoreInput
        {
            Session = new SessionMetrics
            {
                EnginesStartedAtUtc = t0.AddMinutes(-10),
                WheelsOffAtUtc      = t0.AddMinutes(15),
                WheelsOnAtUtc       = t0.AddMinutes(175),
                EnginesOffAtUtc     = t0.AddMinutes(195),
            },
        });

        var mapper = new SimSessionUploadRequestMapper();
        var request = mapper.Map(Session(state), TrackerVersion);

        Assert.Equal(t0.AddMinutes(-10), request.EnginesStartedAt);
        Assert.Equal(t0.AddMinutes(15),  request.WheelsOffAt);
        Assert.Equal(t0.AddMinutes(175), request.WheelsOnAt);
        Assert.Equal(t0.AddMinutes(195), request.EnginesOffAt);
    }

    // ── Fuel ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Map_MapsFuelFields()
    {
        var state = BaseState(scoreInput: new FlightScoreInput
        {
            Session = new SessionMetrics
            {
                FuelAtDepartureLbs = 42_500,
                FuelAtLandingLbs   = 31_200,
            },
        });

        var mapper = new SimSessionUploadRequestMapper();
        var request = mapper.Map(Session(state), TrackerVersion);

        Assert.Equal(42_500, request.FuelAtDepartureLbs);
        Assert.Equal(31_200, request.FuelAtLandingLbs);
        Assert.Equal(11_300, request.FuelBurnedLbs);   // 42500 − 31200
    }

    // ── GPS track ────────────────────────────────────────────────────────────

    [Fact]
    public void Map_MapsGpsTrackWhenNonEmpty()
    {
        var t0 = new DateTimeOffset(2026, 4, 15, 9, 0, 0, TimeSpan.Zero);

        var state = BaseState(scoreInput: new FlightScoreInput
        {
            GpsTrack = new[]
            {
                new GpsTrackPoint
                {
                    TimestampUtc     = t0,
                    Latitude         = 40.641,
                    Longitude        = -73.778,
                    AltitudeFeet     = 350,
                    GroundSpeedKnots = 28.5,
                    Phase            = FlightPhase.TaxiOut,
                },
                new GpsTrackPoint
                {
                    TimestampUtc     = t0.AddMinutes(30),
                    Latitude         = 38.852,
                    Longitude        = -77.037,
                    AltitudeFeet     = 35_000,
                    GroundSpeedKnots = 450,
                    Phase            = FlightPhase.Cruise,
                },
            },
        });

        var mapper = new SimSessionUploadRequestMapper();
        var request = mapper.Map(Session(state), TrackerVersion);

        Assert.NotNull(request.GpsTrack);
        Assert.Equal(2, request.GpsTrack!.Count);

        var first = request.GpsTrack[0];
        Assert.Equal(t0,      first.TimestampUtc);
        Assert.Equal(40.641,  first.Latitude);
        Assert.Equal(-73.778, first.Longitude);
        Assert.Equal(350,     first.AltitudeFeet);
        Assert.Equal(28.5,    first.GroundSpeedKnots);
        Assert.Equal("TaxiOut", first.Phase);

        var second = request.GpsTrack[1];
        Assert.Equal(35_000,  second.AltitudeFeet);
        Assert.Equal("Cruise", second.Phase);
    }

    [Fact]
    public void Map_ReturnsNullGpsTrackWhenEmpty()
    {
        var state = BaseState(scoreInput: new FlightScoreInput
        {
            GpsTrack = Array.Empty<GpsTrackPoint>(),
        });

        var mapper = new SimSessionUploadRequestMapper();
        var request = mapper.Map(Session(state), TrackerVersion);

        // An empty GPS track must be sent as null — not an empty JSON array.
        Assert.Null(request.GpsTrack);
    }

    // ── Block-time calculation ───────────────────────────────────────────────

    [Fact]
    public void Map_CalculatesBlockTimeActualFromBlocksOffAndBlocksOn()
    {
        var blocksOff = new DateTimeOffset(2026, 4, 15, 8, 0, 0, TimeSpan.Zero);
        var blocksOn  = blocksOff.AddHours(2.5);

        var state = BaseState(blockTimes: new FlightSessionBlockTimes
        {
            BlocksOffUtc = blocksOff,
            BlocksOnUtc  = blocksOn,
        });

        var mapper = new SimSessionUploadRequestMapper();
        var request = mapper.Map(Session(state), TrackerVersion);

        Assert.Equal(2.5, request.BlockTimeActual);
    }

    [Fact]
    public void Map_BlockTimeActualIsNullWhenBlocksOnMissing()
    {
        var state = BaseState(blockTimes: new FlightSessionBlockTimes
        {
            BlocksOffUtc = new DateTimeOffset(2026, 4, 15, 8, 0, 0, TimeSpan.Zero),
            // BlocksOnUtc intentionally absent (session ended via SessionEndTriggeredUtc)
            SessionEndTriggeredUtc = new DateTimeOffset(2026, 4, 15, 11, 0, 0, TimeSpan.Zero),
        });

        var mapper = new SimSessionUploadRequestMapper();
        var request = mapper.Map(Session(state), TrackerVersion);

        // Block time cannot be computed without BlocksOnUtc even if the session is otherwise complete.
        Assert.Null(request.BlockTimeActual);
    }

    // ── Tracker version ──────────────────────────────────────────────────────

    [Fact]
    public void Map_UsesSuppliedTrackerVersion()
    {
        var state = BaseState();
        var mapper = new SimSessionUploadRequestMapper();
        var request = mapper.Map(Session(state), "3.0.0");
        Assert.Equal("3.0.0", request.TrackerVersion);
    }

    // ── ScoreInputV5 (v3 structured phase metrics) ────────────────────────────

    [Fact]
    public void Map_ScoreInputV5_MapsPhaseMetricsFromEverySection()
    {
        var state = BaseState(scoreInput: new FlightScoreInput
        {
            TaxiOut  = new TaxiMetrics  { MaxGroundSpeedKnots = 28.5, TaxiLightsOn = true, MaxTurnSpeedKnots = 18.0, StrobeLightOnDuringTaxi = false },
            Takeoff  = new TakeoffMetrics { BounceCount = 1, MaxBankAngleDegrees = 4.2, PositiveRateBeforeGearUp = true },
            Climb    = new ClimbMetrics { MaxIasBelowFl100Knots = 268.0, HeavyFourEngineAircraft = true, LandingLightsOffAboveFL180 = true },
            Cruise   = new CruiseMetrics { MaxAltitudeDeviationFeet = 310, MaxBankAngleDegrees = 22.5, SpeedInstabilityEvents = 1 },
            Descent  = new DescentMetrics { MaxIasBelowFl100Knots = 275.0, LandingLightsOnBeforeFL180 = true },
            Approach = new ApproachMetrics { FlapsConfiguredBy1000Agl = true, IlsApproachDetected = true, MaxGlideslopeDeviationDots = 0.33 },
            Landing  = new LandingMetrics { BounceCount = 0, TouchdownVerticalSpeedFpm = 142 },
            TaxiIn   = new TaxiInMetrics { MaxGroundSpeedKnots = 18.0, SmoothDeceleration = true },
            Safety   = new SafetyMetrics { OverspeedEvents = 2, StallEvents = 1 },
            LightsSystems = new LightsSystemsMetrics { BeaconOnThroughoutFlight = true, StrobesCorrect = true, LandingLightsCompliance = 0.9 },
        });

        var mapper = new SimSessionUploadRequestMapper();
        var v5 = mapper.Map(Session(state), TrackerVersion).ScoreInputV5;

        Assert.NotNull(v5);

        // TaxiOut
        Assert.Equal(28.5, v5!.TaxiOut.MaxGroundSpeedKts);
        Assert.True(v5.TaxiOut.NavLightsOn);
        Assert.Equal(18.0, v5.TaxiOut.MaxTurnSpeedKts);

        // Takeoff
        Assert.True(v5.Takeoff.BounceOnRotation);
        Assert.Equal(4.2, v5.Takeoff.MaxBankBelow1000AglDeg);
        Assert.True(v5.Takeoff.PositiveRateBeforeGearUp);

        // Climb
        Assert.True(v5.Climb.IsHeavy);
        Assert.Equal(268.0, v5.Climb.MaxIasBelowFl100Kts);
        Assert.True(v5.Climb.LandingLightsOffAboveFL180);

        // Cruise
        Assert.Equal(310.0, v5.Cruise.MaxAltitudeDeviationFt);
        Assert.Equal(22.5,  v5.Cruise.MaxBankDeg);
        Assert.Equal(1,     v5.Cruise.SpeedInstabilityEvents);

        // Descent
        Assert.Equal(275.0, v5.Descent.MaxIasBelowFl100Kts);
        Assert.True(v5.Descent.LandingLightsOnBeforeFL180);

        // Approach
        Assert.True(v5.Approach.FlapsConfiguredBy1000Agl);
        Assert.True(v5.Approach.IlsDetected);
        Assert.Equal(0.33, v5.Approach.IlsMaxGlideslopeDevDots);

        // Landing (v3: touchdownRateFpm, bounceCount)
        Assert.Equal(0,   v5.Landing.BounceCount);
        Assert.Equal(142, v5.Landing.TouchdownRateFpm);

        // TaxiIn
        Assert.Equal(18.0, v5.TaxiIn.MaxGroundSpeedKts);
        Assert.True(v5.TaxiIn.SmoothDeceleration);

        // Safety
        Assert.Equal(2, v5.Safety.OverspeedEvents);
        Assert.Equal(1, v5.Safety.StallEvents);

        // LightsSystems
        Assert.True(v5.LightsSystems.BeaconOnThroughoutFlight);
        Assert.True(v5.LightsSystems.StrobesCorrect);
        Assert.Equal(0.9, v5.LightsSystems.LandingLightsCompliance);
    }

    // ── LandingAnalysis ──────────────────────────────────────────────────────

    [Fact]
    public void Map_LandingAnalysis_IsPopulatedFromApproachSampleBuffer()
    {
        var state = BaseState(scoreInput: new FlightScoreInput
        {
            ApproachPath = new[]
            {
                new ApproachSamplePoint { DistanceToThresholdNm = 8.2, AltitudeFeet = 2_600, IndicatedAirspeedKnots = 210, VerticalSpeedFpm = -1_400 },
                new ApproachSamplePoint { DistanceToThresholdNm = 4.1, AltitudeFeet = 1_300, IndicatedAirspeedKnots = 185, VerticalSpeedFpm = -900  },
                new ApproachSamplePoint { DistanceToThresholdNm = 0.5, AltitudeFeet = 150,   IndicatedAirspeedKnots = 145, VerticalSpeedFpm = -650  },
                new ApproachSamplePoint { DistanceToThresholdNm = 0.1, AltitudeFeet = 0,     IndicatedAirspeedKnots = 138, VerticalSpeedFpm = -180  },
            },
        });

        var mapper = new SimSessionUploadRequestMapper();
        var request = mapper.Map(Session(state), TrackerVersion);

        var analysis = request.LandingAnalysis;
        Assert.NotNull(analysis);

        var path = analysis!.ApproachPath;
        Assert.NotNull(path);
        Assert.Equal(4, path!.Count);

        Assert.Equal(8.2,    path[0].DistanceToThresholdNm);
        Assert.Equal(2_600,  path[0].AltitudeFt);
        Assert.Equal(210,    path[0].IasKts);
        Assert.Equal(-1_400, path[0].VsFpm);

        // Final (touchdown) sample.
        Assert.Equal(0.1,  path[3].DistanceToThresholdNm);
        Assert.Equal(0,    path[3].AltitudeFt);
        Assert.Equal(138,  path[3].IasKts);
        Assert.Equal(-180, path[3].VsFpm);
    }

    [Fact]
    public void Map_LandingAnalysis_IsNullWhenApproachPathIsEmpty()
    {
        var state = BaseState(scoreInput: new FlightScoreInput
        {
            ApproachPath = Array.Empty<ApproachSamplePoint>(),
        });

        var mapper = new SimSessionUploadRequestMapper();
        var request = mapper.Map(Session(state), TrackerVersion);

        Assert.Null(request.LandingAnalysis);
    }
}
