namespace SimCrewOps.Hosting.Models;

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
    public bool IsMyFlight { get; init; }
}
