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

    // ── Session-level data ─────────────────────────────────────────────────
    public SessionMetrics Session { get; init; } = new();

    /// <summary>
    /// Downsampled GPS track for flight-path replay (one point every ~30 seconds).
    /// </summary>
    public IReadOnlyList<GpsTrackPoint> GpsTrack { get; init; } = [];
}

public sealed record SessionMetrics
{
    /// <summary>UTC timestamp when the first engine was started.</summary>
    public DateTimeOffset? EnginesStartedAtUtc { get; init; }

    /// <summary>UTC timestamp of wheels-off (liftoff).</summary>
    public DateTimeOffset? WheelsOffAtUtc { get; init; }

    /// <summary>UTC timestamp of wheels-on (touchdown).</summary>
    public DateTimeOffset? WheelsOnAtUtc { get; init; }

    /// <summary>UTC timestamp when all engines were shut down.</summary>
    public DateTimeOffset? EnginesOffAtUtc { get; init; }

    /// <summary>Total usable fuel weight in pounds at the start of taxi out.</summary>
    public double FuelAtDepartureLbs { get; init; }

    /// <summary>Total usable fuel weight in pounds at touchdown.</summary>
    public double FuelAtLandingLbs { get; init; }

    /// <summary>Fuel burned during the flight in pounds (departure minus landing).</summary>
    public double FuelBurnedLbs => FuelAtDepartureLbs > 0 && FuelAtLandingLbs > 0
        ? FuelAtDepartureLbs - FuelAtLandingLbs
        : 0;
}

/// <summary>A single point in the downsampled GPS track.</summary>
public sealed record GpsTrackPoint
{
    public DateTimeOffset TimestampUtc { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    /// <summary>Pressure altitude in feet.</summary>
    public double AltitudeFeet { get; init; }
    public double GroundSpeedKnots { get; init; }
    public FlightPhase Phase { get; init; }
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
    /// <summary>
    /// True when landing lights were on by the time the aircraft descended through 9,900 ft.
    /// Replaces the old FL180 check — lights must be on by FL100, not FL180.
    /// </summary>
    public bool LandingLightsOnBy9900 { get; init; }
}

public sealed record ApproachMetrics
{
    public bool GearDownBy1000Agl { get; init; }
    public int FlapsHandleIndexAt500Agl { get; init; }
    public double VerticalSpeedAt500AglFpm { get; init; }
    public double BankAngleAt500AglDegrees { get; init; }
    public double PitchAngleAt500AglDegrees { get; init; }
    public bool GearDownAt500Agl { get; init; }

    // ── ILS approach quality ───────────────────────────────────────────────
    /// <summary>True when a valid ILS signal was received during the approach.</summary>
    public bool IlsApproachDetected { get; init; }

    /// <summary>Maximum absolute glideslope deviation in CDI dot units during the approach.</summary>
    public double MaxGlideslopeDeviationDots { get; init; }

    /// <summary>Average absolute glideslope deviation in CDI dot units during the approach.</summary>
    public double AvgGlideslopeDeviationDots { get; init; }

    /// <summary>Maximum absolute localizer deviation in CDI dot units during the approach.</summary>
    public double MaxLocalizerDeviationDots { get; init; }

    /// <summary>Average absolute localizer deviation in CDI dot units during the approach.</summary>
    public double AvgLocalizerDeviationDots { get; init; }
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
    public double TouchdownCenterlineDeviationFeet { get; init; }
    public double TouchdownCrabAngleDegrees { get; init; }

    /// <summary>
    /// WGS-84 latitude of the initial wheel contact point, in decimal degrees.
    /// Zero when no touchdown was recorded (e.g. session ended in the air).
    /// </summary>
    public double TouchdownLatitude { get; init; }

    /// <summary>
    /// WGS-84 longitude of the initial wheel contact point, in decimal degrees.
    /// Zero when no touchdown was recorded (e.g. session ended in the air).
    /// </summary>
    public double TouchdownLongitude { get; init; }

    // ── Extended touchdown context ─────────────────────────────────────────
    /// <summary>True when the autopilot master was engaged at the moment of touchdown.</summary>
    public bool AutopilotEngagedAtTouchdown { get; init; }

    /// <summary>True when spoilers were armed or deployed at touchdown.</summary>
    public bool SpoilersDeployedAtTouchdown { get; init; }

    /// <summary>True when reverse thrust was applied during the landing rollout.</summary>
    public bool ReverseThrustUsed { get; init; }

    /// <summary>Ambient wind speed in knots at the time of touchdown.</summary>
    public double WindSpeedAtTouchdownKnots { get; init; }

    /// <summary>Ambient wind direction in degrees magnetic at the time of touchdown.</summary>
    public double WindDirectionAtTouchdownDegrees { get; init; }

    /// <summary>
    /// Headwind component in knots at touchdown (positive = headwind, negative = tailwind).
    /// Computed from wind direction relative to runway heading.
    /// Zero when runway heading is unavailable.
    /// </summary>
    public double HeadwindComponentKnots { get; init; }

    /// <summary>
    /// Crosswind component in knots at touchdown (positive = from the right).
    /// Zero when runway heading is unavailable.
    /// </summary>
    public double CrosswindComponentKnots { get; init; }

    /// <summary>Outside air temperature in degrees Celsius at touchdown.</summary>
    public double OatCelsiusAtTouchdown { get; init; }
}

public sealed record TaxiInMetrics : TaxiMetrics
{
    public bool LandingLightsOff { get; init; }
    public bool StrobesOff { get; init; }
}

public sealed record ArrivalMetrics
{
    public bool TaxiLightsOffBeforeParkingBrakeSet { get; init; }
    /// <summary>
    /// True when all engines were already off before the parking brake was set.
    /// SOPs require engines shutdown first, then parking brake.
    /// </summary>
    public bool AllEnginesOffBeforeParkingBrakeSet { get; init; }
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
