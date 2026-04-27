using System.Text.Json.Serialization;

namespace SimCrewOps.Sync.Models;

public sealed record SimSessionUploadRequest
{
    [JsonPropertyName("trackerVersion")]
    public string TrackerVersion { get; init; } = "3.0.0";

    [JsonPropertyName("flightMode")]
    public string FlightMode { get; init; } = "free_flight";

    [JsonPropertyName("bidId")]
    public string? BidId { get; init; }

    [JsonPropertyName("departure")]
    public string? Departure { get; init; }

    [JsonPropertyName("arrival")]
    public string? Arrival { get; init; }

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

    // ── Block times ─────────────────────────────────────────────────────────────

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

    // ── Touchdown position ──────────────────────────────────────────────────────

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

    // ── Crash flag ──────────────────────────────────────────────────────────────

    [JsonPropertyName("crashDetected")]
    public bool CrashDetected { get; init; }

    // ── GPS flight-path track ───────────────────────────────────────────────────

    /// <summary>
    /// Downsampled GPS track (one point every ~30 s plus phase-change points).
    /// Null when no track was recorded (e.g. session started after cruise).
    /// </summary>
    [JsonPropertyName("gpsTrack")]
    public IReadOnlyList<GpsTrackPointUpload>? GpsTrack { get; init; }

    // ── Structured scoring input ───────────────────────────────────────────────

    /// <summary>
    /// All raw phase metrics collected during the flight in a structured object.
    /// Allows the webapp to score or re-score the session with any algorithm.
    /// </summary>
    [JsonPropertyName("scoringInput")]
    public FlightScoreInputV5Upload? ScoreInputV5 { get; init; }

    // ── Landing analysis ──────────────────────────────────────────────────────

    /// <summary>
    /// Rich landing geometry including approach path, threshold crossing speed/height,
    /// and touchdown distance from threshold.  Null when no landing was recorded.
    /// </summary>
    [JsonPropertyName("landingAnalysis")]
    public LandingAnalysisUpload? LandingAnalysis { get; init; }
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
