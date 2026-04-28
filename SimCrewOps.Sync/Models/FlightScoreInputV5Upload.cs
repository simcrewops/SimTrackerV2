using System.Text.Json.Serialization;

namespace SimCrewOps.Sync.Models;

// ── Top-level container ────────────────────────────────────────────────────────

/// <summary>
/// Structured snapshot of every raw phase metric collected during the flight.
/// Sent as <c>scoringInput</c> in the POST /api/sim-sessions body so the webapp
/// can compute or re-compute scores without needing to know the tracker's
/// scoring algorithm.  Version suffix "V5" denotes the API contract version.
/// This is the v3 shape: raw telemetry metrics only, no grades/findings.
/// </summary>
public sealed record FlightScoreInputV5Upload
{
    [JsonPropertyName("taxiOut")]
    public ScoreInputTaxiV5 TaxiOut { get; init; } = new();

    [JsonPropertyName("taxiIn")]
    public ScoreInputTaxiInV5 TaxiIn { get; init; } = new();

    [JsonPropertyName("takeoff")]
    public ScoreInputTakeoffV5 Takeoff { get; init; } = new();

    [JsonPropertyName("climb")]
    public ScoreInputClimbV5 Climb { get; init; } = new();

    [JsonPropertyName("cruise")]
    public ScoreInputCruiseV5 Cruise { get; init; } = new();

    [JsonPropertyName("descent")]
    public ScoreInputDescentV5 Descent { get; init; } = new();

    [JsonPropertyName("approach")]
    public ScoreInputApproachV5 Approach { get; init; } = new();

    [JsonPropertyName("stabilizedApproach")]
    public ScoreInputStabilizedApproachV5 StabilizedApproach { get; init; } = new();

    [JsonPropertyName("landing")]
    public ScoreInputLandingV5 Landing { get; init; } = new();

    [JsonPropertyName("lightsSystems")]
    public ScoreInputLightsSystemsV5 LightsSystems { get; init; } = new();

    [JsonPropertyName("safety")]
    public ScoreInputSafetyV5 Safety { get; init; } = new();

    [JsonPropertyName("arrival")]
    public ScoreInputArrivalV5? Arrival { get; init; }
}

// ── Phase sub-records ──────────────────────────────────────────────────────────

public sealed record ScoreInputTaxiV5
{
    [JsonPropertyName("maxGroundSpeedKts")]
    public double MaxGroundSpeedKts { get; init; }

    [JsonPropertyName("maxTurnSpeedKts")]
    public double MaxTurnSpeedKts { get; init; }

    [JsonPropertyName("navLightsOn")]
    public bool NavLightsOn { get; init; }

    [JsonPropertyName("strobeLightOnDuringTaxi")]
    public bool StrobeLightOnDuringTaxi { get; init; }
}

public sealed record ScoreInputTaxiInV5
{
    [JsonPropertyName("maxGroundSpeedKts")]
    public double MaxGroundSpeedKts { get; init; }

    [JsonPropertyName("maxTurnSpeedKts")]
    public double MaxTurnSpeedKts { get; init; }

    [JsonPropertyName("navLightsOn")]
    public bool NavLightsOn { get; init; }

    [JsonPropertyName("strobeLightOnDuringTaxi")]
    public bool StrobeLightOnDuringTaxi { get; init; }

    [JsonPropertyName("smoothDeceleration")]
    public bool SmoothDeceleration { get; init; }
}

public sealed record ScoreInputTakeoffV5
{
    [JsonPropertyName("bounceOnRotation")]
    public bool BounceOnRotation { get; init; }

    [JsonPropertyName("positiveRateBeforeGearUp")]
    public bool PositiveRateBeforeGearUp { get; init; }

    [JsonPropertyName("maxBankBelow1000AglDeg")]
    public double MaxBankBelow1000AglDeg { get; init; }

