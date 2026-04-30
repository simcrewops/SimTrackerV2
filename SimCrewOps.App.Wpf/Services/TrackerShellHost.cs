using SimCrewOps.App.Wpf.Models;
using SimCrewOps.Hosting.Config;
using SimCrewOps.Hosting.Hosting;
using SimCrewOps.Hosting.Models;
using SimCrewOps.Persistence.Models;
using SimCrewOps.Persistence.Persistence;
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
    private TrackerServiceStack _serviceStack;
    private readonly TrackerServiceFactory _serviceFactory;
    private readonly MsfsSimConnectHost _simConnectHost;
    private readonly PersistentRuntimeCoordinator _persistentRuntimeCoordinator;

    private TrackerAppSettings _settings;
    private SessionRecoverySnapshot _recoverySnapshot = new();
    private SimConnectRawTelemetryFrame? _lastRawTelemetryFrame;
    private FlightSessionRuntimeState? _runtimeState;
    private ActiveFlightResponse? _activeFlight;
    private DateTimeOffset _activeFlightFetchedUtc = DateTimeOffset.MinValue;
    private IActiveFlightFetcher? _activeFlightFetcher;
    private PreflightStatusResponse? _preflightStatus;
    private CareerResultDto? _serverCareerResult;
    private PostFlightStatusDto? _postFlightStatus;
    private DateTimeOffset? _lastUploadAttemptUtc;
    private CompletedSessionUploadResult? _lastUploadResult;
    private bool _sessionWasResumed;

    // Refresh the active flight from the API every 5 minutes while the app is running.
    private static readonly TimeSpan ActiveFlightRefreshInterval = TimeSpan.FromMinutes(5);

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
        _simConnectHost = new MsfsSimConnectHost(
            new SimulatorProcessDetector(new SystemProcessListProvider()),
            new AdaptiveSimConnectClient());
        var runtimeCoordinator = new RuntimeCoordinator(
            new FlightSessionContext(),
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

        DateTimeOffset? autoResetUtc = null;

        var simConnectPoll = await _simConnectHost.PollAsync(cancellationToken).ConfigureAwait(false);
        if (simConnectPoll.HasTelemetry)
        {
            _lastRawTelemetryFrame = simConnectPoll.RawFrame;

            // Grounded pilots must not accumulate a trackable session. Raw telemetry is
            // still received (SimConnect stays live for display) but we do not feed frames
            // into scoring or persistence while the grounded block is active.
            if (_preflightStatus?.IsGrounded == true)
            {
                return BuildSnapshot(simConnectPoll.Status, autoResetUtc);
            }

            var runtimeFrame = await _persistentRuntimeCoordinator
                .ProcessFrameAsync(simConnectPoll.TelemetryFrame!, cancellationToken)
                .ConfigureAwait(false);
            _runtimeState = runtimeFrame.RuntimeFrame.State;

            if (runtimeFrame.WasRepositionReset)
            {
                autoResetUtc = DateTimeOffset.UtcNow;
                _runtimeState = null; // snapshot shows clean state after reset
            }

            _recoverySnapshot = await _persistentRuntimeCoordinator
                .GetRecoverySnapshotAsync(cancellationToken)
                .ConfigureAwait(false);

            // Immediately upload on session completion; background sync handles retry on failure.
            var queued = runtimeFrame.Persistence.QueuedCompletedSession;
            if (queued is not null && _serviceStack.CompletedSessionUploader is not null)
            {
                try
                {
                    _lastUploadAttemptUtc = DateTimeOffset.UtcNow;
                    var uploadResult = await _serviceStack.CompletedSessionUploader
                        .UploadAsync(queued, cancellationToken)
                        .ConfigureAwait(false);

                    _lastUploadResult = uploadResult;

                    if (uploadResult.Status == SessionUploadStatus.Success)
                    {
                        await _serviceStack.FlightSessionStore
                            .RemoveCompletedSessionAsync(queued.SessionId, cancellationToken)
                            .ConfigureAwait(false);
                        _serverCareerResult = uploadResult.CareerResult;
                        _postFlightStatus = uploadResult.PostFlightStatus;
                    }
                }
                catch
                {
                    // Leave in disk queue; BackgroundSyncCoordinator will retry.
                }
            }
        }

        return BuildSnapshot(simConnectPoll.Status, autoResetUtc);
    }

    /// <summary>
    /// Calls the preflight API to check whether the pilot is grounded.
    /// Returns null when no token is configured or the request fails.
    /// IsGrounded == true blocks session start.
    /// </summary>
    public async Task<PreflightStatusResponse?> CheckPreflightAsync(CancellationToken cancellationToken = default)
    {
        if (_serviceStack.PreflightChecker is null)
            return null;

        var status = await _serviceStack.PreflightChecker
            .CheckAsync(cancellationToken)
            .ConfigureAwait(false);

        _preflightStatus = status;
        return status;
    }

    /// <summary>
    /// Manually resets the current flight session to Preflight state, keeping the
    /// flight context (departure, arrival, etc.) intact. Used by the Reset button.
    /// </summary>
    public async Task<TrackerShellSnapshot> ResetSessionAsync(CancellationToken cancellationToken = default)
    {
        await _persistentRuntimeCoordinator.ResetAsync(cancellationToken).ConfigureAwait(false);
        _runtimeState = null;
        _recoverySnapshot = await _persistentRuntimeCoordinator
            .GetRecoverySnapshotAsync(cancellationToken)
            .ConfigureAwait(false);
        return BuildSnapshot();
    }

    /// <summary>
    /// Fetches the pilot's next assigned flight from the API and pushes it into
    /// the runtime coordinator as the current session context.
    /// </summary>
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
                }
                : new FlightSessionContext();

            _persistentRuntimeCoordinator.UpdateContext(context);
        }
        catch
        {
            // Swallow — active flight is optional; don't crash the tracker over it.
        }
    }

    public async Task SaveSettingsAsync(TrackerAppSettings settings, CancellationToken cancellationToken = default)
    {
        await _settingsStore.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
        _settings = settings;

        // Tear down the old background sync coordinator before replacing the stack.
        if (_serviceStack.BackgroundSyncCoordinator is not null)
            await _serviceStack.BackgroundSyncCoordinator.DisposeAsync().ConfigureAwait(false);

        // Build a fresh service stack so all HTTP clients and token references are up-to-date.
        _serviceStack = _serviceFactory.Create(settings);

        // Hot-swap the uploader on the coordinator (it holds a direct reference to the old one).
        _persistentRuntimeCoordinator.UpdateLivePositionUploader(_serviceStack.LivePositionUploader);
        _activeFlightFetcher = _serviceStack.ActiveFlightFetcher;

        if (_serviceStack.BackgroundSyncCoordinator is not null)
            _serviceStack.BackgroundSyncCoordinator.Start();

        // Immediately fetch the pilot's flight so a newly-pasted API token shows data right away.
        _activeFlightFetchedUtc = DateTimeOffset.MinValue;
        await RefreshActiveFlightAsync(cancellationToken).ConfigureAwait(false);

        // Re-run preflight so a newly-entered token immediately populates identity and
        // grounded state without requiring an app restart.
        await CheckPreflightAsync(cancellationToken).ConfigureAwait(false);
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
        _sessionWasResumed = true;
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
            t.StartsWith("E14") || t == "E145" || t == "E135" || t.StartsWith("RJ"))
            return "regional";

        return "narrowbody";
    }

    private TrackerShellSnapshot BuildSnapshot(
        SimConnectHostStatus? simConnectStatus = null,
        DateTimeOffset? autoResetOccurredUtc = null) =>
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
            AutoResetOccurredUtc = autoResetOccurredUtc,
            PreflightStatus = _preflightStatus,
            ServerCareerResult = _serverCareerResult,
            PostFlightStatus = _postFlightStatus,
            LastUploadAttemptUtc = _lastUploadAttemptUtc,
            LastUploadResult = _lastUploadResult,
            SessionWasResumed = _sessionWasResumed,
        };
}
