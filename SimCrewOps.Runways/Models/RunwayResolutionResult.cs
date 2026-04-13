namespace SimCrewOps.Runways.Models;

public sealed record RunwayResolutionResult
{
    public required string AirportIcao { get; init; }
    public required RunwayEnd Runway { get; init; }
    public required double HeadingDifferenceDegrees { get; init; }
    public required TouchdownProjection Projection { get; init; }
}
