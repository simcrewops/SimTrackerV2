using SimCrewOps.SimConnect.Models;
using SimCrewOps.SimConnect.Services;
using Xunit;

namespace SimCrewOps.SimConnect.Tests;

public sealed class NativeSimConnectClientTests
{
    [Fact]
    public void IsNoDispatchAvailable_TreatsEFailAsEmptyQueue()
    {
        Assert.True(NativeSimConnectBridge.IsNoDispatchAvailable(unchecked((int)0x80004005)));
        Assert.False(NativeSimConnectBridge.IsNoDispatchAvailable(0));
    }

    [Fact]
    public void LightStateDecoder_DecodesOperationalBitMask()
    {
        const int lightStates = 0x0002 | 0x0004 | 0x0010;

        Assert.True(SimConnectLightStateDecoder.IsBeaconOn(lightStates));
        Assert.False(SimConnectLightStateDecoder.IsTaxiOn(lightStates));
        Assert.True(SimConnectLightStateDecoder.IsLandingOn(lightStates));
        Assert.True(SimConnectLightStateDecoder.IsStrobeOn(lightStates));
    }

    [Fact]
    public void NormalizeValueType_UsesInt32ForBoolAndMaskDefinitions()
    {
        var boolDefinition = new SimConnectVariableDefinition
        {
            Key = "landing_light",
            SimVarName = "LIGHT LANDING",
            Unit = "bool",
            UpdateRate = SimConnectUpdateRate.Second,
            ValueType = SimConnectValueType.Float64,
            RequiredForScoring = true,
        };

        var maskDefinition = new SimConnectVariableDefinition
        {
            Key = "light_states",
            SimVarName = "LIGHT STATES",
            Unit = "Mask",
            UpdateRate = SimConnectUpdateRate.Second,
            ValueType = SimConnectValueType.Int32,
            RequiredForScoring = true,
        };

        Assert.Equal(SimConnectDataType.Int32, NativeSimConnectBridge.NormalizeValueType(boolDefinition));
        Assert.Equal(SimConnectDataType.Int32, NativeSimConnectBridge.NormalizeValueType(maskDefinition));
    }

    [Fact]
    public async Task OpenAsync_UsesBridgeFactoryAndReadsFrames()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var expectedFrame = new SimConnectRawTelemetryFrame
        {
            TimestampUtc = new DateTimeOffset(2026, 4, 13, 20, 0, 0, TimeSpan.Zero),
            Latitude = 25.79,
            Longitude = -80.29,
            IndicatedAirspeedKnots = 148,
        };

        var bridge = new StubBridge(expectedFrame);
        var locator = new StubNativeLibraryLocator(1234);
        var factory = new StubBridgeFactory(bridge);
        var client = new NativeSimConnectClient(locator, factory);

        await client.OpenAsync(new SimConnectHostOptions());
        var frame = await client.ReadNextFrameAsync();

        Assert.True(client.IsConnected);
        Assert.Equal((nint)1234, factory.ReceivedLibraryHandle);
        Assert.Equal(148, frame!.IndicatedAirspeedKnots);
    }

    [Fact]
    public async Task OpenAsync_ThrowsPlatformNotSupportedOnNonWindows()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var client = new NativeSimConnectClient(
            new StubNativeLibraryLocator(1234),
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
        var client = new NativeSimConnectClient(
            new StubNativeLibraryLocator(1234),
            new StubBridgeFactory(bridge));

        await client.OpenAsync(new SimConnectHostOptions());
        await client.CloseAsync();

        Assert.False(client.IsConnected);
        Assert.True(bridge.CloseCalled);
    }

    private sealed class StubNativeLibraryLocator(nint libraryHandle) : NativeSimConnectLibraryLocator
    {
        public override nint LoadNativeLibrary(SimConnectHostOptions options) => libraryHandle;
    }

    private sealed class StubBridgeFactory(StubBridge bridge) : INativeSimConnectBridgeFactory
    {
        public nint ReceivedLibraryHandle { get; private set; }

        public Task<INativeSimConnectBridge> CreateAsync(
            nint nativeLibraryHandle,
            SimConnectHostOptions options,
            CancellationToken cancellationToken = default)
        {
            ReceivedLibraryHandle = nativeLibraryHandle;
            return Task.FromResult<INativeSimConnectBridge>(bridge);
        }
    }

    private sealed class StubBridge(SimConnectRawTelemetryFrame? nextFrame) : INativeSimConnectBridge
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
