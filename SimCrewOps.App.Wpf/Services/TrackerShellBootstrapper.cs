using System.IO;
using System.Reflection;
using SimCrewOps.Hosting.Config;
using SimCrewOps.Hosting.Hosting;
using SimCrewOps.Hosting.Models;

namespace SimCrewOps.App.Wpf.Services;

public static class TrackerShellBootstrapper
{
    private const string EmbeddedSimConnectResourceName = "SimCrewOps.NativeSimConnect.dll";
    private const string EmbeddedMsfsSimConnectResourceName = "SimCrewOps.NativeMsfsSimConnect.dll";

    public static async Task<TrackerShellBootstrapResult> BootstrapAsync(CancellationToken cancellationToken = default)
    {
        var appRootDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SimCrewOps",
            "SimTrackerV2");

        Directory.CreateDirectory(appRootDirectory);
        await ExtractBundledNativeDllsAsync(appRootDirectory, cancellationToken).ConfigureAwait(false);

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
            LiveMapService = serviceStack.LiveMapService,
        };
    }

    private static async Task ExtractBundledNativeDllsAsync(string appRootDirectory, CancellationToken cancellationToken)
    {
        var nativeDirectory = Path.Combine(appRootDirectory, "native");
        Directory.CreateDirectory(nativeDirectory);

        var assembly = Assembly.GetEntryAssembly() ?? typeof(TrackerShellBootstrapper).Assembly;
        var resources = new[]
        {
            (ResourceName: EmbeddedSimConnectResourceName, FileName: "SimConnect.dll"),
            (ResourceName: EmbeddedMsfsSimConnectResourceName, FileName: "Microsoft.FlightSimulator.SimConnect.dll"),
        };

        foreach (var (resourceName, fileName) in resources)
        {
            await using var resourceStream = assembly.GetManifestResourceStream(resourceName);
            if (resourceStream is null)
            {
                continue;
            }

            var destination = Path.Combine(nativeDirectory, fileName);
            await using var destinationStream = new FileStream(
                destination,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 65536,
                useAsync: true);

            await resourceStream.CopyToAsync(destinationStream, cancellationToken).ConfigureAwait(false);
        }

        Environment.SetEnvironmentVariable(
            "SIMCONNECT_NATIVE_DLL_PATH",
            Path.Combine(nativeDirectory, "SimConnect.dll"));
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

        return assembly.GetName().Version?.ToString() ?? "3.0.0-beta";
    }
}
