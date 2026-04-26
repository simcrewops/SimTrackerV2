using System.Text.Json.Serialization;

namespace SimCrewOps.Sync.Models;

// ── Top-level container ────────────────────────────────────────────────────────

/// <summary>
/// Structured snapshot of every raw phase metric collected during the flight.
/// Sent as <c>scoringInput</c> in the POST /api/sim-sessions body so the webapp
/// can compute or re-compute scores without needing to know the tracker's
/// scoring algorithm.  Version suffix "V5" denotes the API contract version.
/// </summary>
public sealed record FlightScoreInputV5Upload
{
    [JsonPropertyName("preflight")]
    public ScoreInputPreflightV5 Preflight { get; init; } = new();

    [JsonPropertyName("taxiOut")]
    public ScoreInputTaxiV5 TaxiOut { get; init; } = new();

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

    [JsonPropertyName("landing")]
    public ScoreInputLandingV5 Landing { get; init; } = new();

    [JsonPropertyName("taxiIn")]
    public ScoreInputTaxiV5 TaxiIn { get; init; } = new();

    [JsonPropertyName("arrival")]
    public ScoreInputArrivalV5 Arrival { get; init; } = new();

    [JsonPropertyName("safety")]
    public ScoreInputSafetyV5 Safety { get; init; } = new();
}

// ── Phase sub-records ──────────────────────────────────────────────────────────

public sealed record ScoreInputPreflightV5
{
    [JsonPropertyName("beaconOnBeforeTaxi")]
    public bool BeaconOnBeforeTaxi { get; init; }
}

public sealed record ScoreInputTaxiV5
{
    [JsonPropertyName("maxGroundSpeedKts")]
    public double MaxGroundSpeedKts { get; init; }

    [JsonPropertyName("excessiveTurnSpeedEvents")]
    public int ExcessiveTurnSpeedEvents { get; init; }

    [JsonPropertyName("taxiLightsOn")]
    public bool TaxiLightsOn { get; init; }
}

public sealed record ScoreInputTakeoffV5
{
    [JsonPropertyName("bounces")]
    public int Bounces { get; init; }

    [JsonPropertyName("tailStrike")]
    public bool TailStrike { get; init; }

    [JsonPropertyName("maxBankDeg")]
    public double MaxBankDeg { get; init; }

    [JsonPropertyName("maxPitchDeg")]
    public double MaxPitchDeg { get; init; }

    [JsonPropertyName("maxGForce")]
    public double MaxGForce { get; init; }

    [JsonPropertyName("landingLightsOnBeforeTakeoff")]
    public bool LandingLightsOnBeforeTakeoff { get; init; }

    [JsonPropertyName("landingLightsOffByFl180")]
    public bool LandingLightsOffByFl180 { get; init; }

    [JsonPropertyName("strobesOn")]
    public bool StrobesOn { get; init; }
}

public sealed record ScoreInputClimbV5
{
    [JsonPropertyName("maxIasBelowFl100Kts")]
    public double MaxIasBelowFl100Kts { get; init; }

    [JsonPropertyName("maxBankDeg")]
    public double MaxBankDeg { get; init; }

    [JsonPropertyName("maxGForce")]
    public double MaxGForce { get; init; }
}

public sealed record ScoreInputCruiseV5
{
    [JsonPropertyName("maxAltitudeDeviationFt")]
    public double MaxAltitudeDeviationFt { get; init; }

    [JsonPropertyName("newFlightLevelCaptureSeconds")]
    public double? NewFlightLevelCaptureSeconds { get; init; }

    [JsonPropertyName("speedInstabilityEvents")]
    public int SpeedInstabilityEvents { get; init; }

    [JsonPropertyName("maxBankDeg")]
    public double MaxBankDeg { get; init; }

    [JsonPropertyName("maxGForce")]
    public double MaxGForce { get; init; }
}

public sealed record ScoreInputDescentV5
{
    [JsonPropertyName("maxIasBelowFl100Kts")]
    public double MaxIasBelowFl100Kts { get; init; }

    [JsonPropertyName("maxBankDeg")]
    public double MaxBankDeg { get; init; }

    [JsonPropertyName("maxPitchDeg")]
    public double MaxPitchDeg { get; init; }

    [JsonPropertyName("maxGForce")]
    public double MaxGForce { get; init; }

    [JsonPropertyName("landingLightsOnBy9900")]
    public bool LandingLightsOnBy9900 { get; init; }
}

public sealed record ScoreInputApproachV5
{
    [JsonPropertyName("gearDownBy1000Agl")]
    public bool GearDownBy1000Agl { get; init; }

    [JsonPropertyName("flapsIndexAt500Agl")]
    public int FlapsIndexAt500Agl { get; init; }

    [JsonPropertyName("vsAt500AglFpm")]
    public double VsAt500AglFpm { get; init; }

    [JsonPropertyName("bankAt500AglDeg")]
    public double BankAt500AglDeg { get; init; }

    [JsonPropertyName("pitchAt500AglDeg")]
    public double PitchAt500AglDeg { get; init; }

    [JsonPropertyName("gearDownAt500Agl")]
    public bool GearDownAt500Agl { get; init; }

    // ILS approach quality
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

public sealed record ScoreInputLandingV5
{
    [JsonPropertyName("bounces")]
    public int Bounces { get; init; }

