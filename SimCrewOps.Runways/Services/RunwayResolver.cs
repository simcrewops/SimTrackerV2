using SimCrewOps.Runways.Models;
using SimCrewOps.Runways.Providers;

namespace SimCrewOps.Runways.Services;

public sealed class RunwayResolver
{
    private readonly IRunwayDataProvider _runwayDataProvider;
    private readonly TouchdownZoneCalculator _touchdownZoneCalculator;
    private readonly double _headingToleranceDegrees;

    public RunwayResolver(
        IRunwayDataProvider runwayDataProvider,
        TouchdownZoneCalculator? touchdownZoneCalculator = null,
        double headingToleranceDegrees = 30)
    {
        ArgumentNullException.ThrowIfNull(runwayDataProvider);

        _runwayDataProvider = runwayDataProvider;
        _touchdownZoneCalculator = touchdownZoneCalculator ?? new TouchdownZoneCalculator();
        _headingToleranceDegrees = headingToleranceDegrees;
    }

    public async Task<RunwayResolutionResult?> ResolveAsync(
        RunwayResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var catalog = await _runwayDataProvider
            .GetRunwaysAsync(request.ArrivalAirportIcao, cancellationToken)
            .ConfigureAwait(false);

        if (catalog?.Runways.Count is not > 0)
        {
            return null;
        }

        var bestMatch = catalog.Runways
            .Select(runway =>
            {
                var projection = _touchdownZoneCalculator.ProjectTouchdown(
                    runway,
                    request.TouchdownLatitude,
                    request.TouchdownLongitude);

                return new
                {
                    Runway = runway,
                    HeadingDifference = GeoMath.HeadingDifferenceDegrees(
                        request.TouchdownHeadingTrueDegrees,
                        runway.TrueHeadingDegrees),
                    Projection = projection,
                };
            })
            .Where(candidate => candidate.HeadingDifference <= _headingToleranceDegrees)
            .OrderBy(candidate => candidate.HeadingDifference)
            .ThenBy(candidate => Math.Abs(candidate.Projection.CrossTrackDistanceFeet))
            .ThenBy(candidate => candidate.Projection.DistanceFromThresholdFeet)
            .FirstOrDefault();

        if (bestMatch is null)
        {
            return null;
        }

        return new RunwayResolutionResult
        {
            AirportIcao = catalog.AirportIcao,
            Runway = bestMatch.Runway,
            HeadingDifferenceDegrees = bestMatch.HeadingDifference,
            Projection = bestMatch.Projection,
        };
    }
}
