namespace SimCrewOps.Sync.Models;

public enum SessionUploadStatus
{
    Success,
    RetryableFailure,
    PermanentFailure,
}

public sealed record CompletedSessionUploadResult
{
    public required SessionUploadStatus Status { get; init; }
    public int? StatusCode { get; init; }
    public string? ErrorMessage { get; init; }
    public string? RemoteSessionId { get; init; }
}
