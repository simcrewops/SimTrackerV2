using SimCrewOps.Persistence.Models;
using SimCrewOps.Runtime.Models;
using SimCrewOps.Scoring.Models;
using SimCrewOps.Sync.Sync;
using Xunit;

namespace SimCrewOps.Sync.Tests;

public sealed class SimSessionUploadRequestMapperTests
{
    private static readonly string TrackerVersion = "1.2.3";

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

    // ── ILS approach quality ─────────────────────────────────────────────────

    [Fact]
    public void Map_MapsIlsApproachFields()
    {
        var state = BaseState(scoreInput: new FlightScoreInput
        {
            Approach = new ApproachMetrics
            {
                IlsApproachDetected         = true,
                MaxGlideslopeDeviationDots  = 0.45,
                AvgGlideslopeDeviationDots  = 0.18,
                MaxLocalizerDeviationDots   = 0.32,
                AvgLocalizerDeviationDots   = 0.11,
            },
        });

        var mapper = new SimSessionUploadRequestMapper();
        var request = mapper.Map(Session(state), TrackerVersion);

        Assert.True(request.IlsApproachDetected);
        Assert.Equal(0.45, request.IlsMaxGlideslopeDevDots);
        Assert.Equal(0.18, request.IlsAvgGlideslopeDevDots);
        Assert.Equal(0.32, request.IlsMaxLocalizerDevDots);
        Assert.Equal(0.11, request.IlsAvgLocalizerDevDots);
    }

    // ── Extended touchdown context ────────────────────────────────────────────

