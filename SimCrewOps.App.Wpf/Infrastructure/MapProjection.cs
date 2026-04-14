namespace SimCrewOps.App.Wpf.Infrastructure;

/// <summary>
/// Equirectangular projection utilities for rendering lat/lon positions onto a canvas.
/// </summary>
public static class MapProjection
{
    private const double MinLatitude = -85.0;
    private const double MaxLatitude = 85.0;
    private const double MinLongitude = -180.0;
    private const double MaxLongitude = 180.0;

    /// <summary>
    /// Maps a geographic coordinate to a canvas pixel position using an equirectangular projection.
    /// Longitude -180..180 maps to x 0..canvasWidth; latitude 85..-85 maps to y 0..canvasHeight.
    /// </summary>
    public static (double X, double Y) LatLonToCanvas(
        double lat,
        double lon,
        double canvasWidth,
        double canvasHeight)
    {
        var x = (lon - MinLongitude) / (MaxLongitude - MinLongitude) * canvasWidth;
        var y = (MaxLatitude - lat) / (MaxLatitude - MinLatitude) * canvasHeight;
        return (x, y);
    }
}
