namespace SimCrewOps.Sync.Models;

/// <summary>
/// Response from GET /api/pilot/preflight.
/// Indicates whether the pilot is cleared to fly or is currently grounded / in crew rest.
/// </summary>
public sealed record PreflightStatusResponse
{
    /// <summary>True when the pilot has an unresolved strike that blocks further flying.</summary>
    public bool IsGrounded { get; init; }

    /// <summary>Human-readable reason for the grounding, e.g. "Unresolved immediate strike: Tail Strike".</summary>
    public string? GroundingReason { get; init; }

    /// <summary>Actionable instruction for the pilot, e.g. "Pay the safety class fee at simcrewops.com".</summary>
    public string? GroundingAction { get; init; }

    /// <summary>Machine-readable strike type, e.g. "tail_strike". Null when not grounded.</summary>
    public string? StrikeType { get; init; }

    /// <summary>True when the pilot is within a mandatory crew-rest window.</summary>
    public bool InCrewRest { get; init; }

    /// <summary>UTC timestamp when the crew-rest window ends. Null when not in crew rest.</summary>
    public DateTimeOffset? CrewRestEndsAt { get; init; }

    /// <summary>True when the pilot should be shown a warning before starting a flight.</summary>
    public bool HasWarning => IsGrounded || InCrewRest;
}

/// <summary>
/// Post-flight status embedded in the POST /api/sim-sessions 201 response body.
/// Indicates whether a strike was issued as a result of the flight just submitted.
/// </summary>
public sealed record PostFlightStatus
{
    /// <summary>True when a strike was issued for this flight and the pilot is now grounded.</summary>
    public bool IsGrounded { get; init; }

    /// <summary>Machine-readable strike type, e.g. "tail_strike".</summary>
    public string? StrikeType { get; init; }

    /// <summary>Actionable instruction shown immediately after the flight, e.g. "Pay the safety class fee…".</summary>
    public string? StrikeAction { get; init; }
}
