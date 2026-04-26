using SimCrewOps.SimConnect.Models;

namespace SimCrewOps.SimConnect.Services;

internal interface ISimConnectManagedBridge
{
    bool IsConnected { get; }
    Task<SimConnectRawTelemetryFrame?> ReadNextFrameAsync(CancellationToken cancellationToken = default);
    Task CloseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests runway facility data for the given airport through the live SimConnect
    /// connection. Returns null if the connection is not open or the request times out.
    /// </summary>
    Task<SimConnectAirportFacilitySnapshot?> RequestFacilityDataAsync(
        string airportIcao,
        CancellationToken cancellationToken = default);
}
