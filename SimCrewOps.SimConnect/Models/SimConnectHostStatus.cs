namespace SimCrewOps.SimConnect.Models;

public sealed record SimConnectHostStatus
{
    public SimConnectConnectionState ConnectionState { get; init; } = SimConnectConnectionState.Idle;
    public SimulatorProcessInfo? SimulatorProcess { get; init; }
    public DateTimeOffset? ConnectedUtc { get; init; }
    public DateTimeOffset? LastTelemetryUtc { get; init; }
    public string? LastErrorMessage { get; init; }
}
