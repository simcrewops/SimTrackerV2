namespace SimCrewOps.SimConnect.Models;

public sealed record SimConnectRawTelemetryFrame
{
    public DateTimeOffset TimestampUtc { get; init; }
    public bool HasFlightCriticalData { get; init; }
    public bool HasOperationalData { get; init; }

    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public double AltitudeAglFeet { get; init; }
    public double AltitudeFeet { get; init; }
    public double IndicatedAltitudeFeet { get; init; }
    public double IndicatedAirspeedKnots { get; init; }
    public double TrueAirspeedKnots { get; init; }
    public double Mach { get; init; }
    public double GroundSpeedKnots { get; init; }
    public double VerticalSpeedFpm { get; init; }
    /// <summary>
    /// Physics-engine vertical velocity in ft/s (negative = descending, no barometric lag).
    /// Sourced from VELOCITY WORLD Y.
    /// </summary>
    public double VelocityWorldYFps { get; init; }
    public double BankAngleDegrees { get; init; }
    public double PitchAngleDegrees { get; init; }
    public double HeadingMagneticDegrees { get; init; }
    public double HeadingTrueDegrees { get; init; }
    public double GForce { get; init; }

    public double ParkingBrakePosition { get; init; }  // BRAKE PARKING POSITION: 0–100 (0=released, 100=fully set)
    public double OnGround { get; init; }
    public double CrashFlag { get; init; }
    public double FlapsHandleIndex { get; init; }
    public double GearPosition { get; init; }   // GEAR HANDLE POSITION: 0.0 = up, 1.0 = down

    // Final decoded light values (individual SimVar preferred; bitmask fallback)
    public double BeaconLightOn { get; init; }
    public double TaxiLightsOn { get; init; }
    public double LandingLightsOn { get; init; }
    public double StrobesOn { get; init; }

    // Raw values before fallback logic — for diagnostics
    public int LightStatesRaw { get; init; }          // LIGHT STATES bitmask integer
    public int LightBeaconRaw { get; init; }          // LIGHT BEACON individual SimVar
    public int LightTaxiRaw { get; init; }            // LIGHT TAXI individual SimVar
    public int LightLandingRaw { get; init; }         // LIGHT LANDING individual SimVar
    public int LightStrobeRaw { get; init; }          // LIGHT STROBE individual SimVar
    public bool LightSourceIsIndividual { get; init; } // true = used individual vars, false = used bitmask

    public double WindSpeedKnots { get; init; }
    public double WindDirectionDegrees { get; init; }

    public double StallWarning { get; init; }
    public double GpwsAlert { get; init; }
    public double OverspeedWarning { get; init; }

    public double Engine1Running { get; init; }
    public double Engine2Running { get; init; }
    public double Engine3Running { get; init; }
    public double Engine4Running { get; init; }

    // Aircraft profile — set on connect when SimConnect returns the loaded aircraft path.
    public string ActiveProfileName   { get; init; } = "Default (Standard SimVars)";
    public string? ActiveProfileIcaoType { get; init; }
    /// <summary>True when the matched profile needs LVARs that require a MobiFlight WASM bridge.</summary>
    public bool LvarBridgeRequired  { get; init; }
    /// <summary>True when the MobiFlight WASM bridge is connected and serving LVAR values.</summary>
    public bool LvarBridgeConnected { get; init; }

    /// <summary>
    /// Friendly aircraft title parsed from the MSFS AircraftLoaded path.
    /// e.g. "Community\fenix-a319\..." → "fenix-a319". Null until first AircraftLoaded event.
    /// </summary>
    public string? AircraftTitle { get; init; }
}
