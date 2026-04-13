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
    public LandingMetrics Landing { get; init; } = new();
    public TaxiInMetrics TaxiIn { get; init; } = new();
    public ArrivalMetrics Arrival { get; init; } = new();
    public SafetyMetrics Safety { get; init; } = new();
}

public sealed record PreflightMetrics
{
    public bool BeaconOnBeforeTaxi { get; init; }
}

public record TaxiMetrics
{
    public double MaxGroundSpeedKnots { get; init; }
    public int ExcessiveTurnSpeedEvents { get; init; }
    public bool TaxiLightsOn { get; init; }
}

public sealed record TakeoffMetrics
{
    public int BounceCount { get; init; }
    public bool TailStrikeDetected { get; init; }
    public double MaxBankAngleDegrees { get; init; }
    public double MaxPitchAngleDegrees { get; init; }
    public double MaxGForce { get; init; }
    public bool LandingLightsOnBeforeTakeoff { get; init; }
    public bool LandingLightsOffByFl180 { get; init; }
    public bool StrobesOnFromTakeoffToLanding { get; init; }
}

public sealed record ClimbMetrics
{
    public bool HeavyFourEngineAircraft { get; init; }
    public double MaxIasBelowFl100Knots { get; init; }
    public double MaxBankAngleDegrees { get; init; }
    public double MaxGForce { get; init; }
}

public sealed record CruiseMetrics
{
    public double MaxAltitudeDeviationFeet { get; init; }
    public double? NewFlightLevelCaptureSeconds { get; init; }
    public int SpeedInstabilityEvents { get; init; }
    public double MaxBankAngleDegrees { get; init; }
    public double MaxGForce { get; init; }
}

public sealed record DescentMetrics
{
    public double MaxIasBelowFl100Knots { get; init; }
    public double MaxBankAngleDegrees { get; init; }
    public double MaxPitchAngleDegrees { get; init; }
    public double MaxGForce { get; init; }
    public bool LandingLightsOnByFl180 { get; init; }
}

public sealed record ApproachMetrics
{
    public bool GearDownBy1000Agl { get; init; }
    public int FlapsHandleIndexAt500Agl { get; init; }
    public double VerticalSpeedAt500AglFpm { get; init; }
    public double BankAngleAt500AglDegrees { get; init; }
    public double PitchAngleAt500AglDegrees { get; init; }
    public bool GearDownAt500Agl { get; init; }
}

public sealed record LandingMetrics
{
    public double TouchdownZoneExcessDistanceFeet { get; init; }
    public double TouchdownVerticalSpeedFpm { get; init; }
    public double TouchdownBankAngleDegrees { get; init; }
    public double TouchdownIndicatedAirspeedKnots { get; init; }
    public double TouchdownPitchAngleDegrees { get; init; }
    public double TouchdownGForce { get; init; }
    public int BounceCount { get; init; }
}

public sealed record TaxiInMetrics : TaxiMetrics
{
    public bool LandingLightsOff { get; init; }
    public bool StrobesOff { get; init; }
}

public sealed record ArrivalMetrics
{
    public bool TaxiLightsOffBeforeParkingBrakeSet { get; init; }
    public bool ParkingBrakeSetBeforeAllEnginesShutdown { get; init; }
    public bool AllEnginesOffByEndOfSession { get; init; }
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
