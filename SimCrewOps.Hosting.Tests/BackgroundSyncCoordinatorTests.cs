using SimCrewOps.Hosting.Hosting;
using SimCrewOps.Sync.Models;
using SimCrewOps.Sync.Sync;
using Xunit;

namespace SimCrewOps.Hosting.Tests;

public sealed class BackgroundSyncCoordinatorTests
{
    [Fact]
    public async Task SyncNowAsync_UpdatesStatusAndUsesConfiguredBatchLimit()
    {
        var syncService = new FakeCompletedSessionSyncService();
        await using var coordinator = new BackgroundSyncCoordinator(syncService, TimeSpan.FromSeconds(60), maxSessionsPerPass: 4);

        var summary = await coordinator.SyncNowAsync();

        Assert.Equal(1, syncService.CallCount);
        Assert.Equal(4, syncService.LastMaxSessions);
        Assert.Equal(summary, coordinator.Status.LastSummary);
        Assert.Equal("manual", coordinator.Status.LastTrigger);
        Assert.Equal(0, coordinator.Status.ConsecutiveFailureCount);
        Assert.NotNull(coordinator.Status.LastRunStartedUtc);
        Assert.NotNull(coordinator.Status.LastRunCompletedUtc);
    }

    [Fact]
    public async Task RunStartupSyncAsync_TracksFailures()
    {
        var syncService = new ThrowingCompletedSessionSyncService();
        await using var coordinator = new BackgroundSyncCoordinator(syncService, TimeSpan.FromSeconds(60));

        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.RunStartupSyncAsync());

        Assert.Equal("startup", coordinator.Status.LastTrigger);
        Assert.Equal(1, coordinator.Status.ConsecutiveFailureCount);
        Assert.Equal("sync failed", coordinator.Status.LastErrorMessage);
        Assert.NotNull(coordinator.Status.LastRunStartedUtc);
        Assert.NotNull(coordinator.Status.LastRunCompletedUtc);
    }

    [Fact]
    public async Task StartAndStopAsync_ManageBackgroundLoopState()
    {
        var syncService = new SignalingCompletedSessionSyncService();
        await using var coordinator = new BackgroundSyncCoordinator(syncService, TimeSpan.FromMilliseconds(25));

        coordinator.Start();
        await syncService.FirstCall.WaitAsync(TimeSpan.FromSeconds(1));
        await coordinator.StopAsync();

        Assert.True(syncService.CallCount >= 1);
        Assert.False(coordinator.Status.IsRunning);
        Assert.Equal("background", coordinator.Status.LastTrigger);
    }

    private sealed class FakeCompletedSessionSyncService : ICompletedSessionSyncService
    {
        public int CallCount { get; private set; }
        public int? LastMaxSessions { get; private set; }

        public Task<PendingSessionSyncSummary> SyncPendingSessionsAsync(int? maxSessions = null, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastMaxSessions = maxSessions;

            return Task.FromResult(new PendingSessionSyncSummary
            {
                Results = Array.Empty<PendingSessionSyncResult>(),
            });
        }
    }

    private sealed class ThrowingCompletedSessionSyncService : ICompletedSessionSyncService
    {
        public Task<PendingSessionSyncSummary> SyncPendingSessionsAsync(int? maxSessions = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("sync failed");
    }

    private sealed class SignalingCompletedSessionSyncService : ICompletedSessionSyncService
    {
        private readonly TaskCompletionSource<bool> _firstCall = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount { get; private set; }
        public Task FirstCall => _firstCall.Task;

        public Task<PendingSessionSyncSummary> SyncPendingSessionsAsync(int? maxSessions = null, CancellationToken cancellationToken = default)
        {
            CallCount++;
            _firstCall.TrySetResult(true);

            return Task.FromResult(new PendingSessionSyncSummary
            {
                Results = Array.Empty<PendingSessionSyncResult>(),
            });
        }
    }
}
