using SimCrewOps.Hosting.Models;
using SimCrewOps.Persistence.Persistence;
using SimCrewOps.Runtime.Runtime;
using SimCrewOps.Sync.Sync;

namespace SimCrewOps.Hosting.Hosting;

public sealed class TrackerServiceFactory
{
    private readonly Func<HttpClient> _httpClientFactory;

    public TrackerServiceFactory(Func<HttpClient>? httpClientFactory = null)
    {
        _httpClientFactory = httpClientFactory ?? (() => new HttpClient());
    }

    /// <summary>
    /// Creates just the preflight checker from the given settings.
    /// Returns null if no API token is configured.
    /// Used for hot-reloading after a settings change without restarting the app.
    /// </summary>
    public IPreflightChecker? CreatePreflightChecker(TrackerApiSettings apiSettings)
    {
        ArgumentNullException.ThrowIfNull(apiSettings);

        if (string.IsNullOrWhiteSpace(apiSettings.PilotApiToken))
            return null;

        return new HttpPreflightChecker(
            _httpClientFactory(),
            new SimCrewOpsApiUploaderOptions
            {
                BaseUri = apiSettings.BaseUri,
                SimSessionsPath = apiSettings.SimSessionsPath,
                PilotApiToken = apiSettings.PilotApiToken!,
                TrackerVersion = apiSettings.TrackerVersion,
            });
    }

    /// <summary>
    /// Creates just the active flight fetcher from the given settings.
    /// Returns null if no API token is configured.
    /// Used for hot-reloading after settings change without restarting the app.
    /// </summary>
    public IActiveFlightFetcher? CreateActiveFlightFetcher(TrackerApiSettings apiSettings)
    {
        ArgumentNullException.ThrowIfNull(apiSettings);

        if (string.IsNullOrWhiteSpace(apiSettings.PilotApiToken))
            return null;

        return new HttpActiveFlightFetcher(
            _httpClientFactory(),
            new SimCrewOpsApiUploaderOptions
            {
                BaseUri = apiSettings.BaseUri,
                SimSessionsPath = apiSettings.SimSessionsPath,
                PilotApiToken = apiSettings.PilotApiToken!,
                TrackerVersion = apiSettings.TrackerVersion,
            });
    }

    /// <summary>
    /// Creates just the live position uploader from the given settings.
    /// Returns null if no API token is configured.
    /// Used for hot-reloading after settings change without restarting the app.
    /// </summary>
    /// <param name="apiSettings">Current API settings (base URI, pilot token, etc.).</param>
    /// <param name="apiKeyStore">
    /// Optional shared <see cref="TrackerApiKeyStore"/>.  Pass the same store that was created
    /// by <see cref="Create"/> so hot-reloaded uploaders continue to honour any tracker API key
    /// that was previously received from the bootstrap endpoint.
    /// </param>
    public ILivePositionUploader? CreateLivePositionUploader(
        TrackerApiSettings apiSettings,
        TrackerApiKeyStore? apiKeyStore = null)
    {
        ArgumentNullException.ThrowIfNull(apiSettings);

        if (string.IsNullOrWhiteSpace(apiSettings.PilotApiToken))
            return null;

        return new HttpLivePositionUploader(
            _httpClientFactory(),
            new SimCrewOpsApiUploaderOptions
            {
                BaseUri = apiSettings.BaseUri,
                SimSessionsPath = apiSettings.SimSessionsPath,
                PilotApiToken = apiSettings.PilotApiToken!,
                TrackerVersion = apiSettings.TrackerVersion,
            },
            apiKeyStore);
    }

