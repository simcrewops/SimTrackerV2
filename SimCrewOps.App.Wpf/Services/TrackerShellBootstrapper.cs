using System.IO;
using System.Reflection;
using SimCrewOps.Hosting.Config;
using SimCrewOps.Hosting.Hosting;
using SimCrewOps.Hosting.Models;

namespace SimCrewOps.App.Wpf.Services;

public static class TrackerShellBootstrapper
{
    public static async Task<TrackerShellBootstrapResult> BootstrapAsync(CancellationToken cancellationToken = default)
    {
        var appRootDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SimCrewOps",
            "SimTrackerV2");

        Directory.CreateDirectory(appRootDirectory);

        var settingsFilePath = Path.Combine(appRootDirectory, "settings.json");
        var settingsStore = new FileSystemTrackerAppSettingsStore(new FileSystemTrackerAppSettingsStoreOptions
        {
            SettingsFilePath = settingsFilePath,
        });

        var settings = await settingsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (settings is null)
        {
            settings = CreateDefaultSettings(appRootDirectory);
            await settingsStore.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
        }

        var serviceFactory = new TrackerServiceFactory();
        var serviceStack = serviceFactory.Create(settings);
        var shellHost = new TrackerShellHost(settingsStore, settingsFilePath, serviceStack);

        return new TrackerShellBootstrapResult
        {
            ShellHost = shellHost,
            SettingsStore = settingsStore,
            Settings = settings,
            SettingsFilePath = settingsFilePath,
        };
    }

    private static TrackerAppSettings CreateDefaultSettings(string appRootDirectory) =>
        new()
        {
            Storage = new TrackerStorageSettings
            {
                RootDirectory = Path.Combine(appRootDirectory, "data"),
            },
            Api = new TrackerApiSettings
            {
                BaseUri = new Uri("https://simcrewops.com", UriKind.Absolute),
                SimSessionsPath = "/api/sim-sessions",
                TrackerVersion = ResolveDefaultTrackerVersion(),
            },
            BackgroundSync = new BackgroundSyncSettings
            {
                Enabled = true,
                IntervalSeconds = 300,
                MaxSessionsPerPass = 10,
            },
            Debug = new TrackerDebugSettings
            {
                EnableTelemetryDiagnostics = false,
            },
        };

    private static string ResolveDefaultTrackerVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(TrackerShellBootstrapper).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion;
        }

        return assembly.GetName().Version?.ToString() ?? "2.0.0-alpha";
    }
}
