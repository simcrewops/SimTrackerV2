using System.Diagnostics;
using SimCrewOps.SimConnect.Models;

namespace SimCrewOps.SimConnect.Services;

public sealed class SystemProcessListProvider : IProcessListProvider
{
    public IReadOnlyList<SimulatorProcessInfo> GetProcesses() =>
        Process.GetProcesses()
            .Select(process => new SimulatorProcessInfo
            {
                ProcessId = process.Id,
                ProcessName = process.ProcessName,
            })
            .ToArray();
}
