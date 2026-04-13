using SimCrewOps.SimConnect.Models;

namespace SimCrewOps.SimConnect.Services;

public sealed class SimulatorProcessDetector : ISimulatorProcessDetector
{
    private readonly IProcessListProvider _processListProvider;

    public SimulatorProcessDetector(IProcessListProvider? processListProvider = null)
    {
        _processListProvider = processListProvider ?? new SystemProcessListProvider();
    }

    public SimulatorProcessInfo? FindRunningSimulator(IReadOnlyList<string> processNames)
    {
        ArgumentNullException.ThrowIfNull(processNames);

        var processes = _processListProvider.GetProcesses();
        foreach (var candidate in processNames)
        {
            var normalizedCandidate = NormalizeProcessName(candidate);
            var match = processes.FirstOrDefault(process =>
                string.Equals(NormalizeProcessName(process.ProcessName), normalizedCandidate, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static string NormalizeProcessName(string processName) =>
        processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName[..^4]
            : processName;
}
