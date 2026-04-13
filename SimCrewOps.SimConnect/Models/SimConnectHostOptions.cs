using SimCrewOps.SimConnect.Services;

namespace SimCrewOps.SimConnect.Models;

public sealed record SimConnectHostOptions
{
    public string ClientName { get; init; } = "SimCrewOps Tracker";
    public IReadOnlyList<string> SimulatorProcessNames { get; init; } = SimConnectDefinitionCatalog.DefaultSimulatorProcessNames;
    public string? ManagedAssemblyPath { get; init; }
    public IReadOnlyList<string> ManagedAssemblySearchPaths { get; init; } = [];
    public TimeSpan FrameReadTimeout { get; init; } = TimeSpan.FromMilliseconds(250);
}
