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
        Define("parking_brake", "PARKING BRAKE INDICATOR", "bool", SimConnectUpdateRate.SimFrame),
        // NOTE: on_ground is intentionally NOT read from a SimVar — "SIM ON GROUND" returns 0
        // on MSFS 2024 Xbox Game Pass, and indexed SimVars like "GEAR IS ON GROUND:1" cause
        // SIMCONNECT_EXCEPTION_UNIMPLEMENTED on some builds which can silently stop the entire
        // definition group from delivering further data.  OnGround is instead computed in the
        // telemetry mapper from PLANE ALT ABOVE GROUND, which is already present in this group.
        Define("crash_flag", "CRASH FLAG", "bool", SimConnectUpdateRate.SimFrame),
    ];

    public static readonly IReadOnlyList<SimConnectVariableDefinition> ScoringAndOperationalVariables =
    [
        Define("heading_magnetic", "PLANE HEADING DEGREES MAGNETIC", "degrees", SimConnectUpdateRate.Second),
        Define("heading_true", "PLANE HEADING DEGREES TRUE", "degrees", SimConnectUpdateRate.Second),
        Define("tas", "AIRSPEED TRUE", "knots", SimConnectUpdateRate.Second),
        Define("mach", "AIRSPEED MACH", "mach", SimConnectUpdateRate.Second),
        Define("g_force", "G FORCE", "gforce", SimConnectUpdateRate.Second),
        Define("flaps_index", "FLAPS HANDLE INDEX", "number", SimConnectUpdateRate.Second, SimConnectValueType.Int32),
        Define("gear_position", "GEAR POSITION:1", "percent", SimConnectUpdateRate.Second),
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
