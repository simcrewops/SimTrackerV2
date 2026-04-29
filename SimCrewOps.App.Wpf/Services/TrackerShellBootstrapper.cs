using System.IO;
using System.Reflection;
using SimCrewOps.Hosting.Config;
using SimCrewOps.Hosting.Hosting;
using SimCrewOps.Hosting.Models;

namespace SimCrewOps.App.Wpf.Services;

public static class TrackerShellBootstrapper
{
    // Logical names assigned in SimCrewOps.App.Wpf.csproj via <EmbeddedResource LogicalName="...">.
    private const string EmbeddedRunwayCsvResourceName      = "SimCrewOps.RunwayData.csv";
    private const string EmbeddedSimConnectResourceName     = "SimCrewOps.NativeSimConnect.dll";
    private const string EmbeddedMsfsSimConnectResourceName = "SimCrewOps.NativeMsfsSimConnect.dll";

    public static async Task<TrackerShellBootstrapResult> BootstrapAsync(CancellationToken cancellationToken = default)
    {
        var appRootDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SimCrewOps",
            "SimTrackerV2");

        Directory.CreateDirectory(appRootDirectory);

        // Extract native SimConnect DLLs and set SIMCONNECT_NATIVE_DLL_PATH so that
        // NativeSimConnectLibraryLocator can find them regardless of whether MSFS is installed.
        await ExtractBundledNativeDllsAsync(appRootDirectory, cancellationToken).ConfigureAwait(false);

        // Extract the bundled runway CSV on first launch (single-file publish embeds it).
        // The destination matches GetFallbackCsvPaths search order in TrackerShellHost.
        await ExtractBundledRunwayCsvAsync(appRootDirectory, cancellationToken).ConfigureAwait(false);

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

    /// <summary>
    /// Extracts SimConnect.dll and Microsoft.FlightSimulator.SimConnect.dll from embedded
    /// resources to {appRootDirectory}/native/, then sets the SIMCONNECT_NATIVE_DLL_PATH
    /// environment variable so NativeSimConnectLibraryLocator resolves them as a fallback
    /// when the DLLs are not on the system DLL search path (e.g. MSFS not installed).
    /// Always overwrites — ensures the bundled version stays current after updates.
    /// Does nothing per DLL when the matching embedded resource is absent.
    /// </summary>
    private static async Task ExtractBundledNativeDllsAsync(string appRootDirectory, CancellationToken cancellationToken)
    {
        var nativeDir = Path.Combine(appRootDirectory, "native");
        Directory.CreateDirectory(nativeDir);

        var assembly = Assembly.GetEntryAssembly() ?? typeof(TrackerShellBootstrapper).Assembly;

        var dlls = new[]
        {
            (Resource: EmbeddedSimConnectResourceName,     FileName: "SimConnect.dll"),
            (Resource: EmbeddedMsfsSimConnectResourceName, FileName: "Microsoft.FlightSimulator.SimConnect.dll"),
        };

        foreach (var (resourceName, fileName) in dlls)
        {
            using var resourceStream = assembly.GetManifestResourceStream(resourceName);
            if (resourceStream is null)
                continue; // Not embedded — dev build where DLL lives in bin dir or system PATH.

            var destination = Path.Combine(nativeDir, fileName);
            await using var fileStream = new FileStream(
                destination, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true);
            await resourceStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
        }

        // Point NativeSimConnectLibraryLocator at the extracted SimConnect.dll.
        // This is a fallback — NativeLibrary.TryLoad("SimConnect") tries the system search
        // path first, which picks up MSFS's own version when the sim is installed.
        Environment.SetEnvironmentVariable(
            "SIMCONNECT_NATIVE_DLL_PATH",
            Path.Combine(nativeDir, "SimConnect.dll"));
    }

    /// <summary>
    /// Extracts the embedded runway CSV to {appRootDirectory}/data/ourairports-runways.csv
    /// if the file does not already exist.  Does nothing when the embedded resource is absent
    /// (non-bundled dev builds that still use the file-system CSV).
    /// </summary>
    private static async Task ExtractBundledRunwayCsvAsync(string appRootDirectory, CancellationToken cancellationToken)
    {
        var destination = Path.Combine(appRootDirectory, "data", "ourairports-runways.csv");
        if (File.Exists(destination))
            return;

        var assembly = Assembly.GetEntryAssembly() ?? typeof(TrackerShellBootstrapper).Assembly;
        using var resourceStream = assembly.GetManifestResourceStream(EmbeddedRunwayCsvResourceName);
        if (resourceStream is null)
            return;  // No embedded resource — this is a non-bundled build; skip gracefully.

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await using var fileStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true);
        await resourceStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
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
