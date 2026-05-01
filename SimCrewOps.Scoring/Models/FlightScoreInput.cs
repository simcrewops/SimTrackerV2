namespace SimCrewOps.Scoring.Models;

public sealed record FlightScoreInput
{
    public PreflightMetrics Preflight { get; init; } = new();
    public TaxiMetrics TaxiOut { get; init; } = new();
    public TakeoffMetrics Takeoff { get; init; } = new();
    public ClimbMetrics Climb { get; init; } = new();
    public CruiseMetrics Cruise { get; init; } = new();
    public DescentMetrics Descent { get; init; } = new();
    public ApproachMetrics Approach { get; init; } = new();
    public StabilizedApproachMetrics StabilizedApproach { get; init; } = new();
    public LandingMetrics Landing { get; init; } = new();
    public TaxiInMetrics TaxiIn { get; init; } = new();
    public ArrivalMetrics Arrival { get; init; } = new();
    public LightsSystemsMetrics LightsSystems { get; init; } = new();
    public SafetyMetrics Safety { get; init; } = new();
    public LandingAnalysisData LandingAnalysis { get; init; } = new();
    public IReadOnlyList<FlightPathPoint> FlightPath { get; init; } = [];
    public IReadOnlyList<ApproachPathPoint> ApproachPath { get; init; } = [];
    /// <summary>
    /// A/B instrumentation: multiple touchdown FPM candidates captured at first touchdown.
    /// Persisted locally for post-flight comparison. Not included in the upload payload.
    /// Null until a touchdown is recorded.
    /// </summary>
    public TouchdownRateCandidates? TouchdownRateCandidates { get; init; }
}

public sealed record PreflightMetrics
{
    public bool BeaconOnBeforeTaxi { get; init; }
}

public record TaxiMetrics
{
    public double MaxGroundSpeedKnots { get; init; }
    public double MaxTurnSpeedKnots { get; init; }
    public int ExcessiveTurnSpeedEvents { get; init; }
    public bool TaxiLightsOn { get; init; }
    public bool NavLightsOn { get; init; }
    /// <summary>True when strobes were correctly off throughout taxi. False = strobes on during taxi (violation).</summary>
    public bool StrobesOff { get; init; } = true;
}

public sealed record TakeoffMetrics
{
    public int BounceCount { get; init; }
    public bool TailStrikeDetected { get; init; }
    /// <summary>Max bank angle below 1000 ft AGL during takeoff roll/rotation only.</summary>
    public double MaxBankAngleDegrees { get; init; }
    public double MaxPitchAngleDegrees { get; init; }
    public double MaxGForce { get; init; }
    public bool LandingLightsOnBeforeTakeoff { get; init; }
    public bool LandingLightsOffByFl180 { get; init; }
    public bool StrobesOnFromTakeoffToLanding { get; init; }
    public int FlapsHandleIndexAtLiftoff { get; init; }
    public double InitialClimbFpm { get; init; }
    public bool PositiveRateBeforeGearUp { get; init; }
    /// <summary>Max pitch while WOW=true during takeoff phase only (for tail-strike detection).</summary>
    public double MaxPitchWhileWowDegrees { get; init; }
    /// <summary>Radar AGL (ft) at the tick where MaxPitchWhileWowDegrees was recorded.</summary>
    public double MaxPitchAglFt { get; init; }
    public double GForceAtRotation { get; init; }
}

public sealed record ClimbMetrics
{
    public bool HeavyFourEngineAircraft { get; init; }
    public double MaxIasBelowFl100Knots { get; init; }
    public double MaxBankAngleDegrees { get; init; }
    public double MaxGForce { get; init; }
    public double MinGForce { get; init; } = 1.0;
    public double AvgClimbFpm { get; init; }
    public double? TimeToFL100Minutes { get; init; }
    public double VsStabilityScore { get; init; }
}

public sealed record CruiseMetrics
{
    public double? CruiseTargetAltitudeFt { get; init; }
    public double MaxAltitudeDeviationFeet { get; init; }
    public double? NewFlightLevelCaptureSeconds { get; init; }
    public int SpeedInstabilityEvents { get; init; }
    public double? MachTarget { get; init; }
    public double MaxMachDeviation { get; init; }
    public double? IasTarget { get; init; }
    public double MaxSpeedDeviationKts { get; init; }
    public double LevelMaxBankDegrees { get; init; }
    public double TurnMaxBankDegrees { get; init; }
    public double MaxGForce { get; init; }
    public double MinGForce { get; init; } = 1.0;
}

public sealed record DescentMetrics
{
    public double MaxIasBelowFl100Knots { get; init; }
    public double MaxBankAngleDegrees { get; init; }
    public double MaxPitchAngleDegrees { get; init; }
    public double MaxGForce { get; init; }
    public double MinGForce { get; init; } = 1.0;
    public bool LandingLightsOnByFl180 { get; init; }
    public bool LandingLightsOnBy9900 { get; init; }
    public double AvgDescentFpm { get; init; }
    public double? SpeedAtFL100Kts { get; init; }
    public double MaxDescentRateFpm { get; init; }
    public double MaxNoseDownPitchDeg { get; init; }
}

public sealed record ApproachMetrics
{
    public bool GearDownBy1000Agl { get; init; }
    public double? GearDownAglFt { get; init; }
    public double ApproachSpeedKts { get; init; }
    public double MaxIasDeviationKts { get; init; }
    public bool FlapsConfiguredBy1000Agl { get; init; }
    public double MaxBankDegrees { get; init; }
    public int FlapsHandleIndexAt500Agl { get; init; }
    public double VerticalSpeedAt500AglFpm { get; init; }
    public double BankAngleAt500AglDegrees { get; init; }
    public double PitchAngleAt500AglDegrees { get; init; }
    public bool GearDownAt500Agl { get; init; }
}

