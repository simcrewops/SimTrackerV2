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
            VelocityWorldYFps = rawFrame.VelocityWorldYFps,
            TouchdownNormalVelocityFps = rawFrame.TouchdownNormalVelocityFps,
            BankAngleDegrees = rawFrame.BankAngleDegrees,
            // SimConnect's PLANE PITCH DEGREES convention: negative = nose UP, positive = nose DOWN.
            // Negate here so downstream scoring uses the user-facing convention (positive = nose UP).
            PitchAngleDegrees = -rawFrame.PitchAngleDegrees,
            HeadingMagneticDegrees = rawFrame.HeadingMagneticDegrees,
            HeadingTrueDegrees = rawFrame.HeadingTrueDegrees,
            GForce = rawFrame.GForce,
            OnGround = ToBool(rawFrame.OnGround),
            // BRAKE PARKING POSITION normally returns 0–100, but some complex aircraft
            // (Fenix A320, etc.) return 0 or 1 instead of 0 or 100.  Treat any non-zero
            // value as "set" so both scales work correctly.
            ParkingBrakeSet = rawFrame.ParkingBrakePosition > 0.0,
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
            WindSpeedKnots = rawFrame.WindSpeedKnots,
            WindDirectionDegrees = rawFrame.WindDirectionDegrees,
            Engine1Running = ToBool(rawFrame.Engine1Running),
            Engine2Running = ToBool(rawFrame.Engine2Running),
            Engine3Running = ToBool(rawFrame.Engine3Running),
            Engine4Running = ToBool(rawFrame.Engine4Running),
        };
    }

    private static bool ToBool(double value) => value >= 0.5;
}
