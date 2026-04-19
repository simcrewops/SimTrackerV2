namespace SimCrewOps.SimConnect.Models;

public sealed record SimConnectHostStatus
{
    public SimConnectConnectionState ConnectionState { get; init; } = SimConnectConnectionState.Idle;
    public SimulatorProcessInfo? SimulatorProcess { get; init; }
    public DateTimeOffset? ConnectedUtc { get; init; }
    public DateTimeOffset? LastRawFrameUtc { get; init; }
    public DateTimeOffset? LastTelemetryUtc { get; init; }
    public string? LastErrorMessage { get; init; }
    public string ClientPath { get; init; } = "SimConnect client unknown";
    public int PollCount { get; init; }
    public int NullPollCount { get; init; }
    public int RawFrameCount { get; init; }
    public int TelemetryFrameCount { get; init; }
    public bool HasReceivedFlightCriticalData { get; init; }
    public bool HasReceivedOperationalData { get; init; }

    /// <summary>
    /// Friendly aircraft name parsed from the MSFS .air file path reported by AircraftLoaded.
    /// e.g. "Community\fenix-a319\..." → "fenix-a319". Null until an aircraft is loaded.
    /// </summary>
    public string? DetectedAircraftTitle { get; init; }
}
