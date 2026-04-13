using SimCrewOps.Runways.Models;

namespace SimCrewOps.Runways.Providers;

public sealed class FallbackRunwayDataProvider(params IRunwayDataProvider[] providers) : IRunwayDataProvider
{
    private readonly IReadOnlyList<IRunwayDataProvider> _providers = providers;

    public async Task<AirportRunwayCatalog?> GetRunwaysAsync(string airportIcao, CancellationToken cancellationToken = default)
    {
        foreach (var provider in _providers)
        {
            var catalog = await provider.GetRunwaysAsync(airportIcao, cancellationToken).ConfigureAwait(false);
            if (catalog?.Runways.Count > 0)
            {
                return catalog;
            }
        }

        return null;
    }
}
