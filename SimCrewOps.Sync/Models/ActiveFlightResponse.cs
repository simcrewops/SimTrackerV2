namespace SimCrewOps.Sync.Models;

/// <summary>
/// Payload returned by GET /api/tracker/next-trip.
/// <c>Source</c> is null when the pilot has no upcoming flight assigned.
/// </summary>
public sealed record ActiveFlightResponse
{
    /// <summary>
    /// Where the assignment came from: "premium_time", "bid_packet", "open_time",
    /// "dispatch", "booked", or null when no flight is queued.
    /// </summary>
    public string? Source { get; init; }

    /// <summary>The bid ID this flight belongs to. Null for non-career flights or when the
    /// server does not return one. Passed through to position beacons and session uploads
    /// so career flights link to the correct bid on the backend.</summary>
    public string? BidId { get; init; }

    /// <summary>Scheduled block time in decimal hours as stored on the server (blockTime
    /// minutes ÷ 60). Null when the server does not include a block time.</summary>
    public double? ScheduledBlockHours { get; init; }

    public string? Departure { get; init; }
    public string? Arrival { get; init; }
    public string? FlightNumber { get; init; }
    public string? AircraftType { get; init; }
    public string? Airline { get; init; }

    /// <summary>
    /// Per-session tracker API key issued by the bootstrap endpoint.
    /// When present, the tracker uses this as the Bearer token on
    /// <c>POST /api/tracker/position</c> and <c>POST /api/sim-sessions</c>
    /// instead of the static pilot token — allowing the webapp to validate
    /// a scoped, short-lived credential for tracker-specific routes.
    /// Null for older webapp deployments that do not yet return this field.
    /// </summary>
    public string? TrackerApiKey { get; init; }
}
