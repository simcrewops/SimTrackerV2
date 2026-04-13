namespace SimCrewOps.Tracking.Models;

public sealed record FlightSessionProfile
{
    public bool HeavyFourEngineAircraft { get; init; }
    public int EngineCount { get; init; } = 2;
}