public sealed record StabilizedApproachMetrics
{
    public double ApproachSpeedKts { get; init; }
    public double MaxIasDeviationKts { get; init; }
    public double MaxDescentRateFpm { get; init; }
    public bool ConfigChanged { get; init; }
    public double MaxHeadingDeviationDeg { get; init; }
    public bool IlsAvailable { get; init; }
    public double MaxGlideslopeDevDots { get; init; }
    public double PitchAtGateDeg { get; init; }
}

public sealed record LandingMetrics
{
    public double TouchdownZoneExcessDistanceFeet { get; init; }
    public double TouchdownVerticalSpeedFpm { get; init; }
    public double TouchdownBankAngleDegrees { get; init; }
    public double TouchdownIndicatedAirspeedKnots { get; init; }
    public double TouchdownPitchAngleDegrees { get; init; }
    public double MaxPitchWhileWowDegrees { get; init; }
    public double TouchdownGForce { get; init; }
    public int BounceCount { get; init; }
    public bool GearUpAtTouchdown { get; init; }
    public double TouchdownCenterlineDeviationFeet { get; init; }
    public double TouchdownCrabAngleDegrees { get; init; }
}

public sealed record TaxiInMetrics : TaxiMetrics
{
    public bool LandingLightsOff { get; init; }
    public bool SmoothDeceleration { get; init; } = true;
}

public sealed record ArrivalMetrics
{
    public bool ArrivalReached { get; init; }
    public bool TaxiLightsOffBeforeParkingBrakeSet { get; init; }
    public bool ParkingBrakeSetBeforeAllEnginesShutdown { get; init; }
    public bool AllEnginesOffBeforeParkingBrakeSet { get; init; }
    public bool AllEnginesOffByEndOfSession { get; init; }
    public bool BeaconOffAfterEngines { get; init; }
}

public sealed record LightsSystemsMetrics
{
    public bool BeaconOnThroughoutFlight { get; init; } = true;
    public bool NavLightsOnThroughoutFlight { get; init; } = true;
    public bool StrobesCorrect { get; init; } = true;
    public double LandingLightsCompliance { get; init; } = 1.0;
}

public sealed record SafetyMetrics
{
    public bool CrashDetected { get; init; }
    public int OverspeedEvents { get; init; }
    public int SustainedOverspeedEvents { get; init; }
    public int StallEvents { get; init; }
    public int GpwsEvents { get; init; }
    public int EngineShutdownsInFlight { get; init; }
}

public sealed record LandingAnalysisData
{
    public double? TouchdownLat { get; init; }
    public double? TouchdownLon { get; init; }
    public double? TouchdownHeadingMagneticDeg { get; init; }
    public double? TouchdownAltFt { get; init; }
    public double? TouchdownIAS { get; init; }
    public double? WindSpeedKnots { get; init; }
    public double? WindDirectionDegrees { get; init; }
}

public sealed record FlightPathPoint
{
    public double Lat { get; init; }
    public double Lon { get; init; }
    public double AltFt { get; init; }
    public double TMin { get; init; }
}

public sealed record ApproachPathPoint
{
    public double Lat { get; init; }
    public double Lon { get; init; }
    public double AltFt { get; init; }
    public double IasKts { get; init; }
    public double VsFpm { get; init; }
}

/// <summary>
/// A/B instrumentation: multiple touchdown FPM calculation candidates captured at first touchdown.
/// Stored in local session JSON only — not sent in the upload payload.
/// FPM fields are positive magnitudes (0 = not available); raw fps fields are signed (negative = descending).
/// </summary>
public sealed record TouchdownRateCandidates
{
    /// <summary>VELOCITY WORLD Y on the touchdown frame, converted to fpm magnitude.</summary>
    public double FpmVelocityWorldY { get; init; }
    /// <summary>PLANE TOUCHDOWN NORMAL VELOCITY on the touchdown frame, converted to fpm magnitude.</summary>
    public double FpmTouchdownNormal { get; init; }
    /// <summary>VERTICAL SPEED (barometric) on the touchdown frame, fpm magnitude.</summary>
    public double FpmVerticalSpeed { get; init; }
    /// <summary>The value selected by CalculateTouchdownVerticalSpeed() as the authoritative rate.</summary>
    public double FinalSelected { get; init; }

    /// <summary>VELOCITY WORLD Y on the last airborne frame (frame before OnGround flip), fpm magnitude.</summary>
    public double FpmVelocityWorldYLastAirborne { get; init; }
    /// <summary>VERTICAL SPEED on the last airborne frame, fpm magnitude.</summary>
    public double FpmVerticalSpeedLastAirborne { get; init; }
    /// <summary>Human-readable label identifying which fallback path CalculateTouchdownVerticalSpeed chose.</summary>
    public string SelectedSourceLabel { get; init; } = string.Empty;
    /// <summary>Raw signed fps from PLANE TOUCHDOWN NORMAL VELOCITY on the touchdown frame (negative = descending).</summary>
    public double RawTouchdownNormalVelocityFpsTouchdownFrame { get; init; }
    /// <summary>
    /// Raw signed fps from PLANE TOUCHDOWN NORMAL VELOCITY at the first non-zero reading within a
    /// 2-second post-touchdown window. Null when the TD-frame value was already non-zero, or the window expired with no reading.
    /// </summary>
    public double? RawTouchdownNormalVelocityFpsFirstNonZero { get; init; }
}
