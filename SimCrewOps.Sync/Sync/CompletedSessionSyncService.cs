using SimCrewOps.Persistence.Models;
using SimCrewOps.Persistence.Persistence;
using SimCrewOps.Sync.Models;

namespace SimCrewOps.Sync.Sync;

public sealed class CompletedSessionSyncService : ICompletedSessionSyncService
{
    private readonly IFlightSessionStore _flightSessionStore;
    private readonly ICompletedSessionUploader _completedSessionUploader;

    public CompletedSessionSyncService(
        IFlightSessionStore flightSessionStore,
        ICompletedSessionUploader completedSessionUploader)
    {
        ArgumentNullException.ThrowIfNull(flightSessionStore);
        ArgumentNullException.ThrowIfNull(completedSessionUploader);

        _flightSessionStore = flightSessionStore;
        _completedSessionUploader = completedSessionUploader;
    }

    public async Task<PendingSessionSyncSummary> SyncPendingSessionsAsync(
        int? maxSessions = null,
        CancellationToken cancellationToken = default)
    {
        var pendingSessions = await _flightSessionStore
            .ListCompletedSessionsAsync(cancellationToken)
            .ConfigureAwait(false);

        var batch = maxSessions is > 0
            ? pendingSessions.Take(maxSessions.Value)
            : pendingSessions;

        var results = new List<PendingSessionSyncResult>();
        foreach (var session in batch)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await SyncSessionAsync(session, cancellationToken).ConfigureAwait(false);
            results.Add(result);
        }

        return new PendingSessionSyncSummary
        {
            Results = results,
        };
    }

    private async Task<PendingSessionSyncResult> SyncSessionAsync(
        PendingCompletedSession session,
        CancellationToken cancellationToken)
    {
        try
        {
            var upload = await _completedSessionUploader
                .UploadAsync(session, cancellationToken)
                .ConfigureAwait(false);

            var removed = false;
            if (upload.Status == SessionUploadStatus.Success)
            {
                removed = await _flightSessionStore
                    .RemoveCompletedSessionAsync(session.SessionId, cancellationToken)
                    .ConfigureAwait(false);
            }

            return new PendingSessionSyncResult
            {
                SessionId = session.SessionId,
                Status = upload.Status,
                StatusCode = upload.StatusCode,
                ErrorMessage = upload.ErrorMessage,
                RemovedFromQueue = removed,
                PostFlightStatus = upload.PostFlightStatus,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new PendingSessionSyncResult
            {
                SessionId = session.SessionId,
                Status = SessionUploadStatus.RetryableFailure,
                ErrorMessage = ex.Message,
            };
        }
    }
}
