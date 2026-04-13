namespace SimCrewOps.SimConnect.Models;

public sealed record SimConnectRawTelemetryFrame
{
    public DateTimeOffset TimestampUtc { get; init; }

    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public double AltitudeAglFeet { get; init; }
    public double IndicatedAltitudeFeet { get; init; }
    public double IndicatedAirspeedKnots { get; init; }
    public double TrueAirspeedKnots { get; init; }
    public double Mach { get; init; }
    public double GroundSpeedKnots { get; init; }
    public double VerticalSpeedFpm { get; init; }
    public double BankAngleDegrees { get; init; }
    public double PitchAngleDegrees { get; init; }
    public double HeadingTrueDegrees { get; init; }
    public double GForce { get; init; }

    public double ParkingBrakePosition { get; init; }
    public double OnGround { get; init; }
    public double CrashFlag { get; init; }
    public double FlapsHandleIndex { get; init; }
    public double GearPosition { get; init; }

    public double BeaconLightOn { get; init; }
    public double TaxiLightsOn { get; init; }
    public double LandingLightsOn { get; init; }
    public double StrobesOn { get; init; }

    public double StallWarning { get; init; }
    public double GpwsAlert { get; init; }
    public double OverspeedWarning { get; init; }

    public double Engine1Running { get; init; }
    public double Engine2Running { get; init; }
    public double Engine3Running { get; init; }
    public double Engine4Running { get; init; }
}
