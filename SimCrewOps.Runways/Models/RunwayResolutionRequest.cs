namespace SimCrewOps.Runways.Models;

public sealed record RunwayResolutionRequest
{
    public required string ArrivalAirportIcao { get; init; }
    public required double TouchdownLatitude { get; init; }
    public required double TouchdownLongitude { get; init; }
    public required double TouchdownHeadingTrueDegrees { get; init; }
}
