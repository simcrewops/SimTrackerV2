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

    public double StallWarning { get; init; }
    public double GpwsAlert { get; init; }
    public double OverspeedWarning { get; init; }

    public double Engine1Running { get; init; }
    public double Engine2Running { get; init; }
    public double Engine3Running { get; init; }
    public double Engine4Running { get; init; }

    // Aircraft profile — set on connect when SimConnect returns the loaded aircraft path.
    public string ActiveProfileName   { get; init; } = "Default (Standard SimVars)";
    /// <summary>True when the matched profile needs LVARs that require a MobiFlight WASM bridge.</summary>
    public bool LvarBridgeRequired  { get; init; }
    /// <summary>True when the MobiFlight WASM bridge is connected and serving LVAR values.</summary>
    public bool LvarBridgeConnected { get; init; }

    /// <summary>
    /// Friendly aircraft title parsed from the MSFS AircraftLoaded path.
    /// e.g. "Community\fenix-a319\..." → "fenix-a319". Null until first AircraftLoaded event.
    /// </summary>
    public string? AircraftTitle { get; init; }

    /// <summary>NAV1 glideslope deviation in degrees. Positive = above glidepath, negative = below.</summary>
    public double Nav1GlideslopeErrorDegrees { get; init; }

    /// <summary>NAV1 radial/LOC deviation in degrees. Positive = right of centreline, negative = left.</summary>
    public double Nav1RadialErrorDegrees { get; init; }

    /// <summary>
    /// Destination airport ICAO from the active GPS flight plan (e.g. "KLAX").
    /// Null when no flight plan is loaded or the SimVar returns an empty / invalid value.
    /// </summary>
    public string? GpsDestinationIdent { get; init; }

    // ── Extended context SimVars ───────────────────────────────────────────────
    // All are optional (zero/false when the SimVar is unsupported or not yet received).

    /// <summary>Autopilot master switch state (1.0 = engaged, 0.0 = off).</summary>
    public double AutopilotMaster { get; init; }

    /// <summary>Total usable fuel weight in pounds.</summary>
    public double FuelTotalLbs { get; init; }

    /// <summary>Ambient wind speed at the aircraft's current position, in knots.</summary>
    public double AmbientWindSpeedKnots { get; init; }

    /// <summary>
    /// Ambient wind direction in degrees magnetic (direction wind is coming FROM).
    /// </summary>
    public double AmbientWindDirectionDegrees { get; init; }

    /// <summary>Outside air temperature in degrees Celsius.</summary>
    public double AmbientTemperatureCelsius { get; init; }

    /// <summary>
    /// Spoiler/speedbrake handle position (0.0 = fully retracted, 1.0 = fully deployed).
    /// </summary>
    public double SpoilerHandlePosition { get; init; }

    /// <summary>True when spoilers are armed for automatic deployment (1.0 = armed, 0.0 = not armed).</summary>
    public double SpoilersArmed { get; init; }

    /// <summary>Engine 1 turbine N1 fan speed as a percentage (0–110 %).</summary>
    public double Engine1N1Pct { get; init; }
    /// <summary>Engine 2 turbine N1 fan speed as a percentage (0–110 %).</summary>
    public double Engine2N1Pct { get; init; }
    /// <summary>Engine 3 turbine N1 fan speed as a percentage (0–110 %).</summary>
    public double Engine3N1Pct { get; init; }
    /// <summary>Engine 4 turbine N1 fan speed as a percentage (0–110 %).</summary>
    public double Engine4N1Pct { get; init; }

    /// <summary>
    /// True when a NAV1 ILS glideslope signal is being received (1.0 = valid, 0.0 = no signal).
    /// Used to distinguish a genuine ILS approach from a NAV radio tuned to a VOR.
    /// </summary>
    public double Nav1IlsSignalValid { get; init; }
}