    public TrackerServiceStack Create(TrackerAppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var flightSessionStore = new FileSystemFlightSessionStore(new FileSystemFlightSessionStoreOptions
        {
            RootDirectory = settings.Storage.RootDirectory,
            CurrentSessionFileName = settings.Storage.CurrentSessionFileName,
            CompletedSessionsDirectoryName = settings.Storage.CompletedSessionsDirectoryName,
        });

        // One shared store per service stack — all uploaders reference the same object so
        // updating ApiKey once is enough to affect both live-position and session uploads.
        var apiKeyStore = string.IsNullOrWhiteSpace(settings.Api.PilotApiToken)
            ? null
            : new TrackerApiKeyStore();

        var livePositionUploader = apiKeyStore is null
            ? null
            : new HttpLivePositionUploader(
                _httpClientFactory(),
                new SimCrewOpsApiUploaderOptions
                {
                    BaseUri = settings.Api.BaseUri,
                    SimSessionsPath = settings.Api.SimSessionsPath,
                    PilotApiToken = settings.Api.PilotApiToken!,
                    TrackerVersion = settings.Api.TrackerVersion,
                },
                apiKeyStore);

        var activeFlightFetcher = string.IsNullOrWhiteSpace(settings.Api.PilotApiToken)
            ? null
            : new HttpActiveFlightFetcher(
                _httpClientFactory(),
                new SimCrewOpsApiUploaderOptions
                {
                    BaseUri = settings.Api.BaseUri,
                    SimSessionsPath = settings.Api.SimSessionsPath,
                    PilotApiToken = settings.Api.PilotApiToken!,
                    TrackerVersion = settings.Api.TrackerVersion,
                });

        var liveMapService = string.IsNullOrWhiteSpace(settings.Api.PilotApiToken)
            ? null
            : new LiveMapService(
                _httpClientFactory(),
                settings.Api.BaseUri,
                settings.Api.PilotApiToken);

        var preflightChecker = string.IsNullOrWhiteSpace(settings.Api.PilotApiToken)
            ? null
            : new HttpPreflightChecker(
                _httpClientFactory(),
                new SimCrewOpsApiUploaderOptions
                {
                    BaseUri = settings.Api.BaseUri,
                    SimSessionsPath = settings.Api.SimSessionsPath,
                    PilotApiToken = settings.Api.PilotApiToken!,
                    TrackerVersion = settings.Api.TrackerVersion,
                });

        if (!settings.BackgroundSync.Enabled || string.IsNullOrWhiteSpace(settings.Api.PilotApiToken))
        {
            return new TrackerServiceStack
            {
                Settings = settings,
                FlightSessionStore = flightSessionStore,
                LivePositionUploader = livePositionUploader,
                ActiveFlightFetcher = activeFlightFetcher,
                LiveMapService = liveMapService,
                TrackerApiKeyStore = apiKeyStore,
                PreflightChecker = preflightChecker,
            };
        }

        var uploader = new HttpCompletedSessionUploader(
            _httpClientFactory(),
            new SimCrewOpsApiUploaderOptions
            {
                BaseUri = settings.Api.BaseUri,
                SimSessionsPath = settings.Api.SimSessionsPath,
                PilotApiToken = settings.Api.PilotApiToken!,
                TrackerVersion = settings.Api.TrackerVersion,
            },
            apiKeyStore: apiKeyStore);

        var syncService = new CompletedSessionSyncService(flightSessionStore, uploader);
        var backgroundSyncCoordinator = new BackgroundSyncCoordinator(
            syncService,
            TimeSpan.FromSeconds(Math.Max(1, settings.BackgroundSync.IntervalSeconds)),
            settings.BackgroundSync.MaxSessionsPerPass);

        return new TrackerServiceStack
        {
            Settings = settings,
            FlightSessionStore = flightSessionStore,
            LivePositionUploader = livePositionUploader,
            CompletedSessionUploader = uploader,
            CompletedSessionSyncService = syncService,
            BackgroundSyncCoordinator = backgroundSyncCoordinator,
            ActiveFlightFetcher = activeFlightFetcher,
            LiveMapService = liveMapService,
            TrackerApiKeyStore = apiKeyStore,
            PreflightChecker = preflightChecker,
        };
    }
}
