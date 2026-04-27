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
            BankAngleDegrees = rawFrame.BankAngleDegrees,
            PitchAngleDegrees = rawFrame.PitchAngleDegrees,
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
            Engine1Running = ToBool(rawFrame.Engine1Running),
            Engine2Running = ToBool(rawFrame.Engine2Running),
            Engine3Running = ToBool(rawFrame.Engine3Running),
            Engine4Running = ToBool(rawFrame.Engine4Running),
            // Convert degree deviations to CDI dot units.
            // ILS glideslope standard: 1 dot ≈ 0.35° (full-scale ±2.5 dots at ±0.875°).
            // ILS localizer standard:  1 dot ≈ 0.5°  (full-scale ±2.5 dots at ±1.25°).
            // Clamp to ±2.5 dots (instrument full-scale deflection).
            Nav1GlideslopeErrorDots = Math.Clamp(rawFrame.Nav1GlideslopeErrorDegrees / 0.35, -2.5, 2.5),
            Nav1LocalizerErrorDots  = Math.Clamp(rawFrame.Nav1RadialErrorDegrees / 0.5, -2.5, 2.5),
            Nav1IlsSignalValid = ToBool(rawFrame.Nav1IlsSignalValid),
            // Extended context
            AutopilotEngaged         = ToBool(rawFrame.AutopilotMaster),
            FuelTotalLbs             = rawFrame.FuelTotalLbs,
            WindSpeedKnots           = rawFrame.AmbientWindSpeedKnots,
            WindDirectionDegrees     = rawFrame.AmbientWindDirectionDegrees,
            OutsideAirTempCelsius    = rawFrame.AmbientTemperatureCelsius,
            SpoilerHandlePosition    = rawFrame.SpoilerHandlePosition,
            SpoilersArmed            = ToBool(rawFrame.SpoilersArmed),
            Engine1N1Pct             = rawFrame.Engine1N1Pct,
            Engine2N1Pct             = rawFrame.Engine2N1Pct,
            Engine3N1Pct             = rawFrame.Engine3N1Pct,
            Engine4N1Pct             = rawFrame.Engine4N1Pct,
        };
    }

    private static bool ToBool(double value) => value >= 0.5;
}
