using System.Reflection;
using SimCrewOps.SimConnect.Models;
using SimCrewOps.SimConnect.Services;
using Xunit;

namespace SimCrewOps.SimConnect.Tests;

public sealed class ManagedSimConnectClientTests
{
    [Fact]
    public async Task OpenAsync_UsesBridgeFactoryAndReadsFrames()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var expectedFrame = new SimConnectRawTelemetryFrame
        {
            TimestampUtc = new DateTimeOffset(2026, 4, 13, 18, 0, 0, TimeSpan.Zero),
            Latitude = 25.79,
            Longitude = -80.29,
            IndicatedAirspeedKnots = 148,
        };

        var bridge = new StubBridge(expectedFrame);
        var locator = new StubAssemblyLocator(typeof(ManagedSimConnectClientTests).Assembly);
        var factory = new StubBridgeFactory(bridge);
        var client = new ManagedSimConnectClient(locator, factory);

        await client.OpenAsync(new SimConnectHostOptions());
        var frame = await client.ReadNextFrameAsync();

        Assert.True(client.IsConnected);
        Assert.Same(typeof(ManagedSimConnectClientTests).Assembly, factory.ReceivedAssembly);
        Assert.Equal(148, frame!.IndicatedAirspeedKnots);
    }

    [Fact]
    public async Task OpenAsync_ThrowsPlatformNotSupportedOnNonWindows()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var client = new ManagedSimConnectClient(
            new StubAssemblyLocator(typeof(ManagedSimConnectClientTests).Assembly),
            new StubBridgeFactory(new StubBridge(null)));

        await Assert.ThrowsAsync<PlatformNotSupportedException>(() => client.OpenAsync(new SimConnectHostOptions()));
    }

    [Fact]
    public async Task CloseAsync_ClosesBridgeAndResetsConnection()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var bridge = new StubBridge(null);
        var client = new ManagedSimConnectClient(
            new StubAssemblyLocator(typeof(ManagedSimConnectClientTests).Assembly),
            new StubBridgeFactory(bridge));

        await client.OpenAsync(new SimConnectHostOptions());
        await client.CloseAsync();

        Assert.False(client.IsConnected);
        Assert.True(bridge.CloseCalled);
    }

    [Fact]
    public async Task ReadNextFrameAsync_ThrowsWhenClientIsNotOpen()
    {
        var client = new ManagedSimConnectClient(
            new StubAssemblyLocator(typeof(ManagedSimConnectClientTests).Assembly),
            new StubBridgeFactory(new StubBridge(null)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.ReadNextFrameAsync());
    }

    private sealed class StubAssemblyLocator(Assembly assembly) : SimConnectAssemblyLocator
    {
        public override Assembly LoadManagedAssembly(SimConnectHostOptions options) => assembly;
    }

    private sealed class StubBridgeFactory(StubBridge bridge) : ISimConnectManagedBridgeFactory
    {
        public Assembly? ReceivedAssembly { get; private set; }

        public Task<ISimConnectManagedBridge> CreateAsync(Assembly managedAssembly, SimConnectHostOptions options, CancellationToken cancellationToken = default)
        {
            ReceivedAssembly = managedAssembly;
            return Task.FromResult<ISimConnectManagedBridge>(bridge);
        }
    }

    private sealed class StubBridge(SimConnectRawTelemetryFrame? nextFrame) : ISimConnectManagedBridge
    {
        public bool CloseCalled { get; private set; }
        public bool IsConnected { get; private set; } = true;

        public Task<SimConnectRawTelemetryFrame?> ReadNextFrameAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(nextFrame);

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            CloseCalled = true;
            IsConnected = false;
            return Task.CompletedTask;
        }
    }
}
