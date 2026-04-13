namespace SimCrewOps.Runways.Models;

public sealed record TouchdownProjection
{
    public required double AlongTrackDistanceFeet { get; init; }
    public required double DistanceFromThresholdFeet { get; init; }
    public required double CrossTrackDistanceFeet { get; init; }
    public required double TouchdownZoneExcessDistanceFeet { get; init; }
}
