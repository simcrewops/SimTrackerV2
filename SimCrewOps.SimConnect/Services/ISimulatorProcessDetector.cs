using SimCrewOps.SimConnect.Models;

namespace SimCrewOps.SimConnect.Services;

public interface ISimulatorProcessDetector
{
    SimulatorProcessInfo? FindRunningSimulator(IReadOnlyList<string> processNames);
}
