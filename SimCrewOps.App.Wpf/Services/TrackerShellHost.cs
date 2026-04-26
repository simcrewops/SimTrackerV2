using System.IO;
using SimCrewOps.App.Wpf.Models;
using SimCrewOps.Hosting.Config;
using SimCrewOps.Hosting.Hosting;
using SimCrewOps.Hosting.Models;
using SimCrewOps.Persistence.Models;
using SimCrewOps.Persistence.Persistence;
using SimCrewOps.Runways.Providers;
using SimCrewOps.Runways.Services;
using SimCrewOps.Runtime.Models;
using SimCrewOps.Runtime.Runtime;
using SimCrewOps.SimConnect.Models;
using SimCrewOps.SimConnect.Services;
using SimCrewOps.Sync.Models;
using SimCrewOps.Sync.Sync;
using SimCrewOps.Tracking.Models;

namespace SimCrewOps.App.Wpf.Services;

public sealed class TrackerShellHost : IAsyncDisposable
{
    private readonly ITrackerAppSettingsStore _settingsStore;
    private readonly string _settingsFilePath;
    private readonly TrackerServiceStack _serviceStack;
    private readonly TrackerServiceFactory _serviceFactory;
    private readonly MsfsSimConnectHost _simConnectHost;
    private readonly PersistentRuntimeCoordinator _persistentRuntimeCoordinator;
    private readonly IRunwayDataProvider _runwayDataProvider;

    private TrackerAppSettings _settings;
    private SessionRecoverySnapshot _recoverySnapshot = new();
    private SimConnectRawTelemetryFrame? _lastRawTelemetryFrame;
    private FlightSessionRuntimeState? _runtimeState;
    private ActiveFlightResponse? _activeFlight;
    private DateTimeOffset _activeFlightFetchedUtc = DateTimeOffset.MinValue;
    private IActiveFlightFetcher? _activeFlightFetcher;
    private string? _lastDetectedAircraftTitle;
    private string? _lastGpsDestinationIdent;
    private DateTimeOffset? _lastKnownBlocksOffUtc;

    // Refresh the active flight from the API every minute while the app is running.
    private static readonly TimeSpan ActiveFlightRefreshInterval = TimeSpan.FromMinutes(1);

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
        _serviceFactory = new TrackerServiceFactory();
        _settings = serviceStack.Settings;
        _activeFlightFetcher = serviceStack.ActiveFlightFetcher;
        var managedSimConnectClient = new ManagedSimConnectClient();
        _simConnectHost = new MsfsSimConnectHost(
            new SimulatorProcessDetector(new SystemProcessListProvider()),
            new AdaptiveSimConnectClient(new NativeSimConnectClient(), managedSimConnectClient));
        _runwayDataProvider = CreateRunwayDataProvider(settingsFilePath, managedSimConnectClient);
        var runtimeCoordinator = new RuntimeCoordinator(
            new FlightSessionContext(),
            new RunwayResolver(_runwayDataProvider),
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
            // After any successful upload, immediately refresh the active flight so the
            // tracker picks up the next assignment without waiting for the poll interval.
            _serviceStack.BackgroundSyncCoordinator.OnSessionsUploadedAsync = RefreshActiveFlightAsync;

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

        // Fetch the pilot's next assigned flight from the web app to pre-populate
        // departure/arrival ICAO and flight number in the tracker UI.
        await RefreshActiveFlightAsync(cancellationToken).ConfigureAwait(false);
    }

    public TrackerShellSnapshot GetSnapshot() => BuildSnapshot();

