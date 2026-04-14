using SimCrewOps.Sync.Models;

namespace SimCrewOps.Sync.Sync;

/// <summary>
/// Fetches the pilot's next assigned flight from the SimCrewOps backend.
/// Returns null when the pilot has no flight queued or the request fails.
/// </summary>
public interface IActiveFlightFetcher
{
    Task<ActiveFlightResponse?> FetchAsync(CancellationToken cancellationToken = default);
}
