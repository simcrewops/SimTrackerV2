using SimCrewOps.Tracking.Models;

namespace SimCrewOps.SimConnect.Models;

public sealed record SimConnectPollResult
{
    public required SimConnectHostStatus Status { get; init; }
    public SimConnectRawTelemetryFrame? RawFrame { get; init; }
    public TelemetryFrame? TelemetryFrame { get; init; }

    public bool HasTelemetry => TelemetryFrame is not null;
}
