using SimCrewOps.SimConnect.Models;

namespace SimCrewOps.SimConnect.Services;

public static class SimConnectDefinitionCatalog
{
    public static readonly IReadOnlyList<string> DefaultSimulatorProcessNames =
    [
        "FlightSimulator2024.exe",
        "FlightSimulator.exe",
        "Microsoft.FlightSimulator.exe",
    ];

    public static readonly IReadOnlyList<SimConnectVariableDefinition> FlightCriticalVariables =
    [
        Define("latitude", "PLANE LATITUDE", "degrees", SimConnectUpdateRate.SimFrame),
        Define("longitude", "PLANE LONGITUDE", "degrees", SimConnectUpdateRate.SimFrame),
        Define("agl", "PLANE ALT ABOVE GROUND", "feet", SimConnectUpdateRate.SimFrame),
        Define("altitude", "PLANE ALTITUDE", "feet", SimConnectUpdateRate.SimFrame),
        Define("indicated_altitude", "INDICATED ALTITUDE", "feet", SimConnectUpdateRate.SimFrame),
        Define("ias", "AIRSPEED INDICATED", "knots", SimConnectUpdateRate.SimFrame),
        Define("ground_speed", "GROUND VELOCITY", "knots", SimConnectUpdateRate.SimFrame),
        Define("vertical_speed", "VERTICAL SPEED", "feet per minute", SimConnectUpdateRate.SimFrame),
        Define("bank", "PLANE BANK DEGREES", "degrees", SimConnectUpdateRate.SimFrame),
        Define("pitch", "PLANE PITCH DEGREES", "degrees", SimConnectUpdateRate.SimFrame),
        // PARKING BRAKE INDICATOR reflects the cockpit indicator light — on many aircraft (Fenix,
        // PMDG, etc.) it stays lit even when the brake is released, so it always reads 1.
        // BRAKE PARKING POSITION returns 0–100 (0 = fully released, 100 = fully set) and is
        // universally reliable. We treat any value > 50 as "set".
        Define("parking_brake", "BRAKE PARKING POSITION", "percent", SimConnectUpdateRate.SimFrame),
        // SIM ON GROUND is requested as Float64 (all SimVars use uniform Float64 now).
        // The earlier mixed Int32/Float64 struct had alignment issues that caused it to
        // always read 0. If the SimVar is still broken on a given MSFS build, the
        // AGL + VS heuristic in UpdateFlightCritical acts as a fallback.
        Define("on_ground", "SIM ON GROUND", "bool", SimConnectUpdateRate.SimFrame),
        Define("crash_flag", "CRASH FLAG", "bool", SimConnectUpdateRate.SimFrame),
        // VELOCITY WORLD Y — physics-engine instantaneous vertical velocity (ft/s, negative = descending).
        // Unlike VERTICAL SPEED (barometric, ≥1 s lag), this reflects the true sink rate at the
        // exact frame of wheel contact. Appended last to preserve existing FlightCriticalSnapshot layout.
        Define("velocity_world_y", "VELOCITY WORLD Y", "feet per second", SimConnectUpdateRate.SimFrame),
    ];

