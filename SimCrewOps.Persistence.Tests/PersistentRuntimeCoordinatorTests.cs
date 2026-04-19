using SimCrewOps.Persistence.Models;
using SimCrewOps.Persistence.Persistence;
using SimCrewOps.Runways.Models;
using SimCrewOps.Runways.Providers;
using SimCrewOps.Runways.Services;
using SimCrewOps.Runtime.Models;
using SimCrewOps.Runtime.Runtime;
using SimCrewOps.Scoring.Models;
using SimCrewOps.Tracking.Models;
using Xunit;

namespace SimCrewOps.Persistence.Tests;

public sealed class PersistentRuntimeCoordinatorTests
{
    [Fact]
    public async Task ProcessFrameAsync_SavesActiveSession_ThenQueuesAndClearsOnArrival()
    {
        var store = new SpyFlightSessionStore();
        var coordinator = CreateCoordinator(store, arrivalAirportIcao: "KTEST");
        var t0 = new DateTimeOffset(2026, 4, 13, 12, 0, 0, TimeSpan.Zero);

        var first = await coordinator.ProcessFrameAsync(Frame(t0, onGround: true, parkingBrake: true));
        Assert.True(first.Persistence.CurrentSessionSaved);
        Assert.Equal(1, store.SaveCurrentSessionCallCount);
        Assert.Equal(0, store.QueueCompletedSessionCallCount);

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
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(310), onGround: true, altitudeAgl: 0, groundSpeed: 100, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(311), onGround: true, altitudeAgl: 0, groundSpeed: 20, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(316), onGround: true, altitudeAgl: 0, groundSpeed: 20, heading: 180));

        var arrival = await coordinator.ProcessFrameAsync(Frame(
            t0.AddSeconds(317),
            onGround: true,
            altitudeAgl: 0,
            groundSpeed: 0,
            parkingBrake: true,
            heading: 180));

        Assert.True(arrival.RuntimeFrame.State.IsComplete);
        Assert.False(arrival.Persistence.CurrentSessionSaved);
        Assert.True(arrival.Persistence.CurrentSessionCleared);
        Assert.NotNull(arrival.Persistence.QueuedCompletedSession);
        Assert.True(store.SaveCurrentSessionCallCount > 0);
        Assert.Equal(1, store.QueueCompletedSessionCallCount);
        Assert.Equal(1, store.ClearCurrentSessionCallCount);
        Assert.NotNull(store.LastSavedCurrentState);
        Assert.False(store.LastSavedCurrentState!.IsComplete);
        Assert.Equal(FlightPhase.Arrival, store.LastQueuedCompletedState!.CurrentPhase);
    }

    [Fact]
    public async Task ProcessFrameAsync_DoesNotQueueCompletedSessionTwice()
    {
        var store = new SpyFlightSessionStore();
        var coordinator = CreateCoordinator(store, arrivalAirportIcao: "KTEST");
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
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(310), onGround: true, altitudeAgl: 0, groundSpeed: 100, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(311), onGround: true, altitudeAgl: 0, groundSpeed: 20, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(316), onGround: true, altitudeAgl: 0, groundSpeed: 20, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(317), onGround: true, altitudeAgl: 0, groundSpeed: 0, parkingBrake: true, heading: 180));

        var duplicateArrival = await coordinator.ProcessFrameAsync(Frame(
            t0.AddSeconds(318),
            onGround: true,
            altitudeAgl: 0,
            groundSpeed: 0,
            parkingBrake: true,
            heading: 180));

        Assert.True(duplicateArrival.RuntimeFrame.State.IsComplete);
        Assert.Null(duplicateArrival.Persistence.QueuedCompletedSession);
        Assert.False(duplicateArrival.Persistence.CurrentSessionSaved);
        Assert.False(duplicateArrival.Persistence.CurrentSessionCleared);
        Assert.Equal(1, store.QueueCompletedSessionCallCount);
        Assert.Equal(1, store.ClearCurrentSessionCallCount);
    }

    [Fact]
    public async Task GetRecoverySnapshotAsync_ReturnsCurrentAndPendingSessions()
    {
        var store = new SpyFlightSessionStore
        {
            LoadedCurrentSession = new PersistedCurrentSession
            {
                SavedUtc = new DateTimeOffset(2026, 4, 13, 14, 0, 0, TimeSpan.Zero),
                State = CreateState(FlightPhase.Cruise),
            },
            PendingCompletedSessions =
            {
                new PendingCompletedSession
                {
                    SessionId = "queued-1",
                    SavedUtc = new DateTimeOffset(2026, 4, 13, 14, 30, 0, TimeSpan.Zero),
                    State = CreateState(FlightPhase.Arrival),
                },
            },
        };

        var coordinator = CreateCoordinator(store);
        var snapshot = await coordinator.GetRecoverySnapshotAsync();

        Assert.True(snapshot.HasRecoverableCurrentSession);
        Assert.True(snapshot.HasPendingCompletedSessions);
        Assert.Equal(FlightPhase.Cruise, snapshot.CurrentSession!.State.CurrentPhase);
        Assert.Single(snapshot.PendingCompletedSessions);
        Assert.Equal("queued-1", snapshot.PendingCompletedSessions[0].SessionId);
    }

    [Fact]
    public async Task Restore_ContinuesRecoveredSessionAndQueuesCompletionOnce()
    {
        var store = new SpyFlightSessionStore();
        var coordinator = CreateCoordinator(store, arrivalAirportIcao: "KTEST");
        var t0 = new DateTimeOffset(2026, 4, 13, 15, 0, 0, TimeSpan.Zero);

        coordinator.Restore(new FlightSessionRuntimeState
        {
            Context = new FlightSessionContext
            {
                DepartureAirportIcao = "KDEP",
                ArrivalAirportIcao = "KTEST",
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
                WheelsOnUtc = t0.AddMinutes(-4),
            },
            LastTelemetryFrame = Frame(
                t0,
                onGround: true,
                altitudeAgl: 0,
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
            ScoreResult = new ScoreResult(100, 91, "A", false, Array.Empty<PhaseScoreResult>(), Array.Empty<ScoreFinding>()),
        });

        var arrival = await coordinator.ProcessFrameAsync(
            Frame(
                t0.AddSeconds(1),
                onGround: true,
                altitudeAgl: 0,
                groundSpeed: 0,
                parkingBrake: true,
                heading: 180) with
            {
                TaxiLightsOn = false,
                Engine1Running = true,
                Engine2Running = true,
                Engine3Running = true,
                Engine4Running = true,
            });

        Assert.True(arrival.RuntimeFrame.State.IsComplete);
        Assert.True(arrival.Persistence.CurrentSessionCleared);
        Assert.NotNull(arrival.Persistence.QueuedCompletedSession);
        Assert.Equal(1, store.QueueCompletedSessionCallCount);
        Assert.Equal(1, store.ClearCurrentSessionCallCount);
        Assert.True(store.LastQueuedCompletedState!.ScoreInput.Preflight.BeaconOnBeforeTaxi);
        Assert.True(store.LastQueuedCompletedState.ScoreInput.Climb.HeavyFourEngineAircraft);
        Assert.NotNull(store.LastQueuedCompletedState.BlockTimes.BlocksOnUtc);
    }

    private static PersistentRuntimeCoordinator CreateCoordinator(
        SpyFlightSessionStore store,
        string? arrivalAirportIcao = null)
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

        var runtime = new RuntimeCoordinator(
            new FlightSessionContext
            {
                DepartureAirportIcao = "KDEP",
                ArrivalAirportIcao = arrivalAirportIcao,
            },
            new RunwayResolver(provider));

        return new PersistentRuntimeCoordinator(runtime, store);
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
            AltitudeAglFeet = altitudeAgl,
            GroundSpeedKnots = groundSpeed,
            VerticalSpeedFpm = verticalSpeed,
            HeadingTrueDegrees = heading,
            Engine1Running = engine1Running,
        };
    }

    private static FlightSessionRuntimeState CreateState(FlightPhase phase) =>
        new()
        {
            Context = new FlightSessionContext
            {
                DepartureAirportIcao = "KDEP",
                ArrivalAirportIcao = "KTEST",
            },
            CurrentPhase = phase,
            BlockTimes = new FlightSessionBlockTimes(),
            ScoreInput = new(),
            ScoreResult = new(100, 88, "B", false, Array.Empty<PhaseScoreResult>(), Array.Empty<ScoreFinding>()),
        };

    private sealed class StubRunwayDataProvider(AirportRunwayCatalog? catalog) : IRunwayDataProvider
    {
        public Task<AirportRunwayCatalog?> GetRunwaysAsync(string airportIcao, CancellationToken cancellationToken = default) =>
            Task.FromResult(catalog);
    }

    private sealed class SpyFlightSessionStore : IFlightSessionStore
    {
        public int SaveCurrentSessionCallCount { get; private set; }
        public int ClearCurrentSessionCallCount { get; private set; }
        public int QueueCompletedSessionCallCount { get; private set; }

        public PersistedCurrentSession? LoadedCurrentSession { get; init; }
        public List<PendingCompletedSession> PendingCompletedSessions { get; } = new();
        public FlightSessionRuntimeState? LastSavedCurrentState { get; private set; }
        public FlightSessionRuntimeState? LastQueuedCompletedState { get; private set; }

        public Task SaveCurrentSessionAsync(FlightSessionRuntimeState state, CancellationToken cancellationToken = default)
        {
            SaveCurrentSessionCallCount++;
            LastSavedCurrentState = state;
            return Task.CompletedTask;
        }

        public Task<PersistedCurrentSession?> LoadCurrentSessionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(LoadedCurrentSession);

        public Task ClearCurrentSessionAsync(CancellationToken cancellationToken = default)
        {
            ClearCurrentSessionCallCount++;
            return Task.CompletedTask;
        }

        public Task<PendingCompletedSession> QueueCompletedSessionAsync(FlightSessionRuntimeState state, CancellationToken cancellationToken = default)
        {
            QueueCompletedSessionCallCount++;
            LastQueuedCompletedState = state;

            var record = new PendingCompletedSession
            {
                SessionId = $"queued-{QueueCompletedSessionCallCount}",
                SavedUtc = DateTimeOffset.UtcNow,
                State = state,
            };

            PendingCompletedSessions.Add(record);
            return Task.FromResult(record);
        }

        public Task<IReadOnlyList<PendingCompletedSession>> ListCompletedSessionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PendingCompletedSession>>(PendingCompletedSessions.ToArray());

        public Task<bool> RemoveCompletedSessionAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            var removed = PendingCompletedSessions.RemoveAll(item => item.SessionId == sessionId) > 0;
            return Task.FromResult(removed);
        }
    }
}
