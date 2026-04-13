using System.Reflection;
using SimCrewOps.SimConnect.Models;

namespace SimCrewOps.SimConnect.Services;

internal interface ISimConnectManagedBridgeFactory
{
    Task<ISimConnectManagedBridge> CreateAsync(Assembly managedAssembly, SimConnectHostOptions options, CancellationToken cancellationToken = default);
}
