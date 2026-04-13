using SimCrewOps.SimConnect.Models;
using SimCrewOps.SimConnect.Services;
using Xunit;

namespace SimCrewOps.SimConnect.Tests;

public sealed class SimulatorProcessDetectorTests
{
    [Fact]
    public void FindRunningSimulator_ReturnsPreferredKnownProcess()
    {
        var detector = new SimulatorProcessDetector(new StubProcessListProvider(
        [
            new SimulatorProcessInfo { ProcessId = 1, ProcessName = "FlightSimulator" },
            new SimulatorProcessInfo { ProcessId = 2, ProcessName = "OtherApp.exe" },
        ]));

        var process = detector.FindRunningSimulator(SimConnectDefinitionCatalog.DefaultSimulatorProcessNames);

        Assert.NotNull(process);
        Assert.Equal("FlightSimulator", process!.ProcessName);
    }

    [Fact]
    public void FindRunningSimulator_ReturnsNullWhenNoKnownProcessIsRunning()
    {
        var detector = new SimulatorProcessDetector(new StubProcessListProvider(
        [
            new SimulatorProcessInfo { ProcessId = 1, ProcessName = "OtherApp.exe" },
        ]));

        var process = detector.FindRunningSimulator(SimConnectDefinitionCatalog.DefaultSimulatorProcessNames);

        Assert.Null(process);
    }

    private sealed class StubProcessListProvider(IReadOnlyList<SimulatorProcessInfo> processes) : IProcessListProvider
    {
        public IReadOnlyList<SimulatorProcessInfo> GetProcesses() => processes;
    }
}
