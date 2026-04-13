namespace SimCrewOps.Runtime.Models;

public sealed record FlightSessionBlockTimes
{
    public DateTimeOffset? BlocksOffUtc { get; init; }
    public DateTimeOffset? WheelsOffUtc { get; init; }
    public DateTimeOffset? WheelsOnUtc { get; init; }
    public DateTimeOffset? BlocksOnUtc { get; init; }
}
