namespace SimCrewOps.Sync.Models;

public sealed record PendingSessionSyncSummary
{
    public required IReadOnlyList<PendingSessionSyncResult> Results { get; init; }

    public int AttemptedCount => Results.Count;
    public int SucceededCount => Results.Count(result => result.Status == SessionUploadStatus.Success);
    public int RetryableFailureCount => Results.Count(result => result.Status == SessionUploadStatus.RetryableFailure);
    public int PermanentFailureCount => Results.Count(result => result.Status == SessionUploadStatus.PermanentFailure);
    public int FailedCount => RetryableFailureCount + PermanentFailureCount;

    /// <summary>
    /// The most recent post-flight grounding status from a successfully uploaded session
    /// in this batch. Null when no session in this batch returned a post-flight status.
    /// </summary>
    public PostFlightStatusDto? LastPostFlightStatus =>
        Results
            .LastOrDefault(r => r.Status == SessionUploadStatus.Success && r.PostFlightStatus is not null)
            ?.PostFlightStatus;
}
