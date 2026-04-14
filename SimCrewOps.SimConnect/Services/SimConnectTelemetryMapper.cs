using SimCrewOps.Scoring.Models;
using SimCrewOps.SimConnect.Models;
using SimCrewOps.Tracking.Models;

namespace SimCrewOps.SimConnect.Services;

public sealed class SimConnectTelemetryMapper
{
    public TelemetryFrame Map(SimConnectRawTelemetryFrame rawFrame)
    {
        ArgumentNullException.ThrowIfNull(rawFrame);

        return new TelemetryFrame
        {
            TimestampUtc = rawFrame.TimestampUtc,
            Phase = FlightPhase.Preflight,
            Latitude = rawFrame.Latitude,
            Longitude = rawFrame.Longitude,
            AltitudeFeet = rawFrame.AltitudeFeet,
            IndicatedAltitudeFeet = rawFrame.IndicatedAltitudeFeet,
            AltitudeAglFeet = rawFrame.AltitudeAglFeet,
            IndicatedAirspeedKnots = rawFrame.IndicatedAirspeedKnots,
            TrueAirspeedKnots = rawFrame.TrueAirspeedKnots,
            Mach = rawFrame.Mach,
            GroundSpeedKnots = rawFrame.GroundSpeedKnots,
            VerticalSpeedFpm = rawFrame.VerticalSpeedFpm,
            BankAngleDegrees = rawFrame.BankAngleDegrees,
            PitchAngleDegrees = rawFrame.PitchAngleDegrees,
            HeadingMagneticDegrees = rawFrame.HeadingMagneticDegrees,
            HeadingTrueDegrees = rawFrame.HeadingTrueDegrees,
            GForce = rawFrame.GForce,
            OnGround = ToBool(rawFrame.OnGround),
            ParkingBrakeSet = ToBool(rawFrame.ParkingBrakePosition),
            GearDown = rawFrame.GearPosition >= 0.5,
            GearPosition = rawFrame.GearPosition,
            FlapsHandleIndex = (int)Math.Round(rawFrame.FlapsHandleIndex, MidpointRounding.AwayFromZero),
            BeaconLightOn = ToBool(rawFrame.BeaconLightOn),
            TaxiLightsOn = ToBool(rawFrame.TaxiLightsOn),
            LandingLightsOn = ToBool(rawFrame.LandingLightsOn),
            StrobesOn = ToBool(rawFrame.StrobesOn),
            CrashDetected = ToBool(rawFrame.CrashFlag),
            StallWarning = ToBool(rawFrame.StallWarning),
            GpwsAlert = ToBool(rawFrame.GpwsAlert),
            OverspeedWarning = ToBool(rawFrame.OverspeedWarning),
            Engine1Running = ToBool(rawFrame.Engine1Running),
            Engine2Running = ToBool(rawFrame.Engine2Running),
            Engine3Running = ToBool(rawFrame.Engine3Running),
            Engine4Running = ToBool(rawFrame.Engine4Running),
        };
    }

    private static bool ToBool(double value) => value >= 0.5;
}
