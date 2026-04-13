using SimCrewOps.Runways.Models;

namespace SimCrewOps.Runways.Services;

public sealed class TouchdownZoneCalculator
{
    public const double TouchdownZoneLengthFeet = 3000;

    public TouchdownProjection ProjectTouchdown(RunwayEnd runway, double touchdownLatitude, double touchdownLongitude)
    {
        ArgumentNullException.ThrowIfNull(runway);

        var (eastFeet, northFeet) = GeoMath.ToLocalFeet(
            runway.ThresholdLatitude,
            runway.ThresholdLongitude,
            touchdownLatitude,
            touchdownLongitude);

        var headingRad = GeoMath.NormalizeHeading(runway.TrueHeadingDegrees) * Math.PI / 180.0;
        var unitEast = Math.Sin(headingRad);
        var unitNorth = Math.Cos(headingRad);

        var alongTrackFeet = (eastFeet * unitEast) + (northFeet * unitNorth);
        var crossTrackFeet = (eastFeet * unitNorth) - (northFeet * unitEast);
        var distanceFromThresholdFeet = Math.Max(0, alongTrackFeet);
        var touchdownZoneExcessFeet = Math.Max(0, distanceFromThresholdFeet - TouchdownZoneLengthFeet);

        return new TouchdownProjection
        {
            AlongTrackDistanceFeet = alongTrackFeet,
            DistanceFromThresholdFeet = distanceFromThresholdFeet,
            CrossTrackDistanceFeet = crossTrackFeet,
            TouchdownZoneExcessDistanceFeet = touchdownZoneExcessFeet,
        };
    }
}
