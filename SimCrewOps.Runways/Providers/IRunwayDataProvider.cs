using SimCrewOps.Runways.Models;

namespace SimCrewOps.Runways.Providers;

public interface IRunwayDataProvider
{
    Task<AirportRunwayCatalog?> GetRunwaysAsync(string airportIcao, CancellationToken cancellationToken = default);
}
