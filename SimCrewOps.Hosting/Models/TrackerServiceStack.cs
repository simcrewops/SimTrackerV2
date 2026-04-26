using SimCrewOps.Hosting.Hosting;
using SimCrewOps.Persistence.Persistence;
using SimCrewOps.Runtime.Runtime;
using SimCrewOps.Sync.Sync;

namespace SimCrewOps.Hosting.Models;

public sealed record TrackerServiceStack
{
    public required TrackerAppSettings Settings { get; init; }
    public required IFlightSessionStore FlightSessionStore { get; init; }
    public ILivePositionUploader? LivePositionUploader { get; init; }
    public ICompletedSessionUploader? CompletedSessionUploader { get; init; }
    public ICompletedSessionSyncService? CompletedSessionSyncService { get; init; }
    public BackgroundSyncCoordinator? BackgroundSyncCoordinator { get; init; }
    public IActiveFlightFetcher? ActiveFlightFetcher { get; init; }
    public LiveMapService? LiveMapService { get; init; }

    /// <summary>
    /// Shared mutable holder for the tracker API key returned by the bootstrap endpoint.
    /// Null when no API token is configured (no HTTP stack was created).
    /// Update <see cref="TrackerApiKeyStore.ApiKey"/> when the bootstrap response includes
    /// a <c>trackerApiKey</c>; both live-position and session-upload calls will then
    /// automatically prefer it over the static pilot token.
    /// </summary>
    public TrackerApiKeyStore? TrackerApiKeyStore { get; init; }

    public bool SyncEnabled => BackgroundSyncCoordinator is not null;
}
