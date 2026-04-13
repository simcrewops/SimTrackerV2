namespace SimCrewOps.SimConnect.Models;

public sealed record SimulatorProcessInfo
{
    public required int ProcessId { get; init; }
    public required string ProcessName { get; init; }
}
