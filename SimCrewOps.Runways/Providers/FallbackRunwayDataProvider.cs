using SimCrewOps.Runways.Models;
using System.Diagnostics;

namespace SimCrewOps.Runways.Providers;

public sealed class FallbackRunwayDataProvider(params IRunwayDataProvider[] providers) : IRunwayDataProvider
{
    private readonly IReadOnlyList<IRunwayDataProvider> _providers = providers;

    public async Task<AirportRunwayCatalog?> GetRunwaysAsync(string airportIcao, CancellationToken cancellationToken = default)
    {
        foreach (var provider in _providers)
        {
            AirportRunwayCatalog? catalog;
            try
            {
                catalog = await provider.GetRunwaysAsync(airportIcao, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Trace.TraceWarning(
                    "Runway provider {0} failed for {1}: {2}",
                    provider.GetType().Name,
                    airportIcao,
                    ex.Message);
                continue;
            }

            if (catalog?.Runways.Count > 0)
            {
                return catalog;
            }
        }

        return null;
    }
}
