using SimCrewOps.Runways.Models;
using SimCrewOps.Runways.Providers;

namespace SimCrewOps.App.Wpf.Services;

internal sealed class NullRunwayDataProvider : IRunwayDataProvider
{
    public Task<AirportRunwayCatalog?> GetRunwaysAsync(string airportIcao, CancellationToken cancellationToken = default) =>
        Task.FromResult<AirportRunwayCatalog?>(null);
}