    [JsonPropertyName("tdzExcessFt")]
    public double TdzExcessFt { get; init; }

    [JsonPropertyName("touchdownVsFpm")]
    public double TouchdownVsFpm { get; init; }

    [JsonPropertyName("touchdownBankDeg")]
    public double TouchdownBankDeg { get; init; }

    [JsonPropertyName("touchdownIasKts")]
    public double TouchdownIasKts { get; init; }

    [JsonPropertyName("touchdownPitchDeg")]
    public double TouchdownPitchDeg { get; init; }

    [JsonPropertyName("touchdownGForce")]
    public double TouchdownGForce { get; init; }

    [JsonPropertyName("centerlineDeviationFt")]
    public double CenterlineDeviationFt { get; init; }

    [JsonPropertyName("crabAngleDeg")]
    public double CrabAngleDeg { get; init; }

    [JsonPropertyName("touchdownLat")]
    public double TouchdownLat { get; init; }

    [JsonPropertyName("touchdownLon")]
    public double TouchdownLon { get; init; }

    [JsonPropertyName("autopilotEngaged")]
    public bool AutopilotEngaged { get; init; }

    [JsonPropertyName("spoilersDeployed")]
    public bool SpoilersDeployed { get; init; }

    [JsonPropertyName("reverseThrustUsed")]
    public bool ReverseThrustUsed { get; init; }

    [JsonPropertyName("windSpeedKts")]
    public double WindSpeedKts { get; init; }

    [JsonPropertyName("windDirectionDeg")]
    public double WindDirectionDeg { get; init; }

    [JsonPropertyName("headwindKts")]
    public double HeadwindKts { get; init; }

    [JsonPropertyName("crosswindKts")]
    public double CrosswindKts { get; init; }

    [JsonPropertyName("oatCelsius")]
    public double OatCelsius { get; init; }

    [JsonPropertyName("touchdownHeadingDeg")]
    public double TouchdownHeadingDeg { get; init; }

    [JsonPropertyName("distanceFromThresholdFt")]
    public double DistanceFromThresholdFt { get; init; }

    [JsonPropertyName("speedAtThresholdKts")]
    public double SpeedAtThresholdKts { get; init; }

    [JsonPropertyName("thresholdCrossingHeightFt")]
    public double ThresholdCrossingHeightFt { get; init; }
}

public sealed record ScoreInputArrivalV5
{
    [JsonPropertyName("taxiLightsOffBeforeParkingBrakeSet")]
    public bool TaxiLightsOffBeforeParkingBrakeSet { get; init; }

    [JsonPropertyName("allEnginesOffBeforeParkingBrakeSet")]
    public bool AllEnginesOffBeforeParkingBrakeSet { get; init; }

    [JsonPropertyName("allEnginesOffByEndOfSession")]
    public bool AllEnginesOffByEndOfSession { get; init; }
}

public sealed record ScoreInputSafetyV5
{
    [JsonPropertyName("crashDetected")]
    public bool CrashDetected { get; init; }

    [JsonPropertyName("overspeedEvents")]
    public int OverspeedEvents { get; init; }

    [JsonPropertyName("sustainedOverspeedEvents")]
    public int SustainedOverspeedEvents { get; init; }

    [JsonPropertyName("stallEvents")]
    public int StallEvents { get; init; }

    [JsonPropertyName("gpwsEvents")]
    public int GpwsEvents { get; init; }

    [JsonPropertyName("engineShutdownsInFlight")]
    public int EngineShutdownsInFlight { get; init; }
}

// ── Landing analysis ───────────────────────────────────────────────────────────

/// <summary>
/// Rich landing geometry object sent alongside the standard touchdown metrics.
/// Enables the webapp debrief page to show runway overlay, approach profile,
/// and threshold-crossing data without requiring additional API calls.
/// </summary>
public sealed record LandingAnalysisUpload
{
    /// <summary>Aircraft true heading at the moment of touchdown (degrees).</summary>
    [JsonPropertyName("touchdownHeadingDeg")]
    public double TouchdownHeadingDeg { get; init; }

    /// <summary>ICAO runway designator (e.g. "28L", "18R").</summary>
    [JsonPropertyName("runwayDesignator")]
    public string? RunwayDesignator { get; init; }

    /// <summary>
    /// GPS track points recorded during the Approach phase only.
    /// Subset of the full GPS track filtered by phase, useful for rendering
    /// the approach path on a runway diagram.
    /// Null when no approach-phase GPS points were recorded.
    /// </summary>
    [JsonPropertyName("approachPath")]
    public IReadOnlyList<GpsTrackPointUpload>? ApproachPath { get; init; }

    /// <summary>Distance from the runway threshold to the touchdown point (feet).</summary>
    [JsonPropertyName("touchdownDistanceFromThresholdFt")]
    public double TouchdownDistanceFromThresholdFt { get; init; }

    /// <summary>Indicated airspeed when the aircraft crossed the runway threshold (knots).</summary>
    [JsonPropertyName("speedAtThreshold")]
    public double SpeedAtThreshold { get; init; }

    /// <summary>Altitude AGL when the aircraft crossed the runway threshold (feet).</summary>
    [JsonPropertyName("thresholdCrossingHeightFt")]
    public double ThresholdCrossingHeightFt { get; init; }
}
