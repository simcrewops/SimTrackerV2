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

    [JsonPropertyName("runwayIdentifier")]
    public string? RunwayIdentifier { get; init; }

    [JsonPropertyName("runwayHeadingTrue")]
    public double? RunwayHeadingTrue { get; init; }

    [JsonPropertyName("runwayLengthFt")]
    public double? RunwayLengthFt { get; init; }

    [JsonPropertyName("runwayWidthFt")]
    public double? RunwayWidthFt { get; init; }

    [JsonPropertyName("runwayThresholdLat")]
    public double? RunwayThresholdLat { get; init; }

    [JsonPropertyName("runwayThresholdLon")]
    public double? RunwayThresholdLon { get; init; }

    [JsonPropertyName("touchdownCenterlineDeviationFt")]
    public double? TouchdownCenterlineDeviationFt { get; init; }

    [JsonPropertyName("touchdownCrabAngleDegrees")]
    public double? TouchdownCrabAngleDegrees { get; init; }

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

    // ── Session timing ──────────────────────────────────────────────────────────

    [JsonPropertyName("enginesStartedAt")]
    public DateTimeOffset? EnginesStartedAt { get; init; }

    [JsonPropertyName("wheelsOffAt")]
    public DateTimeOffset? WheelsOffAt { get; init; }

    [JsonPropertyName("wheelsOnAt")]
    public DateTimeOffset? WheelsOnAt { get; init; }

    [JsonPropertyName("enginesOffAt")]
    public DateTimeOffset? EnginesOffAt { get; init; }

    // ── Fuel ───────────────────────────────────────────────────────────────────

    [JsonPropertyName("fuelAtDepartureLbs")]
    public double FuelAtDepartureLbs { get; init; }

    [JsonPropertyName("fuelAtLandingLbs")]
    public double FuelAtLandingLbs { get; init; }

    [JsonPropertyName("fuelBurnedLbs")]
    public double FuelBurnedLbs { get; init; }

    // ── ILS approach quality ────────────────────────────────────────────────────

    [JsonPropertyName("ilsApproachDetected")]
    public bool IlsApproachDetected { get; init; }

    [JsonPropertyName("ilsMaxGlideslopeDevDots")]
    public double IlsMaxGlideslopeDevDots { get; init; }

    [JsonPropertyName("ilsAvgGlideslopeDevDots")]
    public double IlsAvgGlideslopeDevDots { get; init; }

    [JsonPropertyName("ilsMaxLocalizerDevDots")]
    public double IlsMaxLocalizerDevDots { get; init; }

    [JsonPropertyName("ilsAvgLocalizerDevDots")]
    public double IlsAvgLocalizerDevDots { get; init; }

    // ── Extended touchdown context ──────────────────────────────────────────────

    [JsonPropertyName("touchdownAutopilotEngaged")]
    public bool TouchdownAutopilotEngaged { get; init; }

    [JsonPropertyName("touchdownSpoilersDeployed")]
    public bool TouchdownSpoilersDeployed { get; init; }

    [JsonPropertyName("touchdownReverseThrustUsed")]
    public bool TouchdownReverseThrustUsed { get; init; }

    [JsonPropertyName("touchdownWindSpeedKts")]
    public double TouchdownWindSpeedKts { get; init; }

    [JsonPropertyName("touchdownWindDirectionDeg")]
    public double TouchdownWindDirectionDeg { get; init; }

    [JsonPropertyName("touchdownHeadwindKts")]
    public double TouchdownHeadwindKts { get; init; }

    [JsonPropertyName("touchdownCrosswindKts")]
    public double TouchdownCrosswindKts { get; init; }

    [JsonPropertyName("touchdownOatCelsius")]
    public double TouchdownOatCelsius { get; init; }

    // ── GPS flight-path track ───────────────────────────────────────────────────

    /// <summary>
    /// Downsampled GPS track (one point every ~30 s plus phase-change points).
    /// Null when no track was recorded (e.g. session started after cruise).
    /// </summary>
    [JsonPropertyName("gpsTrack")]
    public IReadOnlyList<GpsTrackPointUpload>? GpsTrack { get; init; }
}

/// <summary>A single point in the GPS flight-path track.</summary>
public sealed record GpsTrackPointUpload
{
    [JsonPropertyName("t")]
    public DateTimeOffset TimestampUtc { get; init; }

    [JsonPropertyName("lat")]
    public double Latitude { get; init; }

    [JsonPropertyName("lon")]
    public double Longitude { get; init; }

    [JsonPropertyName("alt")]
    public double AltitudeFeet { get; init; }

    [JsonPropertyName("gs")]
    public double GroundSpeedKnots { get; init; }

    [JsonPropertyName("phase")]
    public string Phase { get; init; } = "";
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
