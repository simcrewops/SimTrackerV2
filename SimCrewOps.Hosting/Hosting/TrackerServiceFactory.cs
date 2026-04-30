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
    public ILivePositionUploader? CreateLivePositionUploader(TrackerApiSettings apiSettings)
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
            });
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

        var livePositionUploader = string.IsNullOrWhiteSpace(settings.Api.PilotApiToken)
            ? null
            : new HttpLivePositionUploader(
                _httpClientFactory(),
                new SimCrewOpsApiUploaderOptions
                {
                    BaseUri = settings.Api.BaseUri,
                    SimSessionsPath = settings.Api.SimSessionsPath,
                    PilotApiToken = settings.Api.PilotApiToken!,
                    TrackerVersion = settings.Api.TrackerVersion,
                });

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

        if (string.IsNullOrWhiteSpace(settings.Api.PilotApiToken))
        {
            return new TrackerServiceStack
            {
                Settings = settings,
                FlightSessionStore = flightSessionStore,
                LivePositionUploader = livePositionUploader,
                ActiveFlightFetcher = activeFlightFetcher,
                LiveMapService = liveMapService,
            };
        }

        var uploaderOptions = new SimCrewOpsApiUploaderOptions
        {
            BaseUri = settings.Api.BaseUri,
            SimSessionsPath = settings.Api.SimSessionsPath,
            PilotApiToken = settings.Api.PilotApiToken!,
            TrackerVersion = settings.Api.TrackerVersion,
        };

        var uploader = new HttpCompletedSessionUploader(
            _httpClientFactory(),
            uploaderOptions);

        var preflightChecker = new HttpPreflightChecker(
            _httpClientFactory(),
            uploaderOptions);

        if (!settings.BackgroundSync.Enabled)
        {
            return new TrackerServiceStack
            {
                Settings = settings,
                FlightSessionStore = flightSessionStore,
                LivePositionUploader = livePositionUploader,
                PreflightChecker = preflightChecker,
                ActiveFlightFetcher = activeFlightFetcher,
                LiveMapService = liveMapService,
            };
        }

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
            PreflightChecker = preflightChecker,
            CompletedSessionSyncService = syncService,
            BackgroundSyncCoordinator = backgroundSyncCoordinator,
            ActiveFlightFetcher = activeFlightFetcher,
            LiveMapService = liveMapService,
        };
    }
}
