using SimCrewOps.Hosting.Models;
using SimCrewOps.Sync.Models;
using SimCrewOps.Sync.Sync;

namespace SimCrewOps.Hosting.Hosting;

public sealed class BackgroundSyncCoordinator : IAsyncDisposable
{
    private readonly ICompletedSessionSyncService _completedSessionSyncService;
    private readonly TimeSpan _interval;
    private readonly int? _maxSessionsPerPass;
    private readonly SemaphoreSlim _syncGate = new(1, 1);

    private CancellationTokenSource? _loopCancellationSource;
    private Task? _loopTask;
    private BackgroundSyncStatus _status;

    public BackgroundSyncCoordinator(
        ICompletedSessionSyncService completedSessionSyncService,
        TimeSpan interval,
        int? maxSessionsPerPass = null)
    {
        ArgumentNullException.ThrowIfNull(completedSessionSyncService);
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be positive.");
        }

        _completedSessionSyncService = completedSessionSyncService;
        _interval = interval;
        _maxSessionsPerPass = maxSessionsPerPass;
        _status = new BackgroundSyncStatus
        {
            Enabled = true,
        };
    }

    public BackgroundSyncStatus Status => _status;

    public Task<PendingSessionSyncSummary> RunStartupSyncAsync(CancellationToken cancellationToken = default) =>
        RunSyncAsync("startup", cancellationToken);

    public Task<PendingSessionSyncSummary> SyncNowAsync(CancellationToken cancellationToken = default) =>
        RunSyncAsync("manual", cancellationToken);

    public void Start()
    {
        if (_loopTask is not null)
        {
            return;
        }

        _loopCancellationSource = new CancellationTokenSource();
        _status = _status with { IsRunning = true };
        _loopTask = RunLoopAsync(_loopCancellationSource.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_loopTask is null)
        {
            return;
        }

        _loopCancellationSource!.Cancel();

        try
        {
            await _loopTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_loopCancellationSource.IsCancellationRequested)
        {
        }
        finally
        {
            _loopCancellationSource.Dispose();
            _loopCancellationSource = null;
            _loopTask = null;
            _status = _status with { IsRunning = false };
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _syncGate.Dispose();
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            await RunSyncAsync("background", cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<PendingSessionSyncSummary> RunSyncAsync(string trigger, CancellationToken cancellationToken)
    {
        await _syncGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var startedUtc = DateTimeOffset.UtcNow;
            _status = _status with
            {
                LastTrigger = trigger,
                LastRunStartedUtc = startedUtc,
                LastErrorMessage = null,
            };

            var summary = await _completedSessionSyncService
                .SyncPendingSessionsAsync(_maxSessionsPerPass, cancellationToken)
                .ConfigureAwait(false);

            _status = _status with
            {
                LastTrigger = trigger,
                LastRunStartedUtc = startedUtc,
                LastRunCompletedUtc = DateTimeOffset.UtcNow,
                LastSummary = summary,
                LastErrorMessage = null,
                ConsecutiveFailureCount = 0,
            };

            return summary;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _status = _status with
            {
                LastTrigger = trigger,
                LastRunCompletedUtc = DateTimeOffset.UtcNow,
                LastErrorMessage = ex.Message,
                ConsecutiveFailureCount = _status.ConsecutiveFailureCount + 1,
            };

            throw;
        }
        finally
        {
            _syncGate.Release();
        }
    }
}
