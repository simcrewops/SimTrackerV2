using SimCrewOps.Hosting.Models;
using SimCrewOps.Persistence.Models;
using SimCrewOps.Runtime.Models;
using SimCrewOps.SimConnect.Models;
using SimCrewOps.Sync.Models;

namespace SimCrewOps.App.Wpf.Models;

public sealed record TrackerShellSnapshot
{
    public required TrackerAppSettings Settings { get; init; }
    public required string SettingsFilePath { get; init; }
    public required SessionRecoverySnapshot RecoverySnapshot { get; init; }
    public required SimConnectHostStatus SimConnectStatus { get; init; }
    public SimConnectRawTelemetryFrame? LastRawTelemetryFrame { get; init; }
    public FlightSessionRuntimeState? RuntimeState { get; init; }
    public BackgroundSyncStatus? BackgroundSyncStatus { get; init; }

    /// <summary>
    /// True when a valid API token is configured and live position uploading is active.
    /// </summary>
    public bool LivePositionEnabled { get; init; }

    /// <summary>
    /// UTC timestamp of the last live-position upload that the server accepted (HTTP 200).
    /// Null until the first successful upload this session.
    /// </summary>
    public DateTimeOffset? LivePositionLastUploadUtc { get; init; }

    /// <summary>
    /// The pilot's next assigned flight fetched from the SimCrewOps web app.
    /// Null when no token is configured, the fetch failed, or no flight is queued.
    /// </summary>
    public ActiveFlightResponse? ActiveFlight { get; init; }

    /// <summary>
    /// Set when a sim reposition (teleport / "Set on Ground") was detected during the
    /// last poll and the session was automatically reset. The UI should show a brief
    /// notification so the pilot knows the tracker started fresh.
    /// Null means no auto-reset this poll.
    /// </summary>
    public DateTimeOffset? AutoResetOccurredUtc { get; init; }

    /// <summary>
    /// Last preflight check result. Null until CheckPreflightAsync is called.
    /// IsGrounded == true means session start is blocked.
    /// </summary>
    public PreflightStatusResponse? PreflightStatus { get; init; }

    /// <summary>
    /// Server-authoritative career result returned by the 201 upload response.
    /// Null until a session completes and uploads successfully.
    /// </summary>
    public CareerResultDto? ServerCareerResult { get; init; }

    /// <summary>
    /// Post-flight status returned alongside career result. Non-null when an
    /// upload succeeds. IsGrounded == true means a new strike was applied.
    /// </summary>
    public PostFlightStatusDto? PostFlightStatus { get; init; }

    /// <summary>UTC timestamp of the most recent completed-session upload attempt this run.</summary>
    public DateTimeOffset? LastUploadAttemptUtc { get; init; }

    /// <summary>Result of the most recent completed-session upload attempt. Null until one is made.</summary>
    public CompletedSessionUploadResult? LastUploadResult { get; init; }

    /// <summary>True when the current session was resumed from a persisted recovery snapshot.</summary>
    public bool SessionWasResumed { get; init; }
}
