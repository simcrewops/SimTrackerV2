using SimCrewOps.Sync.Models;

namespace SimCrewOps.Hosting.Models;

public sealed record BackgroundSyncStatus
{
    public bool Enabled { get; init; }
    public bool IsRunning { get; init; }
    public string? LastTrigger { get; init; }
    public DateTimeOffset? LastRunStartedUtc { get; init; }
    public DateTimeOffset? LastRunCompletedUtc { get; init; }
    public PendingSessionSyncSummary? LastSummary { get; init; }
    public string? LastErrorMessage { get; init; }
    public int ConsecutiveFailureCount { get; init; }
}
