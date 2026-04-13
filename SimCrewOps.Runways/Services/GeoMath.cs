namespace SimCrewOps.Runways.Services;

internal static class GeoMath
{
    private const double EarthRadiusFeet = 20_925_524.9;

    public static double HeadingDifferenceDegrees(double a, double b)
    {
        var diff = Math.Abs(NormalizeHeading(a) - NormalizeHeading(b));
        return diff > 180 ? 360 - diff : diff;
    }

    public static (double EastFeet, double NorthFeet) ToLocalFeet(
        double originLatitude,
        double originLongitude,
        double targetLatitude,
        double targetLongitude)
    {
        var originLatRad = DegreesToRadians(originLatitude);
        var targetLatRad = DegreesToRadians(targetLatitude);
        var deltaLatRad = targetLatRad - originLatRad;
        var deltaLonRad = DegreesToRadians(targetLongitude - originLongitude);
        var meanLatRad = (originLatRad + targetLatRad) / 2.0;

        var northFeet = deltaLatRad * EarthRadiusFeet;
        var eastFeet = deltaLonRad * Math.Cos(meanLatRad) * EarthRadiusFeet;
        return (eastFeet, northFeet);
    }

    public static (double Latitude, double Longitude) DestinationPoint(
        double startLatitude,
        double startLongitude,
        double headingDegrees,
        double distanceFeet)
    {
        var headingRad = DegreesToRadians(NormalizeHeading(headingDegrees));
        var distanceRad = distanceFeet / EarthRadiusFeet;
        var startLatRad = DegreesToRadians(startLatitude);
        var startLonRad = DegreesToRadians(startLongitude);

        var destLatRad = Math.Asin(
            Math.Sin(startLatRad) * Math.Cos(distanceRad) +
            Math.Cos(startLatRad) * Math.Sin(distanceRad) * Math.Cos(headingRad));

        var destLonRad = startLonRad + Math.Atan2(
            Math.Sin(headingRad) * Math.Sin(distanceRad) * Math.Cos(startLatRad),
            Math.Cos(distanceRad) - Math.Sin(startLatRad) * Math.Sin(destLatRad));

        return (RadiansToDegrees(destLatRad), RadiansToDegrees(destLonRad));
    }

    public static double NormalizeHeading(double headingDegrees)
    {
        var normalized = headingDegrees % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static double RadiansToDegrees(double radians) => radians * 180.0 / Math.PI;
}
