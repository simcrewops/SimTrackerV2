namespace SimCrewOps.App.Wpf.Infrastructure;

/// <summary>
/// Converts geographic coordinates (WGS-84) to 2D canvas pixel positions
/// using an equirectangular (plate carrée) projection.
/// </summary>
public static class MapProjection
{
    // Latitude range for the world map image (standard Web Mercator approximation).
    private const double LatMin = -85.0;
    private const double LatMax =  85.0;
    private const double LonMin = -180.0;
    private const double LonMax =  180.0;

    /// <summary>
    /// Maps a lat/lon coordinate to a pixel position on a canvas of the given size.
    /// Returns (X, Y) with (0,0) at the top-left corner.
    /// </summary>
    public static (double X, double Y) LatLonToCanvas(
        double lat, double lon, double canvasWidth, double canvasHeight)
    {
        var x = (lon - LonMin) / (LonMax - LonMin) * canvasWidth;
        var y = (LatMax - lat) / (LatMax - LatMin) * canvasHeight;
        return (x, y);
    }

    /// <summary>
    /// Clamps the output so markers near the poles don't fly off the canvas edge.
    /// </summary>
    public static (double X, double Y) LatLonToCanvasClamped(
        double lat, double lon, double canvasWidth, double canvasHeight)
    {
        var (x, y) = LatLonToCanvas(lat, lon, canvasWidth, canvasHeight);
        x = Math.Clamp(x, 0, canvasWidth);
        y = Math.Clamp(y, 0, canvasHeight);
        return (x, y);
    }
}
