using SimCrewOps.Tracking.Models;

namespace SimCrewOps.Runtime.Models;

public sealed record FlightSessionContext
{
    public string? PilotId { get; init; }
    public string? BidId { get; init; }
    public string? DepartureAirportIcao { get; init; }
    public string? ArrivalAirportIcao { get; init; }
    public string FlightMode { get; init; } = "free_flight";
    public double? ScheduledBlockHours { get; init; }
    public FlightSessionProfile Profile { get; init; } = new();
}