    public static readonly IReadOnlyList<SimConnectVariableDefinition> ScoringAndOperationalVariables =
    [
        Define("heading_magnetic", "PLANE HEADING DEGREES MAGNETIC", "degrees", SimConnectUpdateRate.Second),
        Define("heading_true", "PLANE HEADING DEGREES TRUE", "degrees", SimConnectUpdateRate.Second),
        Define("tas", "AIRSPEED TRUE", "knots", SimConnectUpdateRate.Second),
        Define("mach", "AIRSPEED MACH", "mach", SimConnectUpdateRate.Second),
        Define("g_force", "G FORCE", "gforce", SimConnectUpdateRate.Second),
        Define("flaps_index", "FLAPS HANDLE INDEX", "number", SimConnectUpdateRate.Second, SimConnectValueType.Int32),
        // GEAR POSITION:1 was an indexed SimVar that threw SIMCONNECT_EXCEPTION_UNIMPLEMENTED on
        // MSFS 2024 XGP, silently shifting every subsequent field in the operational struct by one
        // slot (GearPosition read engine combustion = 1.0, lights read wrong fields).
        // GEAR HANDLE POSITION is non-indexed, universally supported, and returns 0 (up) or 1 (down).
        Define("gear_position", "GEAR HANDLE POSITION", "bool", SimConnectUpdateRate.Second),
        Define("engine1", "ENG COMBUSTION:1", "bool", SimConnectUpdateRate.Second),
        Define("engine2", "ENG COMBUSTION:2", "bool", SimConnectUpdateRate.Second),
        Define("engine3", "ENG COMBUSTION:3", "bool", SimConnectUpdateRate.Second, requiredForScoring: false),
        Define("engine4", "ENG COMBUSTION:4", "bool", SimConnectUpdateRate.Second, requiredForScoring: false),
        Define("beacon_light", "LIGHT BEACON", "bool", SimConnectUpdateRate.Second),
        Define("taxi_light", "LIGHT TAXI", "bool", SimConnectUpdateRate.Second),
        Define("landing_light", "LIGHT LANDING", "bool", SimConnectUpdateRate.Second),
        Define("strobe_light", "LIGHT STROBE", "bool", SimConnectUpdateRate.Second),
        Define("light_states", "LIGHT STATES", "Mask", SimConnectUpdateRate.Second, SimConnectValueType.Int32),
        Define("stall_warning", "STALL WARNING", "bool", SimConnectUpdateRate.Second),
        Define("gpws_warning", "GPWS SYSTEM ACTIVE", "bool", SimConnectUpdateRate.Second, requiredForScoring: false),
        Define("overspeed_warning", "OVERSPEED WARNING", "bool", SimConnectUpdateRate.Second, requiredForScoring: false),
        // NAV1 glideslope and localizer deviation — used for approach G/S and LOC display.
        // NAV GLIDE SLOPE ERROR:1 returns deviation in degrees (positive = above glidepath, negative = below).
        // NAV RADIAL ERROR:1 returns deviation from selected course in degrees; for ILS this is LOC deviation.
        // Both are optional: if the aircraft has no NAV tuned these will read 0.0.
        Define("nav1_glideslope_error", "NAV GLIDE SLOPE ERROR:1", "degrees", SimConnectUpdateRate.SimFrame, requiredForScoring: false),
        Define("nav1_radial_error", "NAV RADIAL ERROR:1", "degrees", SimConnectUpdateRate.SimFrame, requiredForScoring: false),

        // ── Extended context (all optional — fail gracefully if unsupported) ──────
        // Autopilot master switch state.
        Define("autopilot_master",       "AUTOPILOT MASTER",           "bool",     SimConnectUpdateRate.Second, requiredForScoring: false),
        // Total usable fuel weight in pounds.
        Define("fuel_total_lbs",         "FUEL TOTAL QUANTITY WEIGHT", "pounds",   SimConnectUpdateRate.Second, requiredForScoring: false),
        // Ambient wind at the aircraft's current position.
        Define("ambient_wind_speed",     "AMBIENT WIND VELOCITY",      "knots",    SimConnectUpdateRate.Second, requiredForScoring: false),
        Define("ambient_wind_direction", "AMBIENT WIND DIRECTION",     "degrees",  SimConnectUpdateRate.Second, requiredForScoring: false),
        // Outside air temperature in Celsius.
        Define("ambient_temperature",    "AMBIENT TEMPERATURE",        "celsius",  SimConnectUpdateRate.Second, requiredForScoring: false),
        // Spoiler/speedbrake handle position (0.0 = retracted, 1.0 = fully deployed).
        Define("spoiler_handle",         "SPOILERS HANDLE POSITION",   "position", SimConnectUpdateRate.Second, requiredForScoring: false),
        // True when spoilers are armed for automatic deployment on touchdown.
        Define("spoilers_armed",         "SPOILERS ARMED",             "bool",     SimConnectUpdateRate.Second, requiredForScoring: false),
        // Turbine engine N1 fan speed as a raw percentage (0–110 %).
        // Uses "Percent" unit (not "Percent Over 100") to preserve the 0–110 range.
        Define("engine1_n1",             "TURB ENG N1:1",              "Percent",  SimConnectUpdateRate.Second, requiredForScoring: false),
        Define("engine2_n1",             "TURB ENG N1:2",              "Percent",  SimConnectUpdateRate.Second, requiredForScoring: false),
        Define("engine3_n1",             "TURB ENG N1:3",              "Percent",  SimConnectUpdateRate.Second, requiredForScoring: false),
        Define("engine4_n1",             "TURB ENG N1:4",              "Percent",  SimConnectUpdateRate.Second, requiredForScoring: false),
        // True when a valid ILS glideslope signal is being received on NAV1.
        // Distinguishes a genuine ILS approach from a VOR tuned on the same frequency.
        Define("nav1_ils_valid",         "NAV HAS GLIDE SLOPE:1",      "bool",     SimConnectUpdateRate.Second, requiredForScoring: false),
    ];

    public static IReadOnlyList<SimConnectVariableDefinition> AllVariables =>
        FlightCriticalVariables.Concat(ScoringAndOperationalVariables).ToArray();

    private static SimConnectVariableDefinition Define(
        string key,
        string simVarName,
        string unit,
        SimConnectUpdateRate updateRate,
        SimConnectValueType valueType = SimConnectValueType.Float64,
        bool requiredForScoring = true) =>
        new()
        {
            Key = key,
            SimVarName = simVarName,
            Unit = unit,
            UpdateRate = updateRate,
            ValueType = valueType,
            RequiredForScoring = requiredForScoring,
        };
}