    [JsonPropertyName("maxPitchWhileWowDeg")]
    public double MaxPitchWhileWowDeg { get; init; }

    /// <summary>
    /// Altitude AGL (feet) at the tick where MaxPitchWhileWowDeg was recorded.
    /// The server skips the tail-strike penalty when this exceeds 20 ft.
    /// </summary>
    [JsonPropertyName("maxPitchAglFt")]
    public double MaxPitchAglFt { get; init; }

    [JsonPropertyName("strobeLightsOn")]
    public bool StrobeLightsOn { get; init; }

    [JsonPropertyName("landingLightsOn")]
    public bool LandingLightsOn { get; init; }

    [JsonPropertyName("gForceAtRotation")]
    public double GForceAtRotation { get; init; }
}

public sealed record ScoreInputClimbV5
{
    [JsonPropertyName("isHeavy")]
    public bool IsHeavy { get; init; }

    [JsonPropertyName("maxIasBelowFl100Kts")]
    public double MaxIasBelowFl100Kts { get; init; }

    [JsonPropertyName("maxBankDeg")]
    public double MaxBankDeg { get; init; }

    [JsonPropertyName("minGForce")]
    public double MinGForce { get; init; }

    [JsonPropertyName("maxGForce")]
    public double MaxGForce { get; init; }

    [JsonPropertyName("landingLightsOffAboveFL180")]
    public bool LandingLightsOffAboveFL180 { get; init; }
}

public sealed record ScoreInputCruiseV5
{
    [JsonPropertyName("cruiseAltitudeFt")]
    public double? CruiseAltitudeFt { get; init; }

    [JsonPropertyName("maxAltitudeDeviationFt")]
    public double MaxAltitudeDeviationFt { get; init; }

    [JsonPropertyName("machTarget")]
    public double? MachTarget { get; init; }

    [JsonPropertyName("maxMachDeviation")]
    public double MaxMachDeviation { get; init; }

    [JsonPropertyName("iasTarget")]
    public double? IasTarget { get; init; }

    [JsonPropertyName("maxIasDeviationKts")]
    public double MaxIasDeviationKts { get; init; }

    [JsonPropertyName("speedInstabilityEvents")]
    public int SpeedInstabilityEvents { get; init; }

    [JsonPropertyName("maxBankDeg")]
    public double MaxBankDeg { get; init; }

    [JsonPropertyName("maxTurnBankDeg")]
    public double MaxTurnBankDeg { get; init; }

    [JsonPropertyName("minGForce")]
    public double MinGForce { get; init; }

    [JsonPropertyName("maxGForce")]
    public double MaxGForce { get; init; }
}

public sealed record ScoreInputDescentV5
{
    [JsonPropertyName("isHeavy")]
    public bool IsHeavy { get; init; }

    [JsonPropertyName("maxIasBelowFl100Kts")]
    public double MaxIasBelowFl100Kts { get; init; }

    [JsonPropertyName("maxBankDeg")]
    public double MaxBankDeg { get; init; }

    [JsonPropertyName("minGForce")]
    public double MinGForce { get; init; }

    [JsonPropertyName("maxGForce")]
    public double MaxGForce { get; init; }

    [JsonPropertyName("maxDescentRateFpm")]
    public double MaxDescentRateFpm { get; init; }

    [JsonPropertyName("landingLightsOnBeforeFL180")]
    public bool LandingLightsOnBeforeFL180 { get; init; }

    [JsonPropertyName("maxNoseDownPitchDeg")]
    public double MaxNoseDownPitchDeg { get; init; }
}

public sealed record ScoreInputApproachV5
{
    [JsonPropertyName("approachSpeedKts")]
    public double ApproachSpeedKts { get; init; }

    [JsonPropertyName("maxIasDeviationKts")]
    public double MaxIasDeviationKts { get; init; }

    [JsonPropertyName("gearDownAglFt")]
    public double? GearDownAglFt { get; init; }

