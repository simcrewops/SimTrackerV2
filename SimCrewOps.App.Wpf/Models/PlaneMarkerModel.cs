using SimCrewOps.App.Wpf.Infrastructure;
using SimCrewOps.Hosting.Models;

namespace SimCrewOps.App.Wpf.Models;

/// <summary>
/// A resolved canvas marker for a single live flight,
/// ready for the LiveMapView to render without further math.
/// </summary>
public sealed class PlaneMarkerModel
{
    /// <summary>Canvas X position (pixels from left).</summary>
    public double X { get; init; }

    /// <summary>Canvas Y position (pixels from top).</summary>
    public double Y { get; init; }

    /// <summary>Heading in degrees (0 = north, clockwise).</summary>
    public double Heading { get; init; }

    /// <summary>True when this marker belongs to the authenticated pilot.</summary>
    public bool IsMyFlight { get; init; }

    /// <summary>Display label shown on hover / in the HUD sidebar.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Altitude in feet.</summary>
    public double Altitude { get; init; }

    /// <summary>Ground speed in knots.</summary>
    public double GroundSpeed { get; init; }

    /// <summary>Flight phase string (e.g. "Cruise", "Approach").</summary>
    public string Phase { get; init; } = string.Empty;

    /// <summary>
    /// Builds a PlaneMarkerModel from a <see cref="LiveFlight"/> and a canvas size.
    /// </summary>
    public static PlaneMarkerModel FromLiveFlight(
        LiveFlight flight, double canvasWidth, double canvasHeight)
    {
        var (x, y) = MapProjection.LatLonToCanvasClamped(
            flight.Latitude, flight.Longitude, canvasWidth, canvasHeight);

        return new PlaneMarkerModel
        {
            X          = x,
            Y          = y,
            Heading    = flight.Heading,
            IsMyFlight = flight.IsMyFlight,
            Label      = $"{flight.Callsign}  {flight.PilotName}",
            Altitude   = flight.Altitude,
            GroundSpeed = flight.GroundSpeed,
            Phase      = flight.Phase,
        };
    }
}
