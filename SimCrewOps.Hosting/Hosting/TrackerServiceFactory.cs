using SimCrewOps.Hosting.Models;
using SimCrewOps.Persistence.Persistence;
using SimCrewOps.Sync.Sync;

namespace SimCrewOps.Hosting.Hosting;

public sealed class TrackerServiceFactory
{
    private readonly Func<HttpClient> _httpClientFactory;

    public TrackerServiceFactory(Func<HttpClient>? httpClientFactory = null)
    {
        _httpClientFactory = httpClientFactory ?? (() => new HttpClient());
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

        if (!settings.BackgroundSync.Enabled || string.IsNullOrWhiteSpace(settings.Api.PilotApiToken))
        {
            return new TrackerServiceStack
            {
                Settings = settings,
                FlightSessionStore = flightSessionStore,
                LivePositionUploader = livePositionUploader,
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
            });

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
        };
    }
}
