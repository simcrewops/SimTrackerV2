namespace SimCrewOps.Sync.Models;

public sealed record PendingSessionSyncResult
{
    public required string SessionId { get; init; }
    public required SessionUploadStatus Status { get; init; }
    public int? StatusCode { get; init; }
    public string? ErrorMessage { get; init; }
    public bool RemovedFromQueue { get; init; }
}
