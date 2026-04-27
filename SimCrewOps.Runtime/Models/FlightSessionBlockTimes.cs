namespace SimCrewOps.Runtime.Models;

public sealed record FlightSessionBlockTimes
{
    public DateTimeOffset? BlocksOffUtc { get; init; }
    public DateTimeOffset? WheelsOffUtc { get; init; }
    public DateTimeOffset? WheelsOnUtc { get; init; }
    public DateTimeOffset? BlocksOnUtc { get; init; }

    /// <summary>
    /// UTC timestamp when the post-flight session-end condition was met:
    /// all engines shut down, beacon off, and parking brake set after a completed landing.
    /// Acts as a fallback completion trigger when the <see cref="BlocksOnUtc"/> event
    /// is not fired (e.g. SimConnect disconnect at gate) and as the primary trigger
    /// for sending full session data to the webapp.
    /// </summary>
    public DateTimeOffset? SessionEndTriggeredUtc { get; init; }
}
