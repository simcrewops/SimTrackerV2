using SimCrewOps.SimConnect.Models;

namespace SimCrewOps.SimConnect.Services;

public interface IProcessListProvider
{
    IReadOnlyList<SimulatorProcessInfo> GetProcesses();
}
