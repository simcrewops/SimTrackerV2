using SimCrewOps.Persistence.Models;
using SimCrewOps.Persistence.Persistence;
using SimCrewOps.Runways.Models;
using SimCrewOps.Runtime.Models;
using SimCrewOps.Scoring.Models;
using SimCrewOps.Tracking.Models;
using Xunit;

namespace SimCrewOps.Persistence.Tests;

public sealed class FileSystemFlightSessionStoreTests : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(
        Path.GetTempPath(),
        "simcrewops-persistence-tests",
        Guid.NewGuid().ToString("n"));

    [Fact]
    public async Task SaveAndLoadCurrentSession_RoundTripsRuntimeState()
    {
        var store = CreateStore();
        var state = CreateState(FlightPhase.Approach, touchdownZoneExcessDistanceFeet: 120);

        await store.SaveCurrentSessionAsync(state);
        var loaded = await store.LoadCurrentSessionAsync();

        Assert.NotNull(loaded);
        Assert.Equal(PersistedCurrentSession.CurrentSchemaVersion, loaded!.SchemaVersion);
        Assert.Equal("KJFK", loaded.State.Context.DepartureAirportIcao);
        Assert.Equal("KMIA", loaded.State.Context.ArrivalAirportIcao);
        Assert.Equal(FlightPhase.Approach, loaded.State.CurrentPhase);
        Assert.Equal(120, loaded.State.ScoreInput.Landing.TouchdownZoneExcessDistanceFeet);
        Assert.Equal("A", loaded.State.ScoreResult.Grade);
        Assert.NotNull(loaded.State.LandingRunwayResolution);
    }

    [Fact]
    public async Task ClearCurrentSession_RemovesSavedSnapshot()
    {
        var store = CreateStore();

        await store.SaveCurrentSessionAsync(CreateState(FlightPhase.Cruise));
        await store.ClearCurrentSessionAsync();

        var loaded = await store.LoadCurrentSessionAsync();
        Assert.Null(loaded);
    }

    [Fact]
    public async Task QueueListAndRemoveCompletedSessions_ManagesPendingQueue()
    {
        var store = CreateStore();

        var first = await store.QueueCompletedSessionAsync(CreateState(FlightPhase.Arrival));
        await Task.Delay(20);
        var second = await store.QueueCompletedSessionAsync(CreateState(FlightPhase.Arrival, touchdownZoneExcessDistanceFeet: 260));

        var pending = await store.ListCompletedSessionsAsync();

        Assert.Equal(2, pending.Count);
        Assert.Equal(first.SessionId, pending[0].SessionId);
        Assert.Equal(second.SessionId, pending[1].SessionId);
        Assert.Equal(260, pending[1].State.ScoreInput.Landing.TouchdownZoneExcessDistanceFeet);

        var removed = await store.RemoveCompletedSessionAsync(first.SessionId);
        var missing = await store.RemoveCompletedSessionAsync(first.SessionId);
        var remaining = await store.ListCompletedSessionsAsync();

        Assert.True(removed);
        Assert.False(missing);
        Assert.Single(remaining);
        Assert.Equal(second.SessionId, remaining[0].SessionId);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    private FileSystemFlightSessionStore CreateStore() =>
        new(new FileSystemFlightSessionStoreOptions
        {
            RootDirectory = _rootDirectory,
        });

    private static FlightSessionRuntimeState CreateState(
        FlightPhase phase,
        double touchdownZoneExcessDistanceFeet = 0)
    {
        return new FlightSessionRuntimeState
        {
            Context = new FlightSessionContext
            {
                PilotId = "pilot-42",
                BidId = "bid-77",
                DepartureAirportIcao = "KJFK",
                ArrivalAirportIcao = "KMIA",
                FlightMode = "career",
            },
            CurrentPhase = phase,
            BlockTimes = new FlightSessionBlockTimes
            {
                BlocksOffUtc = new DateTimeOffset(2026, 4, 13, 12, 0, 0, TimeSpan.Zero),
                WheelsOffUtc = new DateTimeOffset(2026, 4, 13, 12, 15, 0, TimeSpan.Zero),
                WheelsOnUtc = new DateTimeOffset(2026, 4, 13, 14, 45, 0, TimeSpan.Zero),
                BlocksOnUtc = phase == FlightPhase.Arrival
                    ? new DateTimeOffset(2026, 4, 13, 14, 52, 0, TimeSpan.Zero)
                    : null,
            },
            LastTelemetryFrame = new TelemetryFrame
            {
                TimestampUtc = new DateTimeOffset(2026, 4, 13, 14, 50, 0, TimeSpan.Zero),
                Phase = phase,
                Latitude = 25.7933,
                Longitude = -80.2906,
                HeadingTrueDegrees = 90,
                TouchdownZoneExcessDistanceFeet = touchdownZoneExcessDistanceFeet,
            },
            LandingRunwayResolution = new RunwayResolutionResult
            {
                AirportIcao = "KMIA",
                HeadingDifferenceDegrees = 2,
                Runway = new RunwayEnd
                {
                    AirportIcao = "KMIA",
                    RunwayIdentifier = "09",
                    TrueHeadingDegrees = 90,
                    LengthFeet = 13_000,
                    ThresholdLatitude = 25.7933,
                    ThresholdLongitude = -80.2906,
                    DataSource = RunwayDataSource.OurAirportsFallback,
                },
                Projection = new TouchdownProjection
                {
                    AlongTrackDistanceFeet = 3_260,
                    DistanceFromThresholdFeet = 3_260,
                    CrossTrackDistanceFeet = 12,
                    TouchdownZoneExcessDistanceFeet = touchdownZoneExcessDistanceFeet,
                },
            },
            ScoreInput = new FlightScoreInput
            {
                Landing = new LandingMetrics
                {
                    TouchdownZoneExcessDistanceFeet = touchdownZoneExcessDistanceFeet,
                    TouchdownVerticalSpeedFpm = 280,
                    TouchdownGForce = 1.18,
                    BounceCount = 0,
                },
            },
            ScoreResult = new ScoreResult(
                100,
                92,
                "A",
                false,
                Array.Empty<PhaseScoreResult>(),
                Array.Empty<ScoreFinding>()),
        };
    }
}
