using SimCrewOps.Hosting.Models;
using SimCrewOps.Persistence.Models;
using SimCrewOps.Runtime.Models;
using SimCrewOps.SimConnect.Models;

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
}