    public async Task<TrackerShellSnapshot> PollAsync(CancellationToken cancellationToken = default)
    {
        // Refresh active flight info from the API every 5 minutes.
        if (DateTimeOffset.UtcNow - _activeFlightFetchedUtc > ActiveFlightRefreshInterval)
        {
            await RefreshActiveFlightAsync(cancellationToken).ConfigureAwait(false);
        }

        var simConnectPoll = await _simConnectHost.PollAsync(cancellationToken).ConfigureAwait(false);

        // When SimConnect detects a new aircraft title (e.g. "fenix-a319"), push it into
        // the session context so the live position beacon and session upload both record
        // the actual aircraft being flown rather than the bid aircraft type.
        var detectedTitle = simConnectPoll.Status.DetectedAircraftTitle;
        if (!string.IsNullOrWhiteSpace(detectedTitle) && detectedTitle != _lastDetectedAircraftTitle)
        {
            _lastDetectedAircraftTitle = detectedTitle;
            // Use UpdateAircraftType rather than UpdateContext so the SimConnect-detected
            // type always wins — UpdateContext is blocked after blocks-off, but the ATC
            // MODEL SimVar fires asynchronously and typically arrives a few seconds into
            // pushback, after blocks-off has already fired.
            _persistentRuntimeCoordinator.UpdateAircraftType(
                detectedTitle,
                ResolveAircraftCategory(detectedTitle));
        }

        // Auto-detect the arrival airport from the GPS flight plan destination.
        // This fills in ArrivalAirportIcao for free-flight sessions where no bid context
        // is set, enabling runway resolution and TDZ display after landing.
        // Only applies when the context has no arrival set (career flights keep their value).
        var gpsDest = simConnectPoll.RawFrame?.GpsDestinationIdent;
        if (!string.IsNullOrWhiteSpace(gpsDest) && gpsDest != _lastGpsDestinationIdent)
        {
            _lastGpsDestinationIdent = gpsDest;
            var ctx = _persistentRuntimeCoordinator.CurrentContext;
            if (string.IsNullOrWhiteSpace(ctx.ArrivalAirportIcao))
            {
                _persistentRuntimeCoordinator.UpdateContext(ctx with
                {
                    ArrivalAirportIcao = gpsDest,
                });
            }
        }

        if (simConnectPoll.HasTelemetry)
        {
            _lastRawTelemetryFrame = simConnectPoll.RawFrame;
            var runtimeFrame = await _persistentRuntimeCoordinator
                .ProcessFrameAsync(simConnectPoll.TelemetryFrame!, cancellationToken)
                .ConfigureAwait(false);
            _runtimeState = runtimeFrame.RuntimeFrame.State;
            _recoverySnapshot = await _persistentRuntimeCoordinator
                .GetRecoverySnapshotAsync(cancellationToken)
                .ConfigureAwait(false);

            // Trigger an immediate active-flight re-fetch the moment blocks-off fires.
            // The first few position beacons of every leg go out right after pushback —
            // without this they would carry the previous leg's context until the 1-minute
            // timer ticks.
            var newBlocksOff = _runtimeState.BlockTimes.BlocksOffUtc;
            if (newBlocksOff is not null && newBlocksOff != _lastKnownBlocksOffUtc)
            {
                _lastKnownBlocksOffUtc = newBlocksOff;
                _activeFlightFetchedUtc = DateTimeOffset.MinValue; // force refresh on next poll
            }
        }

        return BuildSnapshot(simConnectPoll.Status);
    }

