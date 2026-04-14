using SimCrewOps.SimConnect.Models;
using SimCrewOps.SimConnect.Services;
using Xunit;

namespace SimCrewOps.SimConnect.Tests;

public sealed class AdaptiveSimConnectClientTests
{
    [Fact]
    public async Task OpenAsync_UsesPrimaryClientWhenItSucceeds()
    {
        var primary = new StubClient();
        var fallback = new StubClient();
        var client = new AdaptiveSimConnectClient(primary, fallback);

        await client.OpenAsync(new SimConnectHostOptions());

        Assert.True(primary.OpenCalled);
        Assert.False(fallback.OpenCalled);
        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task OpenAsync_FallsBackWhenPrimaryFails()
    {
        var primary = new StubClient(openException: new DllNotFoundException("missing"));
        var fallback = new StubClient();
        var client = new AdaptiveSimConnectClient(primary, fallback);

        await client.OpenAsync(new SimConnectHostOptions());

        Assert.True(primary.OpenCalled);
        Assert.True(fallback.OpenCalled);
        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task OpenAsync_ThrowsWhenBothClientsFail()
    {
        var client = new AdaptiveSimConnectClient(
            new StubClient(openException: new DllNotFoundException("native")),
            new StubClient(openException: new InvalidOperationException("managed")));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.OpenAsync(new SimConnectHostOptions()));

        Assert.Contains("Unable to open SimConnect", error.Message);
    }

    private sealed class StubClient(Exception? openException = null) : ISimConnectClient
    {
        public bool IsConnected { get; private set; }
        public bool OpenCalled { get; private set; }

        public Task OpenAsync(SimConnectHostOptions options, CancellationToken cancellationToken = default)
        {
            OpenCalled = true;
            if (openException is not null)
            {
                throw openException;
            }

            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            IsConnected = false;
            return Task.CompletedTask;
        }

        public Task<SimConnectRawTelemetryFrame?> ReadNextFrameAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<SimConnectRawTelemetryFrame?>(null);
    }
}
