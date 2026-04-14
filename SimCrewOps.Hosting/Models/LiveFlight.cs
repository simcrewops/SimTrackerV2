namespace SimCrewOps.Hosting.Models;

/// <summary>
/// A live flight position broadcast by the SimCrewOps backend.
/// Returned by GET /api/tracker/live-flights.
/// </summary>
public sealed record LiveFlight
{
    public required string PilotId { get; init; }
    public required string PilotName { get; init; }
    public required string Callsign { get; init; }
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public required double Altitude { get; init; }
    public required double Heading { get; init; }
    public required double GroundSpeed { get; init; }
    public required string Phase { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>True when this position belongs to the authenticated pilot.</summary>
    public bool IsMyFlight { get; init; }
}
