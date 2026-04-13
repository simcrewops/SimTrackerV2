using SimCrewOps.Persistence.Models;
using SimCrewOps.Persistence.Persistence;
using SimCrewOps.Runtime.Models;
using SimCrewOps.Scoring.Models;
using SimCrewOps.Sync.Models;
using SimCrewOps.Sync.Sync;
using Xunit;

namespace SimCrewOps.Sync.Tests;

public sealed class CompletedSessionSyncServiceTests
{
    [Fact]
    public async Task SyncPendingSessionsAsync_RemovesOnlySuccessfulUploads()
    {
        var first = CreatePendingSession("session-1");
        var second = CreatePendingSession("session-2");
        var store = new SpyFlightSessionStore(first, second);
        var uploader = new StubCompletedSessionUploader(session => Task.FromResult(
            session.SessionId switch
            {
                "session-1" => new CompletedSessionUploadResult
                {
                    Status = SessionUploadStatus.Success,
                    StatusCode = 201,
                    RemoteSessionId = "remote-1",
                },
                _ => new CompletedSessionUploadResult
                {
                    Status = SessionUploadStatus.RetryableFailure,
                    StatusCode = 503,
                    ErrorMessage = "Service unavailable",
                },
            }));

        var service = new CompletedSessionSyncService(store, uploader);
        var summary = await service.SyncPendingSessionsAsync();

        Assert.Equal(2, summary.AttemptedCount);
        Assert.Equal(1, summary.SucceededCount);
        Assert.Equal(1, summary.RetryableFailureCount);
        Assert.Equal(0, summary.PermanentFailureCount);
        Assert.Single(store.RemovedSessionIds);
        Assert.Equal("session-1", store.RemovedSessionIds[0]);
        Assert.Contains(summary.Results, result => result.SessionId == "session-1" && result.RemovedFromQueue);
        Assert.Contains(summary.Results, result => result.SessionId == "session-2" && !result.RemovedFromQueue);
    }

    [Fact]
    public async Task SyncPendingSessionsAsync_KeepsPermanentFailuresQueued()
    {
        var session = CreatePendingSession("session-1");
        var store = new SpyFlightSessionStore(session);
        var uploader = new StubCompletedSessionUploader(_ => Task.FromResult(
            new CompletedSessionUploadResult
            {
                Status = SessionUploadStatus.PermanentFailure,
                StatusCode = 422,
                ErrorMessage = "Validation failed",
            }));

        var service = new CompletedSessionSyncService(store, uploader);
        var summary = await service.SyncPendingSessionsAsync();

        Assert.Single(summary.Results);
        Assert.Equal(SessionUploadStatus.PermanentFailure, summary.Results[0].Status);
        Assert.False(summary.Results[0].RemovedFromQueue);
        Assert.Empty(store.RemovedSessionIds);
    }

    [Fact]
    public async Task SyncPendingSessionsAsync_TreatsExceptionsAsRetryableFailures()
    {
        var session = CreatePendingSession("session-1");
        var store = new SpyFlightSessionStore(session);
        var uploader = new StubCompletedSessionUploader(_ => throw new InvalidOperationException("Network down"));

        var service = new CompletedSessionSyncService(store, uploader);
        var summary = await service.SyncPendingSessionsAsync();

        Assert.Single(summary.Results);
        Assert.Equal(SessionUploadStatus.RetryableFailure, summary.Results[0].Status);
        Assert.Equal("Network down", summary.Results[0].ErrorMessage);
        Assert.Empty(store.RemovedSessionIds);
    }

    [Fact]
    public async Task SyncPendingSessionsAsync_RespectsMaxSessionsLimit()
    {
        var first = CreatePendingSession("session-1");
        var second = CreatePendingSession("session-2");
        var third = CreatePendingSession("session-3");
        var store = new SpyFlightSessionStore(first, second, third);
        var uploader = new StubCompletedSessionUploader(session => Task.FromResult(
            new CompletedSessionUploadResult
            {
                Status = SessionUploadStatus.Success,
                StatusCode = 201,
                RemoteSessionId = $"remote-{session.SessionId}",
            }));

        var service = new CompletedSessionSyncService(store, uploader);
        var summary = await service.SyncPendingSessionsAsync(maxSessions: 2);

        Assert.Equal(2, summary.AttemptedCount);
        Assert.Equal(new[] { "session-1", "session-2" }, store.RemovedSessionIds);
    }

    private static PendingCompletedSession CreatePendingSession(string sessionId) =>
        new()
        {
            SessionId = sessionId,
            SavedUtc = new DateTimeOffset(2026, 4, 13, 15, 0, 0, TimeSpan.Zero),
            State = new FlightSessionRuntimeState
            {
                Context = new FlightSessionContext
                {
                    PilotId = "pilot-42",
                    BidId = "bid-77",
                    DepartureAirportIcao = "KJFK",
                    ArrivalAirportIcao = "KMIA",
                    FlightMode = "career",
                },
                CurrentPhase = FlightPhase.Arrival,
                BlockTimes = new FlightSessionBlockTimes
                {
                    BlocksOffUtc = new DateTimeOffset(2026, 4, 13, 12, 0, 0, TimeSpan.Zero),
                    WheelsOffUtc = new DateTimeOffset(2026, 4, 13, 12, 15, 0, TimeSpan.Zero),
                    WheelsOnUtc = new DateTimeOffset(2026, 4, 13, 14, 45, 0, TimeSpan.Zero),
                    BlocksOnUtc = new DateTimeOffset(2026, 4, 13, 14, 52, 0, TimeSpan.Zero),
                },
                ScoreInput = new(),
                ScoreResult = new(100, 91, "A", false, Array.Empty<PhaseScoreResult>(), Array.Empty<ScoreFinding>()),
            },
        };

    private sealed class SpyFlightSessionStore(params PendingCompletedSession[] sessions) : IFlightSessionStore
    {
        private readonly List<PendingCompletedSession> _pendingSessions = sessions.ToList();

        public List<string> RemovedSessionIds { get; } = new();

        public Task SaveCurrentSessionAsync(FlightSessionRuntimeState state, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<PersistedCurrentSession?> LoadCurrentSessionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<PersistedCurrentSession?>(null);

        public Task ClearCurrentSessionAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<PendingCompletedSession> QueueCompletedSessionAsync(FlightSessionRuntimeState state, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<PendingCompletedSession>> ListCompletedSessionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PendingCompletedSession>>(_pendingSessions.ToArray());

        public Task<bool> RemoveCompletedSessionAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            RemovedSessionIds.Add(sessionId);
            _pendingSessions.RemoveAll(session => session.SessionId == sessionId);
            return Task.FromResult(true);
        }
    }

    private sealed class StubCompletedSessionUploader(
        Func<PendingCompletedSession, Task<CompletedSessionUploadResult>> uploadAsync) : ICompletedSessionUploader
    {
        public Task<CompletedSessionUploadResult> UploadAsync(
            PendingCompletedSession session,
            CancellationToken cancellationToken = default) =>
            uploadAsync(session);
    }
}
