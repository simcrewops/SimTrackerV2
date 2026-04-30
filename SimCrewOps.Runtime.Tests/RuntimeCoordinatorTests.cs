using SimCrewOps.PhaseEngine.Models;
using SimCrewOps.Runtime.Models;
using SimCrewOps.Runtime.Runtime;
using SimCrewOps.Scoring.Models;
using SimCrewOps.Tracking.Models;
using Xunit;

namespace SimCrewOps.Runtime.Tests;

public sealed class RuntimeCoordinatorTests
{
    // ── Block times ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RuntimeCoordinator_CapturesBlockTimes()
    {
        var coordinator = new RuntimeCoordinator(new FlightSessionContext
        {
            DepartureAirportIcao = "KDEP",
            ArrivalAirportIcao   = "KARR",
        });

        var t0 = new DateTimeOffset(2026, 4, 13, 12, 0, 0, TimeSpan.Zero);

        await coordinator.ProcessFrameAsync(Frame(t0, onGround: true, parkingBrake: true));
        var blocksOff = await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(1), onGround: true, parkingBrake: false, groundSpeed: 2));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(30), onGround: true, indicatedAirspeed: 55));
        var wheelsOff = await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(31), onGround: false, altitudeAgl: 20, indicatedAirspeed: 90));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(40), onGround: false, altitudeAgl: 500, verticalSpeed: 1500, indicatedAirspeed: 160));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(100), onGround: false, altitudeAgl: 35_000, verticalSpeed: 0, indicatedAirspeed: 280));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(131), onGround: false, altitudeAgl: 35_000, verticalSpeed: 0, indicatedAirspeed: 280));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(200), onGround: false, altitudeAgl: 35_000, verticalSpeed: -600));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(231), onGround: false, altitudeAgl: 35_000, verticalSpeed: -600));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(290), onGround: false, altitudeAgl: 2_800, gearDown: true, verticalSpeed: -500));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(300), onGround: false, altitudeAgl: 100));
        var wheelsOn = await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(310), onGround: true, altitudeAgl: 0, groundSpeed: 100));

        Assert.Equal(BlockEventType.BlocksOff, blocksOff.PhaseFrame.BlockEvent!.Type);
        Assert.Equal(BlockEventType.WheelsOff, wheelsOff.PhaseFrame.BlockEvent!.Type);
        Assert.Equal(BlockEventType.WheelsOn,  wheelsOn.PhaseFrame.BlockEvent!.Type);
        Assert.NotNull(wheelsOn.State.BlockTimes.BlocksOffUtc);
        Assert.NotNull(wheelsOn.State.BlockTimes.WheelsOffUtc);
        Assert.NotNull(wheelsOn.State.BlockTimes.WheelsOnUtc);
        Assert.Equal(FlightPhase.Landing, wheelsOn.State.CurrentPhase);
    }

    [Fact]
    public async Task NoRunwayServiceRequiredToCompleteSession()
    {
        var coordinator = new RuntimeCoordinator(new FlightSessionContext
        {
            DepartureAirportIcao = "KDEP",
            ArrivalAirportIcao   = "KARR",
        });

        var t0 = new DateTimeOffset(2026, 4, 13, 12, 0, 0, TimeSpan.Zero);

        await coordinator.ProcessFrameAsync(Frame(t0, onGround: true, parkingBrake: true));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(1), onGround: true, parkingBrake: false, groundSpeed: 2));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(30), onGround: true, indicatedAirspeed: 55));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(31), onGround: false, altitudeAgl: 20, indicatedAirspeed: 90));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(40), onGround: false, altitudeAgl: 500, verticalSpeed: 1500, indicatedAirspeed: 160));
        // Cruise (VS ≈ 0 sustained ≥ 30 s)
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(100), onGround: false, altitudeAgl: 35_000, verticalSpeed: 0));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(131), onGround: false, altitudeAgl: 35_000, verticalSpeed: 0));
        // Descent (VS < -200 sustained ≥ 30 s)
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(200), onGround: false, altitudeAgl: 35_000, verticalSpeed: -600));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(231), onGround: false, altitudeAgl: 35_000, verticalSpeed: -600));
        // Approach (AGL < 3000 + gear down)
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(290), onGround: false, altitudeAgl: 2_800, gearDown: true, verticalSpeed: -500));
        // WheelsOn: touchdown
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(310), onGround: true, altitudeAgl: 0, groundSpeed: 100));
        // Landing rollout: GS < 40 sustained ≥ 5 s → TaxiIn
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(320), onGround: true, groundSpeed: 30));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(326), onGround: true, groundSpeed: 10));
        // Parking brake set + GS ≈ 0 → Arrival + BlocksOn
        var arrival = await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(327), onGround: true, parkingBrake: true, groundSpeed: 0));

        Assert.Equal(FlightPhase.Arrival, arrival.State.CurrentPhase);
        Assert.True(arrival.State.IsComplete);
        Assert.NotNull(arrival.State.BlockTimes.BlocksOnUtc);
    }

    // ── Session restore ───────────────────────────────────────────────────────

    [Fact]
    public async Task RuntimeCoordinator_Restore_ContinuesSavedSessionWithoutRestarting()
    {
        var coordinator = new RuntimeCoordinator(new FlightSessionContext());

        var t0 = new DateTimeOffset(2026, 4, 13, 14, 0, 0, TimeSpan.Zero);
        coordinator.Restore(new FlightSessionRuntimeState
        {
            Context = new FlightSessionContext
            {
                DepartureAirportIcao = "KDEP",
                ArrivalAirportIcao   = "KARR",
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
                WheelsOnUtc  = t0.AddMinutes(-5),
            },
            LastTelemetryFrame = Frame(t0, onGround: true, groundSpeed: 18) with
            {
                Phase          = FlightPhase.TaxiIn,
                TaxiLightsOn   = true,
                Engine1Running = true,
                Engine2Running = true,
                Engine3Running = true,
                Engine4Running = true,
            },
            ScoreInput = new FlightScoreInput
            {
                Preflight = new PreflightMetrics { BeaconOnBeforeTaxi = true },
                Climb     = new ClimbMetrics     { HeavyFourEngineAircraft = true },
            },
            ScoreResult = new ScoreResult(100, 92, "A", false, Array.Empty<PhaseScoreResult>(), Array.Empty<ScoreFinding>()),
        });

        var arrival = await coordinator.ProcessFrameAsync(
            Frame(t0.AddSeconds(1), onGround: true, parkingBrake: true, groundSpeed: 0) with
            {
                TaxiLightsOn   = false,
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

    // ── Beacon gate ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RuntimeCoordinator_DoesNotBeaconOnFirstFrame()
    {
        var uploader = new SpyLivePositionUploader();
        var coordinator = new RuntimeCoordinator(
            new FlightSessionContext { FlightMode = "career", BidId = "48291" },
            livePositionUploader: uploader);

        var t0 = new DateTimeOffset(2026, 4, 13, 16, 0, 0, TimeSpan.Zero);

        // First frame with valid GPS: seeds position reference, does NOT beacon.
        await coordinator.ProcessFrameAsync(Frame(t0, onGround: true, parkingBrake: true, latitude: 40.0, longitude: -75.0));

        // Give the fire-and-forget a tick to complete (though it should not have fired).
        await Task.Delay(50);

        Assert.Empty(uploader.Payloads);
    }

    [Fact]
    public async Task RuntimeCoordinator_BeaconsOnSecondConfirmedFrame()
    {
        var uploader = new SpyLivePositionUploader();
        var coordinator = new RuntimeCoordinator(
            new FlightSessionContext { FlightMode = "career", BidId = "48291" },
            livePositionUploader: uploader);

        var t0 = new DateTimeOffset(2026, 4, 13, 16, 0, 0, TimeSpan.Zero);

        // Frame 1: GPS seeds — no beacon.
        await coordinator.ProcessFrameAsync(Frame(t0, onGround: true, parkingBrake: true, latitude: 40.0, longitude: -75.0));

        // Frame 2: first confirmed phase with valid GPS — beacon fires regardless of movement
        // because _lastLivePositionSentUtc is still null (elapsed = MaxValue).
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(1), onGround: true, parkingBrake: false, groundSpeed: 2, latitude: 40.0001, longitude: -75.0001));

        // Wait for fire-and-forget.
        await Task.Delay(50);

        Assert.Single(uploader.Payloads);
        Assert.Equal("career", uploader.Payloads[0].FlightMode);
        Assert.Equal("48291",  uploader.Payloads[0].BidId);
    }

    [Fact]
    public async Task RuntimeCoordinator_StopsBeaconingAfterArrivalComplete()
    {
        var uploader = new SpyLivePositionUploader();
        var coordinator = new RuntimeCoordinator(
            new FlightSessionContext(),
            livePositionUploader: uploader);

        var t0 = new DateTimeOffset(2026, 4, 13, 16, 0, 0, TimeSpan.Zero);

        // Drive through a full flight cycle. Position increments stay under 0.01° (< 1 nm)
        // so reposition detection never fires and _blockTimes accumulates correctly.
        await coordinator.ProcessFrameAsync(Frame(t0, onGround: true, parkingBrake: true, latitude: 40.000, longitude: -75.000));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(1), onGround: true, parkingBrake: false, groundSpeed: 2, latitude: 40.001, longitude: -75.001));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(31), onGround: false, altitudeAgl: 20, indicatedAirspeed: 90, latitude: 40.002, longitude: -75.002));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(40), onGround: false, altitudeAgl: 500, verticalSpeed: 1500, indicatedAirspeed: 160, latitude: 40.003, longitude: -75.003));
        // Cruise (VS ≈ 0 sustained ≥ 30 s)
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(100), onGround: false, altitudeAgl: 35_000, verticalSpeed: 0, latitude: 40.004, longitude: -75.004));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(131), onGround: false, altitudeAgl: 35_000, verticalSpeed: 0, latitude: 40.005, longitude: -75.005));
        // Descent (VS < -200 sustained ≥ 30 s)
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(200), onGround: false, altitudeAgl: 35_000, verticalSpeed: -600, latitude: 40.006, longitude: -75.006));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(231), onGround: false, altitudeAgl: 35_000, verticalSpeed: -600, latitude: 40.007, longitude: -75.007));
        // Approach (AGL < 3000 + gear down)
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(290), onGround: false, altitudeAgl: 2_800, gearDown: true, verticalSpeed: -500, latitude: 40.008, longitude: -75.008));
        // WheelsOn: touchdown
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(310), onGround: true, altitudeAgl: 0, groundSpeed: 100, latitude: 40.009, longitude: -75.009));
        // Landing rollout: GS < 40 sustained ≥ 5 s → TaxiIn
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(320), onGround: true, groundSpeed: 30, latitude: 40.009, longitude: -75.009));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(326), onGround: true, groundSpeed: 10, latitude: 40.009, longitude: -75.009));
        // Parking brake set + GS ≈ 0 → Arrival + BlocksOn (session complete).
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(327), onGround: true, parkingBrake: true, groundSpeed: 0, latitude: 40.009, longitude: -75.009));

        await Task.Delay(50);

        var countBeforeArrivalComplete = uploader.Payloads.Count;

        // Additional frames after session complete — must not beacon.
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(500), onGround: true, parkingBrake: true, groundSpeed: 0, latitude: 40.009, longitude: -75.009));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(600), onGround: true, parkingBrake: true, groundSpeed: 0, latitude: 40.010, longitude: -75.010));

        await Task.Delay(50);

        Assert.Equal(countBeforeArrivalComplete, uploader.Payloads.Count);
    }

    [Fact]
    public async Task RuntimeCoordinator_FailedBeaconDoesNotAdvanceThrottleCursor()
    {
        var uploader = new SpyLivePositionUploader(returnSuccess: false);
        var coordinator = new RuntimeCoordinator(
            new FlightSessionContext(),
            livePositionUploader: uploader);

        var t0 = new DateTimeOffset(2026, 4, 13, 16, 0, 0, TimeSpan.Zero);

        // Frame 1: seeds position (no beacon).
        await coordinator.ProcessFrameAsync(Frame(t0, onGround: true, parkingBrake: true, latitude: 40.0, longitude: -75.0));

        // Frame 2: elapsed=MaxValue → attempts send → uploader returns false → cursor not advanced.
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(1), onGround: true, parkingBrake: false, groundSpeed: 2, latitude: 40.0001, longitude: -75.0001));

        // Frame 3: same pos, elapsed only 1s — but because cursor was NOT advanced (previous failed),
        // _lastLivePositionSentUtc is still null → elapsed = MaxValue → attempts send again.
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(2), onGround: true, groundSpeed: 3, latitude: 40.0001, longitude: -75.0001));

        await Task.Delay(50);

        // Both frame 2 and frame 3 should have attempted a send.
        Assert.Equal(2, uploader.Payloads.Count);
    }

    // ── Throttle ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RuntimeCoordinator_ThrottlesLivePositionUploads()
    {
        var uploader = new SpyLivePositionUploader();
        var coordinator = new RuntimeCoordinator(
            new FlightSessionContext(),
            livePositionUploader: uploader);

        var t0 = new DateTimeOffset(2026, 4, 13, 17, 0, 0, TimeSpan.Zero);

        // Frame 1: seeds, no beacon.
        await coordinator.ProcessFrameAsync(Frame(t0, onGround: true, parkingBrake: true, latitude: 40.0, longitude: -75.0));
        // Frame 2: elapsed=MaxValue → beacon (cursor set to t0+1, pos=40.0,-75.0).
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(1), onGround: true, parkingBrake: false, groundSpeed: 2, latitude: 40.0, longitude: -75.0));
        // Frame 3-4: same pos, elapsed < 4s → throttled.
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(2), onGround: true, groundSpeed: 4, latitude: 40.00001, longitude: -75.00001));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(3), onGround: true, groundSpeed: 5, latitude: 40.00002, longitude: -75.00002));
        // Frame 5: elapsed = 4s → beacon.
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(5), onGround: true, groundSpeed: 6, latitude: 40.00003, longitude: -75.00003));

        await Task.Delay(50);

        Assert.Equal(2, uploader.Payloads.Count);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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
        bool engine1Running = true)
    {
        return new TelemetryFrame
        {
            TimestampUtc           = timestampUtc,
            Phase                  = FlightPhase.Preflight,
            OnGround               = onGround,
            ParkingBrakeSet        = parkingBrake,
            GearDown               = gearDown,
            Latitude               = latitude,
            Longitude              = longitude,
            IndicatedAirspeedKnots = indicatedAirspeed,
            AltitudeFeet           = altitudeAgl + 1000,
            AltitudeAglFeet        = altitudeAgl,
            GroundSpeedKnots       = groundSpeed,
            VerticalSpeedFpm       = verticalSpeed,
            HeadingMagneticDegrees = heading,
            HeadingTrueDegrees     = heading,
            Engine1Running         = engine1Running,
        };
    }

    private sealed class SpyLivePositionUploader(bool returnSuccess = true) : ILivePositionUploader
    {
        public List<LivePositionPayload> Payloads { get; } = [];

        public Task<bool> SendPositionAsync(LivePositionPayload payload, CancellationToken cancellationToken = default)
        {
            Payloads.Add(payload);
            return Task.FromResult(returnSuccess);
        }
    }
}
