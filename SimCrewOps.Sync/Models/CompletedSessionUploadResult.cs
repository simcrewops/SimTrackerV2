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

    /// <summary>
    /// Post-flight status parsed from the 201 response body.
    /// Non-null when the server issued a strike for this flight.
    /// Null for non-success responses or when the body omits the field.
    /// </summary>
    public PostFlightStatus? PostFlightStatus { get; init; }
}