    [JsonPropertyName("flapsConfiguredBy1000Agl")]
    public bool FlapsConfiguredBy1000Agl { get; init; }

    [JsonPropertyName("maxBankDeg")]
    public double MaxBankDeg { get; init; }

    [JsonPropertyName("ilsDetected")]
    public bool IlsDetected { get; init; }

    [JsonPropertyName("ilsMaxGlideslopeDevDots")]
    public double IlsMaxGlideslopeDevDots { get; init; }

    [JsonPropertyName("ilsAvgGlideslopeDevDots")]
    public double IlsAvgGlideslopeDevDots { get; init; }

    [JsonPropertyName("ilsMaxLocalizerDevDots")]
    public double IlsMaxLocalizerDevDots { get; init; }

    [JsonPropertyName("ilsAvgLocalizerDevDots")]
    public double IlsAvgLocalizerDevDots { get; init; }
}

public sealed record ScoreInputStabilizedApproachV5
{
    [JsonPropertyName("approachSpeedKts")]
    public double ApproachSpeedKts { get; init; }

    [JsonPropertyName("maxIasDeviationKts")]
    public double MaxIasDeviationKts { get; init; }

    [JsonPropertyName("maxDescentRateFpm")]
    public double MaxDescentRateFpm { get; init; }

    [JsonPropertyName("configChanged")]
    public bool ConfigChanged { get; init; }

    [JsonPropertyName("maxHeadingDeviationDeg")]
    public double MaxHeadingDeviationDeg { get; init; }

    [JsonPropertyName("ilsAvailable")]
    public bool IlsAvailable { get; init; }

    [JsonPropertyName("maxGlideslopeDevDots")]
    public double MaxGlideslopeDevDots { get; init; }

    [JsonPropertyName("pitchAtGateDeg")]
    public double PitchAtGateDeg { get; init; }
}

public sealed record ScoreInputLandingV5
{
    [JsonPropertyName("touchdownRateFpm")]
    public double TouchdownRateFpm { get; init; }

    [JsonPropertyName("touchdownGForce")]
    public double TouchdownGForce { get; init; }

    [JsonPropertyName("bounceCount")]
    public int BounceCount { get; init; }

    [JsonPropertyName("touchdownBankDeg")]
    public double TouchdownBankDeg { get; init; }

    [JsonPropertyName("gearUpAtTouchdown")]
    public bool GearUpAtTouchdown { get; init; }

    [JsonPropertyName("maxPitchWhileWowDeg")]
    public double MaxPitchWhileWowDeg { get; init; }
}

public sealed record ScoreInputLightsSystemsV5
{
    [JsonPropertyName("beaconOnThroughoutFlight")]
    public bool BeaconOnThroughoutFlight { get; init; }

    [JsonPropertyName("navLightsOnThroughoutFlight")]
    public bool NavLightsOnThroughoutFlight { get; init; }

    [JsonPropertyName("strobesCorrect")]
    public bool StrobesCorrect { get; init; }

    [JsonPropertyName("landingLightsCompliance")]
    public double LandingLightsCompliance { get; init; }
}

public sealed record ScoreInputArrivalV5
{
    [JsonPropertyName("enginesOffAfterParkingBrake")]
    public bool EnginesOffAfterParkingBrake { get; init; }

    [JsonPropertyName("beaconOffAfterEngines")]
    public bool BeaconOffAfterEngines { get; init; }
}

public sealed record ScoreInputSafetyV5
{
    [JsonPropertyName("crashDetected")]
    public bool CrashDetected { get; init; }

    [JsonPropertyName("overspeedWarningCount")]
    public int OverspeedWarningCount { get; init; }

    [JsonPropertyName("sustainedOverspeedEvents")]
    public int SustainedOverspeedEvents { get; init; }

    [JsonPropertyName("stallWarningCount")]
    public int StallWarningCount { get; init; }

    [JsonPropertyName("gpwsAlertCount")]
    public int GpwsAlertCount { get; init; }

