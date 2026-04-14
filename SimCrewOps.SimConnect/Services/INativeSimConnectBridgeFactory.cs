using SimCrewOps.SimConnect.Models;

namespace SimCrewOps.SimConnect.Services;

internal interface INativeSimConnectBridgeFactory
{
    Task<INativeSimConnectBridge> CreateAsync(
        nint nativeLibraryHandle,
        SimConnectHostOptions options,
        CancellationToken cancellationToken = default);
}
