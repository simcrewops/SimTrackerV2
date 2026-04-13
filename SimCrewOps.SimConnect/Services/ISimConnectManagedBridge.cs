using SimCrewOps.SimConnect.Models;

namespace SimCrewOps.SimConnect.Services;

internal interface ISimConnectManagedBridge
{
    bool IsConnected { get; }
    Task<SimConnectRawTelemetryFrame?> ReadNextFrameAsync(CancellationToken cancellationToken = default);
    Task CloseAsync(CancellationToken cancellationToken = default);
}