    [JsonPropertyName("engineShutdownsInFlight")]
    public int EngineShutdownsInFlight { get; init; }

    [JsonPropertyName("engineShutdownInFlight")]
    public bool EngineShutdownInFlight { get; init; }
}

// ── Landing analysis ───────────────────────────────────────────────────────────

/// <summary>
/// Landing analysis object included in the POST /api/sim-sessions body.
/// Contains only the raw approach telemetry stream; all runway geometry
/// (designator, touchdown distance, threshold speed/height, heading) is derived
/// server-side by the webapp using the touchdown GPS coords and OurAirports data.
/// </summary>
public sealed record LandingAnalysisUpload
{
    /// <summary>
    /// Time-series approach profile sampled every ~2 s from when the aircraft enters
    /// Descent/Approach within 15 nm of the arrival airport through touchdown.
    /// Chronological order — farthest sample first, touchdown last.
    /// Null when no approach data was recorded.
    /// </summary>
    [JsonPropertyName("approachPath")]
    public IReadOnlyList<ApproachSampleUpload>? ApproachPath { get; init; }

    /// <summary>WGS-84 latitude of touchdown. Null when no touchdown was recorded.</summary>
    [JsonPropertyName("touchdownLat")]
    public double? TouchdownLat { get; init; }

    /// <summary>WGS-84 longitude of touchdown. Null when no touchdown was recorded.</summary>
    [JsonPropertyName("touchdownLon")]
    public double? TouchdownLon { get; init; }

    /// <summary>True heading at touchdown (degrees 0–360). Null when no touchdown was recorded.</summary>
    [JsonPropertyName("touchdownHeadingDeg")]
    public double? TouchdownHeadingDeg { get; init; }

    /// <summary>
    /// Pressure altitude (MSL, feet) at touchdown.
    /// Used to compute threshold crossing height AGL by subtracting from approach
    /// path sample altitudes. Null when no touchdown was recorded.
    /// </summary>
    [JsonPropertyName("touchdownAltFt")]
    public double? TouchdownAltFt { get; init; }
}

/// <summary>A single point in the flight path sampled every 60 seconds (blocks-off → blocks-on).</summary>
public sealed record FlightPathPointUpload
{
    [JsonPropertyName("lat")]
    public double Lat { get; init; }

    [JsonPropertyName("lon")]
    public double Lon { get; init; }

    /// <summary>Pressure altitude in feet, rounded to the nearest integer.</summary>
    [JsonPropertyName("altFt")]
    public int AltFt { get; init; }

    /// <summary>Minutes since blocks-off, rounded to one decimal place.</summary>
    [JsonPropertyName("tMin")]
    public double TMin { get; init; }
}

/// <summary>A single approach telemetry sample.</summary>
public sealed record ApproachSampleUpload
{
    /// <summary>Haversine distance from the aircraft to the arrival airport reference point (nm).</summary>
    [JsonPropertyName("distanceToThresholdNm")]
    public double DistanceToThresholdNm { get; init; }

    /// <summary>Altitude above ground level (feet).</summary>
    [JsonPropertyName("altitudeFt")]
    public double AltitudeFt { get; init; }

    /// <summary>Indicated airspeed (knots).</summary>
    [JsonPropertyName("iasKts")]
    public double IasKts { get; init; }

    /// <summary>Vertical speed (feet per minute; negative = descending).</summary>
    [JsonPropertyName("vsFpm")]
    public double VsFpm { get; init; }

    /// <summary>WGS-84 latitude at this sample.</summary>
    [JsonPropertyName("lat")]
    public double Lat { get; init; }

    /// <summary>WGS-84 longitude at this sample.</summary>
    [JsonPropertyName("lon")]
    public double Lon { get; init; }

    /// <summary>Minutes since blocks-off at this sample.</summary>
    [JsonPropertyName("tMin")]
    public double TMin { get; init; }
}
