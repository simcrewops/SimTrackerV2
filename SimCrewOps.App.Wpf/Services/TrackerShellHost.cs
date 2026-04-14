using System.IO;
using SimCrewOps.App.Wpf.Models;
using SimCrewOps.Hosting.Config;
using SimCrewOps.Hosting.Models;
using SimCrewOps.Persistence.Models;
using SimCrewOps.Persistence.Persistence;
using SimCrewOps.Runways.Providers;
using SimCrewOps.Runways.Services;
using SimCrewOps.Runtime.Models;
using SimCrewOps.Runtime.Runtime;
using SimCrewOps.SimConnect.Models;
using SimCrewOps.SimConnect.Services;
using SimCrewOps.Tracking.Models;

namespace SimCrewOps.App.Wpf.Services;

public sealed class TrackerShellHost : IAsyncDisposable
{
    private readonly ITrackerAppSettingsStore _settingsStore;
    private readonly string _settingsFilePath;
    private readonly TrackerServiceStack _serviceStack;
    private readonly MsfsSimConnectHost _simConnectHost;
    private readonly PersistentRuntimeCoordinator _persistentRuntimeCoordinator;

    private TrackerAppSettings _settings;
    private SessionRecoverySnapshot _recoverySnapshot = new();
    private FlightSessionRuntimeState? _runtimeState;

    public TrackerShellHost(
        ITrackerAppSettingsStore settingsStore,
        string settingsFilePath,
        TrackerServiceStack serviceStack)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsFilePath);
        ArgumentNullException.ThrowIfNull(serviceStack);

        _settingsStore = settingsStore;
        _settingsFilePath = settingsFilePath;
        _serviceStack = serviceStack;
        _settings = serviceStack.Settings;
        _simConnectHost = new MsfsSimConnectHost(
            new SimulatorProcessDetector(new SystemProcessListProvider()),
            new AdaptiveSimConnectClient());
        var runwayDataProvider = CreateRunwayDataProvider(settingsFilePath);
        var runtimeCoordinator = new RuntimeCoordinator(
            new FlightSessionContext(),
            new RunwayResolver(runwayDataProvider),
            livePositionUploader: serviceStack.LivePositionUploader);
        _persistentRuntimeCoordinator = new PersistentRuntimeCoordinator(
            runtimeCoordinator,
            serviceStack.FlightSessionStore);
    }

    public TrackerAppSettings Settings => _settings;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _recoverySnapshot = await _persistentRuntimeCoordinator
            .GetRecoverySnapshotAsync(cancellationToken)
            .ConfigureAwait(false);

        if (_serviceStack.BackgroundSyncCoordinator is not null)
        {
            _serviceStack.BackgroundSyncCoordinator.Start();

            try
            {
                await _serviceStack.BackgroundSyncCoordinator.RunStartupSyncAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Surface the error via the coordinator status rather than crashing startup.
            }
        }

        _recoverySnapshot = await _persistentRuntimeCoordinator
            .GetRecoverySnapshotAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public TrackerShellSnapshot GetSnapshot() => BuildSnapshot();

    public async Task<TrackerShellSnapshot> PollAsync(CancellationToken cancellationToken = default)
    {
        var simConnectPoll = await _simConnectHost.PollAsync(cancellationToken).ConfigureAwait(false);
        if (simConnectPoll.HasTelemetry)
        {
            var runtimeFrame = await _persistentRuntimeCoordinator
                .ProcessFrameAsync(simConnectPoll.TelemetryFrame!, cancellationToken)
                .ConfigureAwait(false);
            _runtimeState = runtimeFrame.RuntimeFrame.State;
            _recoverySnapshot = await _persistentRuntimeCoordinator
                .GetRecoverySnapshotAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return BuildSnapshot(simConnectPoll.Status);
    }

    public async Task SaveSettingsAsync(TrackerAppSettings settings, CancellationToken cancellationToken = default)
    {
        await _settingsStore.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
        _settings = settings;
    }

    public async Task DiscardRecoveryAsync(CancellationToken cancellationToken = default)
    {
        await _persistentRuntimeCoordinator.ClearCurrentSessionAsync(cancellationToken).ConfigureAwait(false);
        _runtimeState = null;
        _recoverySnapshot = await _persistentRuntimeCoordinator
            .GetRecoverySnapshotAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<TrackerShellSnapshot> ResumeRecoveryAsync(CancellationToken cancellationToken = default)
    {
        var recoveredState = _recoverySnapshot.CurrentSession?.State;
        if (recoveredState is null)
        {
            return BuildSnapshot();
        }

        _persistentRuntimeCoordinator.Restore(recoveredState);
        _runtimeState = recoveredState;
        _recoverySnapshot = await _persistentRuntimeCoordinator
            .GetRecoverySnapshotAsync(cancellationToken)
            .ConfigureAwait(false);

        return BuildSnapshot();
    }

    public async Task SyncNowAsync(CancellationToken cancellationToken = default)
    {
        if (_serviceStack.BackgroundSyncCoordinator is null)
        {
            return;
        }

        await _serviceStack.BackgroundSyncCoordinator.SyncNowAsync(cancellationToken).ConfigureAwait(false);
        _recoverySnapshot = await _persistentRuntimeCoordinator
            .GetRecoverySnapshotAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_serviceStack.BackgroundSyncCoordinator is not null)
        {
            await _serviceStack.BackgroundSyncCoordinator.DisposeAsync().ConfigureAwait(false);
        }

        await _simConnectHost.DisconnectAsync().ConfigureAwait(false);
    }

    private static IRunwayDataProvider CreateRunwayDataProvider(string settingsFilePath)
    {
        var providers = new List<IRunwayDataProvider>
        {
            new SimConnectFacilityRunwayProvider(),
        };

        foreach (var csvPath in GetFallbackCsvPaths(settingsFilePath))
        {
            if (!File.Exists(csvPath))
            {
                continue;
            }

            providers.Add(OurAirportsCsvRunwayDataProvider.FromFile(csvPath));
            break;
        }

        return providers.Count == 1
            ? providers[0]
            : new FallbackRunwayDataProvider(providers.ToArray());
    }

    private static IEnumerable<string> GetFallbackCsvPaths(string settingsFilePath)
    {
        var settingsDirectory = Path.GetDirectoryName(settingsFilePath);
        if (!string.IsNullOrWhiteSpace(settingsDirectory))
        {
            yield return Path.Combine(settingsDirectory, "ourairports-runways.csv");
            yield return Path.Combine(settingsDirectory, "data", "ourairports-runways.csv");
        }

        yield return Path.Combine(AppContext.BaseDirectory, "data", "ourairports-runways.csv");
    }

    private TrackerShellSnapshot BuildSnapshot(SimConnectHostStatus? simConnectStatus = null) =>
        new()
        {
            Settings = Settings,
            SettingsFilePath = _settingsFilePath,
            RecoverySnapshot = _recoverySnapshot,
            SimConnectStatus = simConnectStatus ?? _simConnectHost.Status,
            RuntimeState = _runtimeState,
            BackgroundSyncStatus = _serviceStack.BackgroundSyncCoordinator?.Status,
        };
}
