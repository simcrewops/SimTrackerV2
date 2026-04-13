using SimCrewOps.Hosting.Models;

namespace SimCrewOps.Hosting.Config;

public interface ITrackerAppSettingsStore
{
    Task<TrackerAppSettings?> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(TrackerAppSettings settings, CancellationToken cancellationToken = default);
}
