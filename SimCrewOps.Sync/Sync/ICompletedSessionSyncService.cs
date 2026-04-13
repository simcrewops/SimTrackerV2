using SimCrewOps.Sync.Models;

namespace SimCrewOps.Sync.Sync;

public interface ICompletedSessionSyncService
{
    Task<PendingSessionSyncSummary> SyncPendingSessionsAsync(
        int? maxSessions = null,
        CancellationToken cancellationToken = default);
}
