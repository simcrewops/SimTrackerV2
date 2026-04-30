namespace SimCrewOps.Sync.Models;

public sealed record PendingSessionSyncResult
{
    public required string SessionId { get; init; }
    public required SessionUploadStatus Status { get; init; }
    public int? StatusCode { get; init; }
    public string? ErrorMessage { get; init; }
    public bool RemovedFromQueue { get; init; }

    /// <summary>
    /// Post-flight grounding status parsed from the 201 response body.
    /// Non-null when the server issued a strike for this session.
    /// </summary>
    public PostFlightStatus? PostFlightStatus { get; init; }
}
