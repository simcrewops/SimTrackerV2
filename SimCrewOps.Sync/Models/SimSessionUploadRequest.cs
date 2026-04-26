using System.Text.Json.Serialization;

namespace SimCrewOps.Sync.Models;

public sealed record SimSessionUploadRequest
{
    [JsonPropertyName("bounces")]
    public int Bounces { get; init; }

    [JsonPropertyName("touchdownVS")]
    public double TouchdownVS { get; init; }

    [JsonPropertyName("touchdownBank")]
    public double TouchdownBank { get; init; }

    [JsonPropertyName("touchdownIAS")]
    public double TouchdownIAS { get; init; }

    [JsonPropertyName("touchdownPitch")]
    public double TouchdownPitch { get; init; }

    [JsonPropertyName("actualBlocksOff")]
    public DateTimeOffset? ActualBlocksOff { get; init; }

    [JsonPropertyName("actualWheelsOff")]
    public DateTimeOffset? ActualWheelsOff { get; init; }

    [JsonPropertyName("actualWheelsOn")]
    public DateTimeOffset? ActualWheelsOn { get; init; }

    [JsonPropertyName("actualBlocksOn")]
    public DateTimeOffset? ActualBlocksOn { get; init; }

    [JsonPropertyName("blockTimeActual")]
    public double? BlockTimeActual { get; init; }

    [JsonPropertyName("blockTimeScheduled")]
    public double? BlockTimeScheduled { get; init; }

    [JsonPropertyName("crashDetected")]
    public bool CrashDetected { get; init; }

    [JsonPropertyName("overspeedEvents")]
    public int OverspeedEvents { get; init; }

    [JsonPropertyName("stallEvents")]
    public int StallEvents { get; init; }

    [JsonPropertyName("gpwsEvents")]
    public int GpwsEvents { get; init; }

    [JsonPropertyName("grade")]
    public string Grade { get; init; } = "F";

    [JsonPropertyName("scoreFinal")]
    public double ScoreFinal { get; init; }

    [JsonPropertyName("trackerVersion")]
    public string TrackerVersion { get; init; } = "dev";

    [JsonPropertyName("flightMode")]
    public string FlightMode { get; init; } = "free_flight";

    [JsonPropertyName("bidId")]
    public string? BidId { get; init; }

    [JsonPropertyName("departure")]
    public string? Departure { get; init; }

    [JsonPropertyName("arrival")]
    public string? Arrival { get; init; }

    /// <summary>
    /// WGS-84 latitude of the initial wheel contact point, in decimal degrees.
    /// Zero when no touchdown was recorded (e.g. session ended in the air).
    /// </summary>
    [JsonPropertyName("touchdownLat")]
    public double TouchdownLat { get; init; }

    /// <summary>
    /// WGS-84 longitude of the initial wheel contact point, in decimal degrees.
    /// Zero when no touchdown was recorded (e.g. session ended in the air).
    /// </summary>
    [JsonPropertyName("touchdownLon")]
    public double TouchdownLon { get; init; }

    /// <summary>
    /// ICAO aircraft type code as detected by MSFS (e.g. "A319", "B738").
    /// Sourced from the ATC MODEL SimVar; null for free flights where no type
    /// was detected before blocks-on.
    /// </summary>
    [JsonPropertyName("aircraft")]
    public string? AircraftType { get; init; }

    /// <summary>
    /// World map size category: "regional", "narrowbody", or "widebody".
    /// Derived from <see cref="AircraftType"/> at upload time.
    /// </summary>
    [JsonPropertyName("aircraftCategory")]
    public string? AircraftCategory { get; init; }

    /// <summary>
    /// Per-phase score breakdown with all deduction findings.
    /// Allows the website to show pilots exactly why they lost points on each phase.
    /// Only findings with actual deductions (PointsDeducted > 0) or automatic fails are included.
    /// </summary>
    [JsonPropertyName("phaseFindings")]
    public IReadOnlyList<PhaseScoreFindingUpload>? PhaseFindings { get; init; }

    /// <summary>
    /// Global safety deductions (overspeed, stall, GPWS) that are not tied to a single phase.
    /// </summary>
    [JsonPropertyName("globalFindings")]
    public IReadOnlyList<ScoreFindingUpload>? GlobalFindings { get; init; }
}

public sealed record PhaseScoreFindingUpload
{
    [JsonPropertyName("phase")]
    public string Phase { get; init; } = "";

    [JsonPropertyName("maxPoints")]
    public double MaxPoints { get; init; }

    [JsonPropertyName("awardedPoints")]
    public double AwardedPoints { get; init; }

    /// <summary>Deduction findings for this phase. Empty list = clean phase.</summary>
    [JsonPropertyName("findings")]
    public IReadOnlyList<ScoreFindingUpload> Findings { get; init; } = [];
}

public sealed record ScoreFindingUpload
{
    [JsonPropertyName("description")]
    public string Description { get; init; } = "";

    [JsonPropertyName("pointsDeducted")]
    public double PointsDeducted { get; init; }

    [JsonPropertyName("isAutomaticFail")]
    public bool IsAutomaticFail { get; init; }
}
