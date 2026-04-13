using SimCrewOps.SimConnect.Models;

namespace SimCrewOps.SimConnect.Services;

public interface ISimConnectClient
{
    bool IsConnected { get; }
    Task OpenAsync(SimConnectHostOptions options, CancellationToken cancellationToken = default);
    Task CloseAsync(CancellationToken cancellationToken = default);
    Task<SimConnectRawTelemetryFrame?> ReadNextFrameAsync(CancellationToken cancellationToken = default);
}
