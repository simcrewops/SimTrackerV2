using SimCrewOps.PhaseEngine.Models;
using SimCrewOps.Runways.Models;
using SimCrewOps.Runways.Providers;
using SimCrewOps.Runways.Services;
using SimCrewOps.Runtime.Models;
using SimCrewOps.Runtime.Runtime;
using SimCrewOps.Scoring.Models;
using SimCrewOps.Tracking.Models;
using Xunit;

namespace SimCrewOps.Runtime.Tests;

public sealed class RuntimeCoordinatorTests
{
    [Fact]
    public async Task RuntimeCoordinator_CapturesBlockTimesAndInjectsTouchdownZoneExcess()
    {
        var runway = new RunwayEnd
        {
            AirportIcao = "KTEST",
            RunwayIdentifier = "18",
            TrueHeadingDegrees = 180,
            LengthFeet = 10_000,
            ThresholdLatitude = 40.0,
            ThresholdLongitude = -75.0,
            DataSource = RunwayDataSource.OurAirportsFallback,
        };

        var provider = new StubRunwayDataProvider(new AirportRunwayCatalog
        {
            AirportIcao = "KTEST",
            DataSource = RunwayDataSource.OurAirportsFallback,
            Runways = new[] { runway },
        });

        var coordinator = new RuntimeCoordinator(
            new FlightSessionContext { DepartureAirportIcao = "KDEP", ArrivalAirportIcao = "KTEST" },
            new RunwayResolver(provider));

        var t0 = new DateTimeOffset(2026, 4, 13, 12, 0, 0, TimeSpan.Zero);

        await coordinator.ProcessFrameAsync(Frame(t0, onGround: true, parkingBrake: true));
        var blocksOff = await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(1), onGround: true, parkingBrake: false, groundSpeed: 2));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(30), onGround: true, indicatedAirspeed: 55));
        var wheelsOff = await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(31), onGround: false, altitudeAgl: 20, indicatedAirspeed: 90, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(40), onGround: false, altitudeAgl: 500, verticalSpeed: 1500, indicatedAirspeed: 160, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(100), onGround: false, altitudeAgl: 35_000, verticalSpeed: 0, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(131), onGround: false, altitudeAgl: 35_000, verticalSpeed: 0, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(200), onGround: false, altitudeAgl: 35_000, verticalSpeed: -600, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(231), onGround: false, altitudeAgl: 35_000, verticalSpeed: -600, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(290), onGround: false, altitudeAgl: 2_800, gearDown: true, verticalSpeed: -500, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(300), onGround: false, altitudeAgl: 100, heading: 180));

        var touchdownPoint = Offset(runway.ThresholdLatitude, runway.ThresholdLongitude, runway.TrueHeadingDegrees, 3_250);
        var wheelsOn = await coordinator.ProcessFrameAsync(Frame(
            t0.AddSeconds(310),
            onGround: true,
            latitude: touchdownPoint.Latitude,
            longitude: touchdownPoint.Longitude,
            altitudeAgl: 0,
            groundSpeed: 100,
            heading: 178));

        Assert.Equal(BlockEventType.BlocksOff, blocksOff.PhaseFrame.BlockEvent!.Type);
        Assert.Equal(BlockEventType.WheelsOff, wheelsOff.PhaseFrame.BlockEvent!.Type);
        Assert.Equal(BlockEventType.WheelsOn, wheelsOn.PhaseFrame.BlockEvent!.Type);
        Assert.NotNull(wheelsOn.RunwayResolution);
        Assert.Equal("18", wheelsOn.RunwayResolution!.Runway.RunwayIdentifier);
        Assert.InRange(wheelsOn.EnrichedTelemetryFrame.TouchdownZoneExcessDistanceFeet ?? -1, 200, 300);
        Assert.InRange(wheelsOn.State.ScoreInput.Landing.TouchdownZoneExcessDistanceFeet, 200, 300);
        Assert.NotNull(wheelsOn.State.BlockTimes.BlocksOffUtc);
        Assert.NotNull(wheelsOn.State.BlockTimes.WheelsOffUtc);
        Assert.NotNull(wheelsOn.State.BlockTimes.WheelsOnUtc);
        Assert.Equal(FlightPhase.Landing, wheelsOn.State.CurrentPhase);
    }

    [Fact]
    public async Task RuntimeCoordinator_SkipsRunwayResolutionWhenArrivalAirportIsMissing()
    {
        var coordinator = new RuntimeCoordinator(
            new FlightSessionContext(),
            new RunwayResolver(new StubRunwayDataProvider(null)));

        var t0 = new DateTimeOffset(2026, 4, 13, 13, 0, 0, TimeSpan.Zero);

        await coordinator.ProcessFrameAsync(Frame(t0, onGround: true, parkingBrake: true));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(1), onGround: true, parkingBrake: false, groundSpeed: 2));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(30), onGround: true, indicatedAirspeed: 55));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(31), onGround: false, altitudeAgl: 20, indicatedAirspeed: 90, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(40), onGround: false, altitudeAgl: 500, verticalSpeed: 1500, indicatedAirspeed: 160, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(100), onGround: false, altitudeAgl: 35_000, verticalSpeed: 0, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(131), onGround: false, altitudeAgl: 35_000, verticalSpeed: 0, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(200), onGround: false, altitudeAgl: 35_000, verticalSpeed: -600, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(231), onGround: false, altitudeAgl: 35_000, verticalSpeed: -600, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(290), onGround: false, altitudeAgl: 2_800, gearDown: true, verticalSpeed: -500, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(300), onGround: false, altitudeAgl: 100, heading: 180));

        var touchdown = await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(310), onGround: true, altitudeAgl: 0, groundSpeed: 100, heading: 180));

        Assert.Null(touchdown.RunwayResolution);
        Assert.Null(touchdown.EnrichedTelemetryFrame.TouchdownZoneExcessDistanceFeet);
        Assert.Equal(0, touchdown.State.ScoreInput.Landing.TouchdownZoneExcessDistanceFeet);
    }

    [Fact]
    public async Task RuntimeCoordinator_Restore_ContinuesSavedSessionWithoutRestarting()
    {
        var coordinator = new RuntimeCoordinator(
            new FlightSessionContext(),
            new RunwayResolver(new StubRunwayDataProvider(null)));

        var t0 = new DateTimeOffset(2026, 4, 13, 14, 0, 0, TimeSpan.Zero);
        coordinator.Restore(new FlightSessionRuntimeState
        {
            Context = new FlightSessionContext
            {
                DepartureAirportIcao = "KDEP",
                ArrivalAirportIcao = "KARR",
                Profile = new FlightSessionProfile
                {
                    HeavyFourEngineAircraft = true,
                    EngineCount = 4,
                },
            },
            CurrentPhase = FlightPhase.TaxiIn,
            BlockTimes = new FlightSessionBlockTimes
            {
                BlocksOffUtc = t0.AddHours(-2),
                WheelsOffUtc = t0.AddHours(-1.9),
                WheelsOnUtc = t0.AddMinutes(-5),
            },
            LastTelemetryFrame = Frame(
                t0,
                onGround: true,
                groundSpeed: 18,
                heading: 180) with
            {
                Phase = FlightPhase.TaxiIn,
                TaxiLightsOn = true,
                Engine1Running = true,
                Engine2Running = true,
                Engine3Running = true,
                Engine4Running = true,
            },
            ScoreInput = new FlightScoreInput
            {
                Preflight = new PreflightMetrics
                {
                    BeaconOnBeforeTaxi = true,
                },
                Climb = new ClimbMetrics
                {
                    HeavyFourEngineAircraft = true,
                },
            },
            ScoreResult = new ScoreResult(100, 92, "A", false, Array.Empty<PhaseScoreResult>(), Array.Empty<ScoreFinding>()),
        });

        var arrival = await coordinator.ProcessFrameAsync(
            Frame(
                t0.AddSeconds(1),
                onGround: true,
                parkingBrake: true,
                groundSpeed: 0,
                heading: 180) with
            {
                TaxiLightsOn = false,
                Engine1Running = true,
                Engine2Running = true,
                Engine3Running = true,
                Engine4Running = true,
            });

        Assert.Equal(FlightPhase.Arrival, arrival.State.CurrentPhase);
        Assert.NotNull(arrival.State.BlockTimes.BlocksOffUtc);
        Assert.NotNull(arrival.State.BlockTimes.WheelsOffUtc);
        Assert.NotNull(arrival.State.BlockTimes.WheelsOnUtc);
        Assert.NotNull(arrival.State.BlockTimes.BlocksOnUtc);
        Assert.True(arrival.State.ScoreInput.Preflight.BeaconOnBeforeTaxi);
        Assert.True(arrival.State.ScoreInput.Climb.HeavyFourEngineAircraft);
        Assert.Equal("KARR", arrival.State.Context.ArrivalAirportIcao);
    }

    // ── Session-end condition ───────────────────────────────────────────────

    [Fact]
    public async Task RuntimeCoordinator_SessionEndCondition_DoesNotFireBeforeWheelsOn()
    {
        // Even when all post-shutdown conditions are met (engines off, beacon off,
        // parking brake set), the session-end trigger must NOT fire unless WheelsOn
        // has already been recorded — otherwise a pre-departure shutdown fires it.
        var coordinator = new RuntimeCoordinator(
            new FlightSessionContext(),
            new RunwayResolver(new StubRunwayDataProvider(null)));

        var t0 = new DateTimeOffset(2026, 4, 15, 10, 0, 0, TimeSpan.Zero);

        // Restore with no WheelsOnUtc (pre-flight / aborted pushback scenario).
        coordinator.Restore(new FlightSessionRuntimeState
        {
            Context = new FlightSessionContext(),
            CurrentPhase = FlightPhase.Preflight,
            BlockTimes = new FlightSessionBlockTimes(),   // no WheelsOnUtc
            ScoreInput = new FlightScoreInput(),
            ScoreResult = new ScoreResult(100, 92, "A", false, Array.Empty<PhaseScoreResult>(), Array.Empty<ScoreFinding>()),
        });

        // All post-shutdown conditions met — but no WheelsOn recorded yet.
        var result = await coordinator.ProcessFrameAsync(
            Frame(t0, onGround: true, parkingBrake: true, engine1Running: false));

        Assert.Null(result.State.BlockTimes.SessionEndTriggeredUtc);
        Assert.False(result.State.IsComplete);
    }

    [Fact]
    public async Task RuntimeCoordinator_SessionEndCondition_FiresAfterLandingWhenAllConditionsMet()
    {
        var coordinator = new RuntimeCoordinator(
            new FlightSessionContext(),
            new RunwayResolver(new StubRunwayDataProvider(null)));

        var t0 = new DateTimeOffset(2026, 4, 15, 11, 0, 0, TimeSpan.Zero);

        // Restore into post-landing TaxiIn state (WheelsOnUtc already recorded).
        coordinator.Restore(new FlightSessionRuntimeState
        {
            Context = new FlightSessionContext(),
            CurrentPhase = FlightPhase.TaxiIn,
            BlockTimes = new FlightSessionBlockTimes
            {
                BlocksOffUtc = t0.AddHours(-2),
                WheelsOffUtc = t0.AddHours(-1.9),
                WheelsOnUtc  = t0.AddMinutes(-5),
            },
            LastTelemetryFrame = Frame(t0.AddSeconds(-1), onGround: true, heading: 90),
            ScoreInput = new FlightScoreInput(),
            ScoreResult = new ScoreResult(100, 92, "A", false, Array.Empty<PhaseScoreResult>(), Array.Empty<ScoreFinding>()),
        });

        // Send the post-shutdown frame: engines off + beacon off (default) + parking brake set.
        var sessionEndTime = t0.AddSeconds(30);
        var result = await coordinator.ProcessFrameAsync(
            Frame(sessionEndTime, onGround: true, parkingBrake: true, engine1Running: false));

        Assert.Equal(sessionEndTime, result.State.BlockTimes.SessionEndTriggeredUtc);
        Assert.True(result.State.IsComplete);
    }

    [Fact]
    public async Task RuntimeCoordinator_SessionEndCondition_DoesNotFireWhenAnEngineIsStillRunning()
    {
        var coordinator = new RuntimeCoordinator(
            new FlightSessionContext(),
            new RunwayResolver(new StubRunwayDataProvider(null)));

        var t0 = new DateTimeOffset(2026, 4, 15, 12, 0, 0, TimeSpan.Zero);

        coordinator.Restore(new FlightSessionRuntimeState
        {
            Context = new FlightSessionContext(),
            CurrentPhase = FlightPhase.TaxiIn,
            BlockTimes = new FlightSessionBlockTimes
            {
                BlocksOffUtc = t0.AddHours(-2),
                WheelsOffUtc = t0.AddHours(-1.9),
                WheelsOnUtc  = t0.AddMinutes(-5),
            },
            LastTelemetryFrame = Frame(t0.AddSeconds(-1), onGround: true, heading: 90),
            ScoreInput = new FlightScoreInput(),
            ScoreResult = new ScoreResult(100, 92, "A", false, Array.Empty<PhaseScoreResult>(), Array.Empty<ScoreFinding>()),
        });

        // Engine 1 is still running — condition not met.
        var result = await coordinator.ProcessFrameAsync(
            Frame(t0, onGround: true, parkingBrake: true, engine1Running: true));

        Assert.Null(result.State.BlockTimes.SessionEndTriggeredUtc);
        Assert.False(result.State.IsComplete);
    }

    [Fact]
    public async Task RuntimeCoordinator_SessionEndCondition_DoesNotFireWhenBeaconIsOn()
    {
        var coordinator = new RuntimeCoordinator(
            new FlightSessionContext(),
            new RunwayResolver(new StubRunwayDataProvider(null)));

        var t0 = new DateTimeOffset(2026, 4, 15, 13, 0, 0, TimeSpan.Zero);

        coordinator.Restore(new FlightSessionRuntimeState
        {
            Context = new FlightSessionContext(),
            CurrentPhase = FlightPhase.TaxiIn,
            BlockTimes = new FlightSessionBlockTimes
            {
                BlocksOffUtc = t0.AddHours(-2),
                WheelsOffUtc = t0.AddHours(-1.9),
                WheelsOnUtc  = t0.AddMinutes(-5),
            },
            LastTelemetryFrame = Frame(t0.AddSeconds(-1), onGround: true, heading: 90),
            ScoreInput = new FlightScoreInput(),
            ScoreResult = new ScoreResult(100, 92, "A", false, Array.Empty<PhaseScoreResult>(), Array.Empty<ScoreFinding>()),
        });

        // All engines off, parking brake set — but beacon is still on.
        var result = await coordinator.ProcessFrameAsync(
            Frame(t0, onGround: true, parkingBrake: true, engine1Running: false) with
            {
                BeaconLightOn = true,
            });

        Assert.Null(result.State.BlockTimes.SessionEndTriggeredUtc);
        Assert.False(result.State.IsComplete);
    }

    [Fact]
    public async Task RuntimeCoordinator_SessionEndCondition_DoesNotFireWhenParkingBrakeNotSet()
    {
        var coordinator = new RuntimeCoordinator(
            new FlightSessionContext(),
            new RunwayResolver(new StubRunwayDataProvider(null)));

        var t0 = new DateTimeOffset(2026, 4, 15, 14, 0, 0, TimeSpan.Zero);

        coordinator.Restore(new FlightSessionRuntimeState
        {
            Context = new FlightSessionContext(),
            CurrentPhase = FlightPhase.TaxiIn,
            BlockTimes = new FlightSessionBlockTimes
            {
                BlocksOffUtc = t0.AddHours(-2),
                WheelsOffUtc = t0.AddHours(-1.9),
                WheelsOnUtc  = t0.AddMinutes(-5),
            },
            LastTelemetryFrame = Frame(t0.AddSeconds(-1), onGround: true, heading: 90),
            ScoreInput = new FlightScoreInput(),
            ScoreResult = new ScoreResult(100, 92, "A", false, Array.Empty<PhaseScoreResult>(), Array.Empty<ScoreFinding>()),
        });

        // All engines off, beacon off (default) — but parking brake NOT set.
        var result = await coordinator.ProcessFrameAsync(
            Frame(t0, onGround: true, parkingBrake: false, engine1Running: false));

        Assert.Null(result.State.BlockTimes.SessionEndTriggeredUtc);
        Assert.False(result.State.IsComplete);
    }

    [Fact]
    public async Task RuntimeCoordinator_SessionEndCondition_FiresOnlyOnce()
    {
        var coordinator = new RuntimeCoordinator(
            new FlightSessionContext(),
            new RunwayResolver(new StubRunwayDataProvider(null)));

        var t0 = new DateTimeOffset(2026, 4, 15, 15, 0, 0, TimeSpan.Zero);

        coordinator.Restore(new FlightSessionRuntimeState
        {
            Context = new FlightSessionContext(),
            CurrentPhase = FlightPhase.TaxiIn,
            BlockTimes = new FlightSessionBlockTimes
            {
                BlocksOffUtc = t0.AddHours(-2),
                WheelsOffUtc = t0.AddHours(-1.9),
                WheelsOnUtc  = t0.AddMinutes(-5),
            },
            LastTelemetryFrame = Frame(t0.AddSeconds(-1), onGround: true, heading: 90),
            ScoreInput = new FlightScoreInput(),
            ScoreResult = new ScoreResult(100, 92, "A", false, Array.Empty<PhaseScoreResult>(), Array.Empty<ScoreFinding>()),
        });

        var firstSessionEndTime = t0.AddSeconds(10);

        // First qualifying frame — fires the trigger.
        await coordinator.ProcessFrameAsync(
            Frame(firstSessionEndTime, onGround: true, parkingBrake: true, engine1Running: false));

        // Second qualifying frame — trigger already fired; timestamp must remain from the first.
        var secondResult = await coordinator.ProcessFrameAsync(
            Frame(t0.AddSeconds(20), onGround: true, parkingBrake: true, engine1Running: false));

        Assert.Equal(firstSessionEndTime, secondResult.State.BlockTimes.SessionEndTriggeredUtc);
    }

    // ── Wind component decomposition ────────────────────────────────────────

    [Fact]
    public async Task RuntimeCoordinator_TouchdownWindComponents_HeadwindCorrect()
    {
        // Runway 18 (heading 180°).  Wind from 180° is a pure headwind.
        // headwind = speed × cos(0°) = speed;  crosswind = speed × sin(0°) = 0
        var runway = new RunwayEnd
        {
            AirportIcao = "KWIND",
            RunwayIdentifier = "18",
            TrueHeadingDegrees = 180,
            LengthFeet = 10_000,
            ThresholdLatitude = 40.0,
            ThresholdLongitude = -75.0,
            DataSource = RunwayDataSource.OurAirportsFallback,
        };

        var provider = new StubRunwayDataProvider(new AirportRunwayCatalog
        {
            AirportIcao = "KWIND",
            DataSource = RunwayDataSource.OurAirportsFallback,
            Runways = new[] { runway },
        });

        var coordinator = new RuntimeCoordinator(
            new FlightSessionContext { DepartureAirportIcao = "KDEP", ArrivalAirportIcao = "KWIND" },
            new RunwayResolver(provider));

        var t0 = new DateTimeOffset(2026, 4, 15, 16, 0, 0, TimeSpan.Zero);

        await coordinator.ProcessFrameAsync(Frame(t0, onGround: true, parkingBrake: true));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(1), onGround: true, parkingBrake: false, groundSpeed: 2));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(30), onGround: true, indicatedAirspeed: 55));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(31), onGround: false, altitudeAgl: 20, indicatedAirspeed: 90, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(40), onGround: false, altitudeAgl: 500, verticalSpeed: 1500, indicatedAirspeed: 160, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(100), onGround: false, altitudeAgl: 35_000, verticalSpeed: 0, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(131), onGround: false, altitudeAgl: 35_000, verticalSpeed: 0, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(200), onGround: false, altitudeAgl: 35_000, verticalSpeed: -600, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(231), onGround: false, altitudeAgl: 35_000, verticalSpeed: -600, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(290), onGround: false, altitudeAgl: 2_800, gearDown: true, verticalSpeed: -500, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(300), onGround: false, altitudeAgl: 100, heading: 180));

        var touchdownPoint = Offset(runway.ThresholdLatitude, runway.ThresholdLongitude, runway.TrueHeadingDegrees, 3_250);
        var wheelsOn = await coordinator.ProcessFrameAsync(
            Frame(t0.AddSeconds(310),
                  onGround: true,
                  latitude: touchdownPoint.Latitude,
                  longitude: touchdownPoint.Longitude,
                  altitudeAgl: 0,
                  groundSpeed: 100,
                  heading: 180) with
            {
                WindSpeedKnots = 20.0,
                WindDirectionDegrees = 180.0,   // wind FROM south → direct headwind on RWY 18
            });

        var landing = wheelsOn.State.ScoreInput.Landing;

        // headwind ≈ 20 kts, crosswind ≈ 0 kts (within floating-point tolerance)
        Assert.InRange(landing.HeadwindComponentKnots,  19.9,  20.1);
        Assert.InRange(landing.CrosswindComponentKnots, -0.01,  0.01);
    }

    [Fact]
    public async Task RuntimeCoordinator_TouchdownWindComponents_CrosswindCorrect()
    {
        // Runway 18 (heading 180°).  Wind from 270° (west) = pure right crosswind.
        // relAngle = 270 − 180 = 90°
        // headwind  = speed × cos(90°) = 0;  crosswind = speed × sin(90°) = speed
        var runway = new RunwayEnd
        {
            AirportIcao = "KXWIND",
            RunwayIdentifier = "18",
            TrueHeadingDegrees = 180,
            LengthFeet = 10_000,
            ThresholdLatitude = 40.0,
            ThresholdLongitude = -75.0,
            DataSource = RunwayDataSource.OurAirportsFallback,
        };

        var provider = new StubRunwayDataProvider(new AirportRunwayCatalog
        {
            AirportIcao = "KXWIND",
            DataSource = RunwayDataSource.OurAirportsFallback,
            Runways = new[] { runway },
        });

        var coordinator = new RuntimeCoordinator(
            new FlightSessionContext { DepartureAirportIcao = "KDEP", ArrivalAirportIcao = "KXWIND" },
            new RunwayResolver(provider));

        var t0 = new DateTimeOffset(2026, 4, 15, 17, 0, 0, TimeSpan.Zero);

        await coordinator.ProcessFrameAsync(Frame(t0, onGround: true, parkingBrake: true));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(1), onGround: true, parkingBrake: false, groundSpeed: 2));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(30), onGround: true, indicatedAirspeed: 55));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(31), onGround: false, altitudeAgl: 20, indicatedAirspeed: 90, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(40), onGround: false, altitudeAgl: 500, verticalSpeed: 1500, indicatedAirspeed: 160, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(100), onGround: false, altitudeAgl: 35_000, verticalSpeed: 0, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(131), onGround: false, altitudeAgl: 35_000, verticalSpeed: 0, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(200), onGround: false, altitudeAgl: 35_000, verticalSpeed: -600, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(231), onGround: false, altitudeAgl: 35_000, verticalSpeed: -600, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(290), onGround: false, altitudeAgl: 2_800, gearDown: true, verticalSpeed: -500, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(300), onGround: false, altitudeAgl: 100, heading: 180));

        var touchdownPoint = Offset(runway.ThresholdLatitude, runway.ThresholdLongitude, runway.TrueHeadingDegrees, 3_250);
        var wheelsOn = await coordinator.ProcessFrameAsync(
            Frame(t0.AddSeconds(310),
                  onGround: true,
                  latitude: touchdownPoint.Latitude,
                  longitude: touchdownPoint.Longitude,
                  altitudeAgl: 0,
                  groundSpeed: 100,
                  heading: 180) with
            {
                WindSpeedKnots = 15.0,
                WindDirectionDegrees = 270.0,   // wind FROM west → pure right crosswind on RWY 18
            });

        var landing = wheelsOn.State.ScoreInput.Landing;

        // headwind ≈ 0 kts, crosswind ≈ +15 kts (from right — positive)
        Assert.InRange(landing.HeadwindComponentKnots,  -0.01,  0.01);
        Assert.InRange(landing.CrosswindComponentKnots,  14.9,  15.1);
    }

    // ── Approach path recording ─────────────────────────────────────────────

    [Fact]
    public async Task RuntimeCoordinator_ApproachPath_RecordsSamplesFromApproachPhase()
    {
        // Verifies that:
        //  • The airport reference coords are resolved on Approach entry.
        //  • Samples are time-gated (≥ 2 s apart) during Approach.
        //  • A final sample is recorded at the Landing transition.
        //  • All sample fields (distNm, altFt, iasKts, vsFpm) are populated.
        var runway = new RunwayEnd
        {
            AirportIcao = "KAPTH",
            RunwayIdentifier = "18",
            TrueHeadingDegrees = 180,
            LengthFeet = 10_000,
            ThresholdLatitude = 40.0,
            ThresholdLongitude = -75.0,
            DataSource = RunwayDataSource.OurAirportsFallback,
        };

        var provider = new StubRunwayDataProvider(new AirportRunwayCatalog
        {
            AirportIcao = "KAPTH",
            DataSource = RunwayDataSource.OurAirportsFallback,
            Runways = new[] { runway },
        });

        var coordinator = new RuntimeCoordinator(
            new FlightSessionContext { DepartureAirportIcao = "KDEP", ArrivalAirportIcao = "KAPTH" },
            new RunwayResolver(provider));

        var t0 = new DateTimeOffset(2026, 4, 15, 18, 0, 0, TimeSpan.Zero);

        // Standard pre-flight and climb sequence.
        await coordinator.ProcessFrameAsync(Frame(t0, onGround: true, parkingBrake: true));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(1), onGround: true, parkingBrake: false, groundSpeed: 2));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(30), onGround: true, indicatedAirspeed: 55));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(31), onGround: false, altitudeAgl: 20, indicatedAirspeed: 90, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(40), onGround: false, altitudeAgl: 500, verticalSpeed: 1500, indicatedAirspeed: 160, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(100), onGround: false, altitudeAgl: 35_000, verticalSpeed: 0, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(131), onGround: false, altitudeAgl: 35_000, verticalSpeed: 0, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(200), onGround: false, altitudeAgl: 35_000, verticalSpeed: -600, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(231), onGround: false, altitudeAgl: 35_000, verticalSpeed: -600, heading: 180));

        // Enter Approach from ~5 nm north of the threshold (well within the 15 nm trigger).
        // Offset to ~5 nm north so the haversine distance lands in range.
        const double FiveNmInDegLat = 5.0 / 60.0; // ~0.0833 degrees per nm
        var approachLat = runway.ThresholdLatitude + FiveNmInDegLat;
        await coordinator.ProcessFrameAsync(Frame(
            t0.AddSeconds(290),
            onGround: false,
            altitudeAgl: 2_800,
            gearDown: true,
            verticalSpeed: -700,
            indicatedAirspeed: 180,
            heading: 180,
            latitude: approachLat,
            longitude: -75.0));

        // Second Approach frame — 4 s later (≥ 2 s → new sample recorded).
        await coordinator.ProcessFrameAsync(Frame(
            t0.AddSeconds(294),
            onGround: false,
            altitudeAgl: 2_500,
            gearDown: true,
            verticalSpeed: -700,
            indicatedAirspeed: 175,
            heading: 180,
            latitude: approachLat - 0.01,
            longitude: -75.0));

        // Third Approach frame — only 1 s later (< 2 s → throttled, no new sample).
        await coordinator.ProcessFrameAsync(Frame(
            t0.AddSeconds(295),
            onGround: false,
            altitudeAgl: 2_450,
            gearDown: true,
            verticalSpeed: -700,
            indicatedAirspeed: 174,
            heading: 180,
            latitude: approachLat - 0.011,
            longitude: -75.0));

        // Landing transition — one final sample regardless of time guard.
        var touchdownPoint = Offset(runway.ThresholdLatitude, runway.ThresholdLongitude, runway.TrueHeadingDegrees, 2_000);
        var wheelsOn = await coordinator.ProcessFrameAsync(Frame(
            t0.AddSeconds(310),
            onGround: true,
            latitude: touchdownPoint.Latitude,
            longitude: touchdownPoint.Longitude,
            altitudeAgl: 0,
            groundSpeed: 120,
            indicatedAirspeed: 138,
            verticalSpeed: -180,
            heading: 180));

        var approachPath = wheelsOn.State.ScoreInput.ApproachPath;

        // Approach entry (t0+290) → sample 1; second frame (t0+294, ≥2s) → sample 2;
        // third frame (t0+295, <2s) → throttled; Landing entry (t0+310) → final sample.
        Assert.Equal(3, approachPath.Count);

        // First sample: aircraft at approachLat, ~5 nm north of threshold.
        Assert.InRange(approachPath[0].DistanceToThresholdNm, 4.0, 6.0);
        Assert.Equal(2_800, approachPath[0].AltitudeFeet);
        Assert.Equal(180,   approachPath[0].IndicatedAirspeedKnots);
        Assert.Equal(-700,  approachPath[0].VerticalSpeedFpm);

        // Final (landing) sample: aircraft at touchdownPoint, AGL=0, IAS=138.
        var last = approachPath[^1];
        Assert.Equal(0,    last.AltitudeFeet);
        Assert.Equal(138,  last.IndicatedAirspeedKnots);
        Assert.Equal(-180, last.VerticalSpeedFpm);
        // Distance to threshold at touchdown is small (within 1 nm of the 2 000 ft mark)
        Assert.InRange(last.DistanceToThresholdNm, 0.0, 1.0);
    }

    [Fact]
    public async Task RuntimeCoordinator_ApproachPath_EmptyWhenNoArrivalAirportConfigured()
    {
        // Without an ArrivalAirportIcao the coordinator cannot resolve reference coords
        // and the approach path must remain empty.
        var coordinator = new RuntimeCoordinator(
            new FlightSessionContext(), // no ArrivalAirportIcao
            new RunwayResolver(new StubRunwayDataProvider(null)));

        var t0 = new DateTimeOffset(2026, 4, 15, 20, 0, 0, TimeSpan.Zero);

        await coordinator.ProcessFrameAsync(Frame(t0, onGround: true, parkingBrake: true));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(1), onGround: true, parkingBrake: false, groundSpeed: 2));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(30), onGround: true, indicatedAirspeed: 55));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(31), onGround: false, altitudeAgl: 20, indicatedAirspeed: 90, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(40), onGround: false, altitudeAgl: 500, verticalSpeed: 1500, indicatedAirspeed: 160, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(100), onGround: false, altitudeAgl: 35_000, verticalSpeed: 0, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(131), onGround: false, altitudeAgl: 35_000, verticalSpeed: 0, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(200), onGround: false, altitudeAgl: 35_000, verticalSpeed: -600, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(231), onGround: false, altitudeAgl: 35_000, verticalSpeed: -600, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(290), onGround: false, altitudeAgl: 2_800, gearDown: true, verticalSpeed: -500, heading: 180));
        var final = await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(310), onGround: true, altitudeAgl: 0, groundSpeed: 100, heading: 180));

        Assert.Empty(final.State.ScoreInput.ApproachPath);
    }

    [Fact]
    public async Task RuntimeCoordinator_SendsLivePositionFromFirstFrame()
    {
        // Position upload is no longer gated on IsActiveFlight() — it fires from the
        // very first frame so pilots appear on the world map even if the tracker started
        // mid-flight (e.g. after an auto-update restart) and BlocksOff was never captured.
        var uploader = new SpyLivePositionUploader();
        var coordinator = new RuntimeCoordinator(
            new FlightSessionContext
            {
                FlightMode = "career",
                BidId = "48291",
            },
            new RunwayResolver(new StubRunwayDataProvider(null)),
            livePositionUploader: uploader);

        var t0 = new DateTimeOffset(2026, 4, 13, 16, 0, 0, TimeSpan.Zero);

        // Frame 1: still at the gate (Preflight) — upload fires immediately.
        await coordinator.ProcessFrameAsync(Frame(t0, onGround: true, parkingBrake: true));
        // Frame 2: pushing back (TaxiOut) — same position, only 1 s later → throttled (< 4 s, no movement).
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(1), onGround: true, parkingBrake: false, groundSpeed: 2));

        Assert.Single(uploader.Payloads);
        Assert.Equal("Preflight", uploader.Payloads[0].Phase);   // fires on first frame, not gated on TaxiOut
        Assert.Equal("career", uploader.Payloads[0].FlightMode);
        Assert.Equal("48291", uploader.Payloads[0].BidId);
    }

    [Fact]
    public async Task RuntimeCoordinator_ThrottlesLivePositionUploads()
    {
        var uploader = new SpyLivePositionUploader();
        var coordinator = new RuntimeCoordinator(
            new FlightSessionContext(),
            new RunwayResolver(new StubRunwayDataProvider(null)),
            livePositionUploader: uploader);

        var t0 = new DateTimeOffset(2026, 4, 13, 17, 0, 0, TimeSpan.Zero);

        await coordinator.ProcessFrameAsync(Frame(t0, onGround: true, parkingBrake: true, latitude: 40.0, longitude: -75.0));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(1), onGround: true, parkingBrake: false, groundSpeed: 2, latitude: 40.0, longitude: -75.0));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(2), onGround: true, groundSpeed: 4, latitude: 40.00001, longitude: -75.00001));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(3), onGround: true, groundSpeed: 5, latitude: 40.00002, longitude: -75.00002));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(5), onGround: true, groundSpeed: 6, latitude: 40.00003, longitude: -75.00003));

        Assert.Equal(2, uploader.Payloads.Count);
    }

    private static TelemetryFrame Frame(
        DateTimeOffset timestampUtc,
        bool onGround,
        bool parkingBrake = false,
        bool gearDown = false,
        double latitude = 40.0,
        double longitude = -75.0,
        double indicatedAirspeed = 0,
        double altitudeAgl = 0,
        double groundSpeed = 0,
        double verticalSpeed = 0,
        double heading = 0,
        bool engine1Running = true)   // default true: engine running during normal taxi/flight
    {
        return new TelemetryFrame
        {
            TimestampUtc = timestampUtc,
            Phase = FlightPhase.Preflight,
            OnGround = onGround,
            ParkingBrakeSet = parkingBrake,
            GearDown = gearDown,
            Latitude = latitude,
            Longitude = longitude,
            IndicatedAirspeedKnots = indicatedAirspeed,
            AltitudeFeet = altitudeAgl + 1000,
            AltitudeAglFeet = altitudeAgl,
            GroundSpeedKnots = groundSpeed,
            VerticalSpeedFpm = verticalSpeed,
            HeadingMagneticDegrees = heading,
            HeadingTrueDegrees = heading,
            Engine1Running = engine1Running,
        };
    }

    private static (double Latitude, double Longitude) Offset(
        double latitude,
        double longitude,
        double headingDegrees,
        double distanceFeet)
    {
        const double EarthRadiusFeet = 20_925_524.9;
        var headingRad = headingDegrees * Math.PI / 180.0;
        var distanceRad = distanceFeet / EarthRadiusFeet;
        var startLatRad = latitude * Math.PI / 180.0;
        var startLonRad = longitude * Math.PI / 180.0;

        var destLatRad = Math.Asin(
            Math.Sin(startLatRad) * Math.Cos(distanceRad) +
            Math.Cos(startLatRad) * Math.Sin(distanceRad) * Math.Cos(headingRad));

        var destLonRad = startLonRad + Math.Atan2(
            Math.Sin(headingRad) * Math.Sin(distanceRad) * Math.Cos(startLatRad),
            Math.Cos(distanceRad) - Math.Sin(startLatRad) * Math.Sin(destLatRad));

        return (destLatRad * 180.0 / Math.PI, destLonRad * 180.0 / Math.PI);
    }

    private sealed class StubRunwayDataProvider(AirportRunwayCatalog? catalog) : IRunwayDataProvider
    {
        public Task<AirportRunwayCatalog?> GetRunwaysAsync(string airportIcao, CancellationToken cancellationToken = default) =>
            Task.FromResult(catalog);
    }

    private sealed class SpyLivePositionUploader : ILivePositionUploader
    {
        public List<LivePositionPayload> Payloads { get; } = [];

        public Task<bool> SendPositionAsync(LivePositionPayload payload, CancellationToken cancellationToken = default)
        {
            Payloads.Add(payload);
            return Task.FromResult(true);
        }
    }
}
