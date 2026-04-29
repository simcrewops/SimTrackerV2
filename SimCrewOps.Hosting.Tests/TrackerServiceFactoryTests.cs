using SimCrewOps.Hosting.Hosting;
using SimCrewOps.Hosting.Models;
using SimCrewOps.Sync.Sync;
using Xunit;

namespace SimCrewOps.Hosting.Tests;

public sealed class TrackerServiceFactoryTests
{
    [Fact]
    public void Create_WithTokenAndEnabledSync_BuildsFullServiceStack()
    {
        var factory = new TrackerServiceFactory(() => new HttpClient(new FakeHttpMessageHandler()));
        var settings = CreateSettings(pilotApiToken: "token-123", backgroundSyncEnabled: true);

        var stack = factory.Create(settings);

        Assert.NotNull(stack.FlightSessionStore);
        Assert.NotNull(stack.LivePositionUploader);
        Assert.NotNull(stack.CompletedSessionUploader);
        Assert.NotNull(stack.CompletedSessionSyncService);
        Assert.NotNull(stack.BackgroundSyncCoordinator);
        Assert.NotNull(stack.PreflightChecker);
        Assert.True(stack.SyncEnabled);
    }

    [Fact]
    public void Create_WithoutToken_DisablesSyncServices()
    {
        var factory = new TrackerServiceFactory(() => new HttpClient(new FakeHttpMessageHandler()));
        var settings = CreateSettings(pilotApiToken: null, backgroundSyncEnabled: true);

        var stack = factory.Create(settings);

        Assert.NotNull(stack.FlightSessionStore);
        Assert.Null(stack.LivePositionUploader);
        Assert.Null(stack.CompletedSessionUploader);
        Assert.Null(stack.CompletedSessionSyncService);
        Assert.Null(stack.BackgroundSyncCoordinator);
        Assert.Null(stack.PreflightChecker);
        Assert.False(stack.SyncEnabled);
    }

    [Fact]
    public void Create_WithSyncDisabled_DoesNotBuildBackgroundServices()
    {
        var factory = new TrackerServiceFactory(() => new HttpClient(new FakeHttpMessageHandler()));
        var settings = CreateSettings(pilotApiToken: "token-123", backgroundSyncEnabled: false);

        var stack = factory.Create(settings);

        Assert.NotNull(stack.FlightSessionStore);
        Assert.NotNull(stack.LivePositionUploader);
        Assert.Null(stack.CompletedSessionUploader);
        Assert.Null(stack.CompletedSessionSyncService);
        Assert.Null(stack.BackgroundSyncCoordinator);
        Assert.NotNull(stack.PreflightChecker);
        Assert.False(stack.SyncEnabled);
    }

    private static TrackerAppSettings CreateSettings(string? pilotApiToken, bool backgroundSyncEnabled) =>
        new()
        {
            Storage = new TrackerStorageSettings
            {
                RootDirectory = Path.Combine(Path.GetTempPath(), "simcrewops-test-data"),
            },
            Api = new TrackerApiSettings
            {
                PilotApiToken = pilotApiToken,
                TrackerVersion = "2.1.0",
            },
            BackgroundSync = new BackgroundSyncSettings
            {
                Enabled = backgroundSyncEnabled,
                IntervalSeconds = 60,
                MaxSessionsPerPass = 5,
            },
            Debug = new TrackerDebugSettings
            {
                EnableTelemetryDiagnostics = false,
            },
        };

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.Created));
    }
}
