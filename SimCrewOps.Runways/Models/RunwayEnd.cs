namespace SimCrewOps.Runways.Models;

public sealed record RunwayEnd
{
    public required string AirportIcao { get; init; }
    public required string RunwayIdentifier { get; init; }
    public required double TrueHeadingDegrees { get; init; }
    public required double LengthFeet { get; init; }
    public required double ThresholdLatitude { get; init; }
    public required double ThresholdLongitude { get; init; }
    public double DisplacedThresholdFeet { get; init; }
    public double WidthFeet { get; init; }
    public RunwayDataSource DataSource { get; init; }
}
