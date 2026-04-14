using SimCrewOps.Scoring.Models;

namespace SimCrewOps.Tracking.Models;

public sealed record TelemetryFrame
{
    public DateTimeOffset TimestampUtc { get; init; }
    public FlightPhase Phase { get; init; }

    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public double AltitudeFeet { get; init; }
    public double IndicatedAltitudeFeet { get; init; }
    public double AltitudeAglFeet { get; init; }
    public double IndicatedAirspeedKnots { get; init; }
    public double TrueAirspeedKnots { get; init; }
    public double Mach { get; init; }
    public double GroundSpeedKnots { get; init; }
    public double VerticalSpeedFpm { get; init; }
    public double BankAngleDegrees { get; init; }
    public double PitchAngleDegrees { get; init; }
    public double HeadingMagneticDegrees { get; init; }
    public double HeadingTrueDegrees { get; init; }
    public double GForce { get; init; }

    public bool OnGround { get; init; }
    public bool ParkingBrakeSet { get; init; }
    public bool GearDown { get; init; }
    public int FlapsHandleIndex { get; init; }

    public bool BeaconLightOn { get; init; }
    public bool TaxiLightsOn { get; init; }
    public bool LandingLightsOn { get; init; }
    public bool StrobesOn { get; init; }

    public bool CrashDetected { get; init; }
    public bool StallWarning { get; init; }
    public bool GpwsAlert { get; init; }
    public bool OverspeedWarning { get; init; }

    public bool Engine1Running { get; init; }
    public bool Engine2Running { get; init; }
    public bool Engine3Running { get; init; }
    public bool Engine4Running { get; init; }

    public double? TouchdownZoneExcessDistanceFeet { get; init; }
    // Reserved for future gate-arrival precision scoring.
    public double? GateArrivalDistanceFeet { get; init; }
}
