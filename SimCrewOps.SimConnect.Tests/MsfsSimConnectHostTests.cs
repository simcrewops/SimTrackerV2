using SimCrewOps.SimConnect.Models;
using SimCrewOps.SimConnect.Services;
using Xunit;

namespace SimCrewOps.SimConnect.Tests;

public sealed class MsfsSimConnectHostTests
{
    [Fact]
    public async Task PollAsync_WaitsWhenNoSimulatorProcessIsRunning()
    {
        var host = new MsfsSimConnectHost(
            new StubProcessDetector(null),
            new StubSimConnectClient());

        var result = await host.PollAsync();

        Assert.Equal(SimConnectConnectionState.WaitingForSimulatorProcess, result.Status.ConnectionState);
        Assert.False(result.HasTelemetry);
    }

    [Fact]
    public async Task PollAsync_ConnectsAndMapsTelemetry()
    {
        var client = new StubSimConnectClient
        {
            NextFrame = new SimConnectRawTelemetryFrame
            {
                TimestampUtc = new DateTimeOffset(2026, 4, 13, 16, 0, 0, TimeSpan.Zero),
                Latitude = 40.64,
                Longitude = -73.78,
                AltitudeAglFeet = 35,
                AltitudeFeet = 48,
                IndicatedAltitudeFeet = 48,
                IndicatedAirspeedKnots = 132,
                TrueAirspeedKnots = 138,
                Mach = 0.2,
                GroundSpeedKnots = 138,
                VerticalSpeedFpm = -180,
                BankAngleDegrees = 1.2,
                PitchAngleDegrees = 2.3,
                HeadingMagneticDegrees = 41,
                HeadingTrueDegrees = 44,
                GForce = 1.22,
                OnGround = 1,
                ParkingBrakePosition = 0,
                FlapsHandleIndex = 3,
                GearPosition = 1,
                Engine1Running = 1,
                Engine2Running = 1,
                HasFlightCriticalData = true,
                HasOperationalData = true,
            },
        };

        var host = new MsfsSimConnectHost(
            new StubProcessDetector(new SimulatorProcessInfo
            {
                ProcessId = 42,
                ProcessName = "FlightSimulator2024.exe",
            }),
            client);

        var result = await host.PollAsync();

        Assert.True(client.OpenCalled);
        Assert.Equal(SimConnectConnectionState.Connected, result.Status.ConnectionState);
        Assert.True(result.HasTelemetry);
        Assert.Equal(132, result.TelemetryFrame!.IndicatedAirspeedKnots);
        Assert.Equal(result.RawFrame!.TimestampUtc, result.Status.LastTelemetryUtc);
        Assert.Equal(1, result.Status.PollCount);
        Assert.Equal(1, result.Status.RawFrameCount);
        Assert.Equal(1, result.Status.TelemetryFrameCount);
        Assert.True(result.Status.HasReceivedFlightCriticalData);
        Assert.True(result.Status.HasReceivedOperationalData);
    }

    [Fact]
    public async Task PollAsync_FaultsAndClosesWhenClientReadFails()
    {
        var client = new StubSimConnectClient
        {
            ThrowOnRead = new InvalidOperationException("read failed"),
        };

        var host = new MsfsSimConnectHost(
            new StubProcessDetector(new SimulatorProcessInfo
            {
                ProcessId = 42,
                ProcessName = "FlightSimulator2024.exe",
            }),
            client);

        var result = await host.PollAsync();

        Assert.Equal(SimConnectConnectionState.Faulted, result.Status.ConnectionState);
        Assert.Equal("read failed", result.Status.LastErrorMessage);
        Assert.Equal(1, result.Status.PollCount);
        Assert.True(client.CloseCalled);
    }

    [Fact]
    public void DefinitionCatalog_ContainsExpectedProcessNamesAndCoreVars()
    {
        Assert.Contains("FlightSimulator2024.exe", SimConnectDefinitionCatalog.DefaultSimulatorProcessNames);
        Assert.Contains(SimConnectDefinitionCatalog.FlightCriticalVariables, variable => variable.SimVarName == "PLANE LATITUDE");
        Assert.Contains(SimConnectDefinitionCatalog.FlightCriticalVariables, variable => variable.SimVarName == "PLANE ALT ABOVE GROUND LEVEL");
        Assert.Contains(SimConnectDefinitionCatalog.FlightCriticalVariables, variable => variable.SimVarName == "PLANE ALTITUDE");
        Assert.Contains(SimConnectDefinitionCatalog.ScoringAndOperationalVariables, variable => variable.SimVarName == "PLANE HEADING DEGREES MAGNETIC");
        Assert.Contains(SimConnectDefinitionCatalog.ScoringAndOperationalVariables, variable => variable.SimVarName == "LIGHT STATES");
        Assert.Contains(SimConnectDefinitionCatalog.ScoringAndOperationalVariables, variable => variable.SimVarName == "LIGHT BEACON");
        Assert.Contains(SimConnectDefinitionCatalog.ScoringAndOperationalVariables, variable => variable.SimVarName == "ENG COMBUSTION:1");
    }

    private sealed class StubProcessDetector(SimulatorProcessInfo? process) : ISimulatorProcessDetector
    {
        public SimulatorProcessInfo? FindRunningSimulator(IReadOnlyList<string> processNames) => process;
    }

    private sealed class StubSimConnectClient : ISimConnectClient
    {
        public bool IsConnected { get; private set; }
        public bool OpenCalled { get; private set; }
        public bool CloseCalled { get; private set; }
        public SimConnectRawTelemetryFrame? NextFrame { get; init; }
        public Exception? ThrowOnRead { get; init; }

        public Task OpenAsync(SimConnectHostOptions options, CancellationToken cancellationToken = default)
        {
            OpenCalled = true;
            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            CloseCalled = true;
            IsConnected = false;
            return Task.CompletedTask;
        }

        public Task<SimConnectRawTelemetryFrame?> ReadNextFrameAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowOnRead is not null)
            {
                throw ThrowOnRead;
            }

            return Task.FromResult(NextFrame);
        }
    }
}
