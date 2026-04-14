using System.Windows;

namespace SimCrewOps.App.Wpf.Utilities;

/// <summary>
/// Converts WGS-84 geographic coordinates to canvas pixel coordinates using a
/// simple equirectangular (plate carrée) projection.
/// </summary>
public static class MapProjection
{
    // Clamp latitude to ±85° — the practical range for an equirectangular world map.
    private const double MinLatitude = -85.0;
    private const double MaxLatitude = 85.0;

    /// <summary>
    /// Maps a WGS-84 latitude/longitude to a pixel position on a canvas of the
    /// given dimensions.
    /// <list type="bullet">
    ///   <item>Longitude -180..180 maps linearly to 0..canvasWidth.</item>
    ///   <item>Latitude 85..-85 maps linearly to 0..canvasHeight (north is up, so higher
    ///   latitude yields a lower Y value).</item>
    /// </list>
    /// </summary>
    public static Point LatLonToCanvas(double lat, double lon, double canvasWidth, double canvasHeight)
    {
        var x = (lon + 180.0) / 360.0 * canvasWidth;
        var y = (MaxLatitude - Math.Clamp(lat, MinLatitude, MaxLatitude))
                / (MaxLatitude - MinLatitude)
                * canvasHeight;
        return new Point(x, y);
    }
}