    /// <summary>
    /// Fetches the pilot's next assigned flight from the API and pushes it into
    /// the runtime coordinator as the current session context.
    /// </summary>
    /// <summary>
    /// Immediately re-fetches the active flight from the API, bypassing the normal refresh interval.
    /// Call this after the pilot clicks "Fly Next" on the web app so the tracker picks up the
    /// new departure/arrival without waiting for the next scheduled refresh.
    /// </summary>
    public async Task ForceRefreshActiveFlightAsync(CancellationToken cancellationToken = default)
    {
        _activeFlightFetchedUtc = DateTimeOffset.MinValue;
        await RefreshActiveFlightAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RefreshActiveFlightAsync(CancellationToken cancellationToken = default)
    {
        if (_activeFlightFetcher is null)
            return;

        try
        {
            var flight = await _activeFlightFetcher
                .FetchAsync(cancellationToken)
                .ConfigureAwait(false);

            _activeFlight = flight;
            _activeFlightFetchedUtc = DateTimeOffset.UtcNow;

            // Build a FlightSessionContext from the fetched data and push it into
            // the runtime coordinator so departure/arrival ICAO are used for runway
            // resolution and live position uploads.
            var context = flight is not null
                ? new FlightSessionContext
                {
                    DepartureAirportIcao = flight.Departure,
                    ArrivalAirportIcao   = flight.Arrival,
                    FlightMode           = "career",
                    FlightNumber         = flight.FlightNumber,
                    AircraftType         = flight.AircraftType,
                    AircraftCategory     = ResolveAircraftCategory(flight.AircraftType),
                    BidId                = string.IsNullOrWhiteSpace(flight.BidId) ? null : flight.BidId,
                    ScheduledBlockHours  = flight.ScheduledBlockHours,
                }
                : new FlightSessionContext();

            _persistentRuntimeCoordinator.UpdateContext(context);

            // UpdateContext just pushed the bid aircraft type ("E75L") into the coordinator,
            // overwriting any SimConnect-confirmed type ("A319").  The poll loop will NOT
            // re-call UpdateAircraftType because _lastDetectedAircraftTitle hasn't changed.
            // Re-apply the SimConnect detection now so the live ping always uses what MSFS
            // actually has loaded, not what the schedule says.
            if (!string.IsNullOrWhiteSpace(_lastDetectedAircraftTitle))
            {
                _persistentRuntimeCoordinator.UpdateAircraftType(
                    _lastDetectedAircraftTitle,
                    ResolveAircraftCategory(_lastDetectedAircraftTitle));
            }

            if (flight is not null)
            {
                _ = PreWarmRunwayCacheAsync(flight.Departure, flight.Arrival, cancellationToken);
            }
        }
        catch
        {
            // Swallow — active flight is optional; don't crash the tracker over it.
        }
    }

    private async Task PreWarmRunwayCacheAsync(
        string? departureIcao,
        string? arrivalIcao,
        CancellationToken cancellationToken)
    {
        var tasks = new List<Task>(2);
        if (!string.IsNullOrWhiteSpace(departureIcao))
            tasks.Add(_runwayDataProvider.GetRunwaysAsync(departureIcao, cancellationToken));
        if (!string.IsNullOrWhiteSpace(arrivalIcao))
            tasks.Add(_runwayDataProvider.GetRunwaysAsync(arrivalIcao, cancellationToken));

        try { await Task.WhenAll(tasks).ConfigureAwait(false); }
        catch { /* pre-warm is best-effort */ }
    }

    public async Task SaveSettingsAsync(TrackerAppSettings settings, CancellationToken cancellationToken = default)
    {
        await _settingsStore.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
        _settings = settings;

        // Hot-reload the live position uploader so a newly-entered API token takes effect
        // immediately without requiring an app restart.
        var newUploader = _serviceFactory.CreateLivePositionUploader(settings.Api);
        _persistentRuntimeCoordinator.UpdateLivePositionUploader(newUploader);

        // Hot-reload the active flight fetcher and immediately pull the latest flight info.
        // This means a user who just pasted their API token sees their flight assignment
        // right away without having to restart the app.
        _activeFlightFetcher = _serviceFactory.CreateActiveFlightFetcher(settings.Api);
        _activeFlightFetchedUtc = DateTimeOffset.MinValue; // force refresh on next poll
        await RefreshActiveFlightAsync(cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// Maps an aircraft type string (e.g. "B738", "A320", "CRJ9") to the world map
    /// category expected by the frontend: "regional", "narrowbody", or "widebody".
    /// </summary>
    private static string ResolveAircraftCategory(string? aircraftType)
    {
        if (string.IsNullOrWhiteSpace(aircraftType))
            return "narrowbody";

        var t = aircraftType.ToUpperInvariant();

        // Widebody types
        if (t.StartsWith("B74") || t.StartsWith("B77") || t.StartsWith("B78") ||
            t.StartsWith("A33") || t.StartsWith("A34") || t.StartsWith("A35") ||
            t.StartsWith("A38") || t.StartsWith("B76") || t == "A300" || t == "A310")
            return "widebody";

        // Regional jets and turboprops
        if (t.StartsWith("CRJ") || t.StartsWith("E17") || t.StartsWith("E19") ||
            t.StartsWith("AT") || t.StartsWith("DH8") || t.StartsWith("SF3") ||
            t.StartsWith("E14") || t == "E145" || t == "E135" || t.StartsWith("RJ") ||
            t == "E75L" || t == "E75S")  // Embraer 175 ICAO type codes
            return "regional";

        return "narrowbody";
    }

    private static IRunwayDataProvider CreateRunwayDataProvider(
        string settingsFilePath,
        ManagedSimConnectClient managedSimConnectClient)
    {
        var providers = new List<IRunwayDataProvider>
        {
            SimConnectFacilityRunwayProvider.ForLiveConnection(managedSimConnectClient),
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
            LastRawTelemetryFrame = _lastRawTelemetryFrame,
            RuntimeState = _runtimeState,
            BackgroundSyncStatus = _serviceStack.BackgroundSyncCoordinator?.Status,
            LivePositionEnabled = !string.IsNullOrWhiteSpace(Settings.Api.PilotApiToken),
            LivePositionLastUploadUtc = _persistentRuntimeCoordinator.LastSuccessfulUploadUtc,
            ActiveFlight = _activeFlight,
        };
}
