using SimCrewOps.Hosting.Config;
using SimCrewOps.Hosting.Models;
using Xunit;

namespace SimCrewOps.Hosting.Tests;

public sealed class FileSystemTrackerAppSettingsStoreTests : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(
        Path.GetTempPath(),
        "simcrewops-hosting-tests",
        Guid.NewGuid().ToString("n"));

    [Fact]
    public async Task SaveAndLoadAsync_RoundTripsSettings()
    {
        var store = new FileSystemTrackerAppSettingsStore(new FileSystemTrackerAppSettingsStoreOptions
        {
            SettingsFilePath = Path.Combine(_rootDirectory, "settings.json"),
        });

        var settings = new TrackerAppSettings
        {
            Storage = new TrackerStorageSettings
            {
                RootDirectory = Path.Combine(_rootDirectory, "data"),
            },
            Api = new TrackerApiSettings
            {
                BaseUri = new Uri("https://simcrewops.com"),
                SimSessionsPath = "/api/sim-sessions",
                PilotApiToken = "token-123",
                TrackerVersion = "2.1.0",
            },
            BackgroundSync = new BackgroundSyncSettings
            {
                Enabled = true,
                IntervalSeconds = 120,
                MaxSessionsPerPass = 10,
            },
            Debug = new TrackerDebugSettings
            {
                EnableTelemetryDiagnostics = true,
            },
        };

        await store.SaveAsync(settings);
        var loaded = await store.LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal(settings.Storage.RootDirectory, loaded!.Storage.RootDirectory);
        Assert.Equal(settings.Api.BaseUri, loaded.Api.BaseUri);
        Assert.Equal(settings.Api.PilotApiToken, loaded.Api.PilotApiToken);
        Assert.Equal(settings.Api.TrackerVersion, loaded.Api.TrackerVersion);
        Assert.Equal(settings.BackgroundSync.IntervalSeconds, loaded.BackgroundSync.IntervalSeconds);
        Assert.Equal(settings.BackgroundSync.MaxSessionsPerPass, loaded.BackgroundSync.MaxSessionsPerPass);
        Assert.Equal(settings.Debug.EnableTelemetryDiagnostics, loaded.Debug.EnableTelemetryDiagnostics);
    }

    [Fact]
    public async Task LoadAsync_ReturnsNullWhenSettingsFileDoesNotExist()
    {
        var store = new FileSystemTrackerAppSettingsStore(new FileSystemTrackerAppSettingsStoreOptions
        {
            SettingsFilePath = Path.Combine(_rootDirectory, "missing.json"),
        });

        var loaded = await store.LoadAsync();
        Assert.Null(loaded);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }
}
