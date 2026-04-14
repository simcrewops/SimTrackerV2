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

    public bool SyncEnabled => BackgroundSyncCoordinator is not null;
}