    [Fact]
    public void Map_MapsTouchdownContextFields()
    {
        var state = BaseState(scoreInput: new FlightScoreInput
        {
            Landing = new LandingMetrics
            {
                AutopilotEngagedAtTouchdown   = true,
                SpoilersDeployedAtTouchdown   = true,
                ReverseThrustUsed             = false,
                WindSpeedAtTouchdownKnots     = 12.5,
                WindDirectionAtTouchdownDegrees = 240,
                HeadwindComponentKnots        = 10.8,
                CrosswindComponentKnots       = -6.2,
                OatCelsiusAtTouchdown         = 8.3,
            },
        });

        var mapper = new SimSessionUploadRequestMapper();
        var request = mapper.Map(Session(state), TrackerVersion);

        Assert.True(request.TouchdownAutopilotEngaged);
        Assert.True(request.TouchdownSpoilersDeployed);
        Assert.False(request.TouchdownReverseThrustUsed);
        Assert.Equal(12.5,  request.TouchdownWindSpeedKts);
        Assert.Equal(240,   request.TouchdownWindDirectionDeg);
        Assert.Equal(10.8,  request.TouchdownHeadwindKts);
        Assert.Equal(-6.2,  request.TouchdownCrosswindKts);
        Assert.Equal(8.3,   request.TouchdownOatCelsius);
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

    // ── Score normalisation ──────────────────────────────────────────────────

    [Fact]
    public void Map_NormalisesScoreTo100ScaleWhenMaximumExceeds100()
    {
        // When runway-data scoring adds bonus points the raw maximum may exceed 100.
        // The mapper must normalise ScoreFinal to a 0–100 scale.
        var state = BaseState() with
        {
            ScoreResult = new ScoreResult(120, 108, "A", false, Array.Empty<PhaseScoreResult>(), Array.Empty<ScoreFinding>()),
        };

        var mapper = new SimSessionUploadRequestMapper();
        var request = mapper.Map(Session(state), TrackerVersion);

        // 108 / 120 * 100 = 90.0
        Assert.Equal(90.0, request.ScoreFinal);
    }

    [Fact]
    public void Map_NormalisesScoreTo100ScaleWhenMaximumIs100()
    {
        var state = BaseState() with
        {
            ScoreResult = new ScoreResult(100, 85, "B", false, Array.Empty<PhaseScoreResult>(), Array.Empty<ScoreFinding>()),
        };

        var mapper = new SimSessionUploadRequestMapper();
        var request = mapper.Map(Session(state), TrackerVersion);

        // 85 / 100 * 100 = 85.0
        Assert.Equal(85.0, request.ScoreFinal);
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

    // ── ScoreInputV5 (structured phase metrics) ──────────────────────────────

    [Fact]
    public void Map_ScoreInputV5_MapsPhaseMetricsFromEverySection()
    {
        // Populate a representative field from each phase section so we can verify
        // the mapper covers the full FlightScoreInput → FlightScoreInputV5Upload path.
        var state = BaseState(scoreInput: new FlightScoreInput
        {
            Preflight = new PreflightMetrics { BeaconOnBeforeTaxi = true },
            TaxiOut   = new TaxiMetrics     { MaxGroundSpeedKnots = 28.5, TaxiLightsOn = true },
            Takeoff   = new TakeoffMetrics  { BounceCount = 1, MaxBankAngleDegrees = 4.2 },
            Climb     = new ClimbMetrics    { MaxIasBelowFl100Knots = 268.0 },
            Cruise    = new CruiseMetrics   { MaxAltitudeDeviationFeet = 310, MaxBankAngleDegrees = 22.5 },
            Descent   = new DescentMetrics  { MaxIasBelowFl100Knots = 275.0, LandingLightsOnBy9900 = true },
            Approach  = new ApproachMetrics { GearDownBy1000Agl = true, IlsApproachDetected = true, MaxGlideslopeDeviationDots = 0.33 },
            Landing   = new LandingMetrics
            {
                BounceCount              = 0,
                TouchdownVerticalSpeedFpm = -142,
                TouchdownHeadingDegrees  = 182.0,
                TouchdownDistanceFromThresholdFt = 2_840,
                SpeedAtThresholdKnots    = 135.0,
                ThresholdCrossingHeightFt = 48.0,
            },
            TaxiIn   = new TaxiInMetrics   { MaxGroundSpeedKnots = 18.0 },
            Arrival  = new ArrivalMetrics  { AllEnginesOffByEndOfSession = true },
            Safety   = new SafetyMetrics   { OverspeedEvents = 2, StallEvents = 1 },
        });

        var mapper = new SimSessionUploadRequestMapper();
        var v5 = mapper.Map(Session(state), TrackerVersion).ScoreInputV5;

        Assert.NotNull(v5);

        Assert.True(v5!.Preflight.BeaconOnBeforeTaxi);
        Assert.Equal(28.5, v5.TaxiOut.MaxGroundSpeedKts);
        Assert.True(v5.TaxiOut.TaxiLightsOn);
        Assert.Equal(1,    v5.Takeoff.Bounces);
        Assert.Equal(4.2,  v5.Takeoff.MaxBankDeg);
        Assert.Equal(268.0, v5.Climb.MaxIasBelowFl100Kts);
        Assert.Equal(310.0, v5.Cruise.MaxAltitudeDeviationFt);
        Assert.Equal(22.5,  v5.Cruise.MaxBankDeg);
        Assert.Equal(275.0, v5.Descent.MaxIasBelowFl100Kts);
        Assert.True(v5.Descent.LandingLightsOnBy9900);
        Assert.True(v5.Approach.GearDownBy1000Agl);
        Assert.True(v5.Approach.IlsDetected);
        Assert.Equal(0.33,  v5.Approach.IlsMaxGlideslopeDevDots);
        Assert.Equal(0,     v5.Landing.Bounces);
        Assert.Equal(-142,  v5.Landing.TouchdownVsFpm);
        Assert.Equal(182.0, v5.Landing.TouchdownHeadingDeg);
        Assert.Equal(2_840, v5.Landing.DistanceFromThresholdFt);
        Assert.Equal(135.0, v5.Landing.SpeedAtThresholdKts);
        Assert.Equal(48.0,  v5.Landing.ThresholdCrossingHeightFt);
        Assert.Equal(18.0,  v5.TaxiIn.MaxGroundSpeedKts);
        Assert.True(v5.Arrival.AllEnginesOffByEndOfSession);
        Assert.Equal(2, v5.Safety.OverspeedEvents);
        Assert.Equal(1, v5.Safety.StallEvents);
    }

    // ── LandingAnalysis ──────────────────────────────────────────────────────

    [Fact]
    public void Map_LandingAnalysis_IsPopulatedWithApproachPathAndGeometryFields()
    {
        // GPS track has one Approach-phase point sandwiched between non-Approach points.
        // Only the Approach point should appear in LandingAnalysis.ApproachPath.
        var t0 = new DateTimeOffset(2026, 4, 15, 10, 0, 0, TimeSpan.Zero);

        var state = BaseState(scoreInput: new FlightScoreInput
        {
            Landing = new LandingMetrics
            {
                TouchdownHeadingDegrees          = 183.0,
                TouchdownDistanceFromThresholdFt = 2_500,
                SpeedAtThresholdKnots            = 138.0,
                ThresholdCrossingHeightFt        = 45.0,
            },
            GpsTrack = new[]
            {
                new GpsTrackPoint
                {
                    TimestampUtc     = t0.AddMinutes(-5),
                    Latitude         = 40.1,
                    Longitude        = -75.1,
                    AltitudeFeet     = 4_000,
                    GroundSpeedKnots = 220,
                    Phase            = FlightPhase.Descent,
                },
                new GpsTrackPoint
                {
                    TimestampUtc     = t0.AddMinutes(-2),
                    Latitude         = 40.05,
                    Longitude        = -75.05,
                    AltitudeFeet     = 1_200,
                    GroundSpeedKnots = 170,
                    Phase            = FlightPhase.Approach,   // <-- only this one
                },
                new GpsTrackPoint
                {
                    TimestampUtc     = t0,
                    Latitude         = 40.0,
                    Longitude        = -75.0,
                    AltitudeFeet     = 0,
                    GroundSpeedKnots = 130,
                    Phase            = FlightPhase.Landing,
                },
            },
        });

        var mapper = new SimSessionUploadRequestMapper();
        var request = mapper.Map(Session(state), TrackerVersion);

        var analysis = request.LandingAnalysis;
        Assert.NotNull(analysis);

        Assert.Equal(183.0,  analysis!.TouchdownHeadingDeg);
        Assert.Equal(2_500,  analysis.TouchdownDistanceFromThresholdFt);
        Assert.Equal(138.0,  analysis.SpeedAtThreshold);
        Assert.Equal(45.0,   analysis.ThresholdCrossingHeightFt);

        // Approach path must contain only the Approach-phase GPS point.
        Assert.NotNull(analysis.ApproachPath);
        Assert.Single(analysis.ApproachPath!);
        Assert.Equal("Approach", analysis.ApproachPath[0].Phase);
        Assert.Equal(40.05,   analysis.ApproachPath[0].Latitude);
        Assert.Equal(-75.05,  analysis.ApproachPath[0].Longitude);
    }

    [Fact]
    public void Map_LandingAnalysis_IsNullWhenNoLandingDataPresent()
    {
        // When TouchdownDistanceFromThresholdFt == 0, TouchdownHeadingDegrees == 0,
        // and no runway was resolved, the LandingAnalysis object must be omitted entirely.
        var state = BaseState(scoreInput: new FlightScoreInput
        {
            Landing = new LandingMetrics
            {
                TouchdownDistanceFromThresholdFt = 0,
                TouchdownHeadingDegrees          = 0,
                // All other fields default to 0 / false
            },
        });
        // No LandingRunwayResolution set on state (HasResolvedLandingRunway == false).

        var mapper = new SimSessionUploadRequestMapper();
        var request = mapper.Map(Session(state), TrackerVersion);

        Assert.Null(request.LandingAnalysis);
    }

    [Fact]
    public void Map_LandingAnalysis_ApproachPathIsNullWhenNoApproachGpsPoints()
    {
        // LandingAnalysis is still emitted when touchdown data is present, but
        // ApproachPath is null when the GPS track has no Approach-phase points.
        var state = BaseState(scoreInput: new FlightScoreInput
        {
            Landing = new LandingMetrics
            {
                TouchdownDistanceFromThresholdFt = 1_800,
                TouchdownHeadingDegrees          = 270,
            },
            GpsTrack = new[]
            {
                new GpsTrackPoint { Phase = FlightPhase.Cruise,  AltitudeFeet = 35_000 },
                new GpsTrackPoint { Phase = FlightPhase.Landing, AltitudeFeet = 0 },
                // No Approach point
            },
        });

        var mapper = new SimSessionUploadRequestMapper();
        var request = mapper.Map(Session(state), TrackerVersion);

        Assert.NotNull(request.LandingAnalysis);
        Assert.Null(request.LandingAnalysis!.ApproachPath);
    }
}
