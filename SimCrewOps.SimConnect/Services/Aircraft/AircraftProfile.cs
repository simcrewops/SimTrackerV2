namespace SimCrewOps.SimConnect.Services.Aircraft;

/// <summary>
/// Describes how to read cockpit light states for a specific aircraft family.
/// Third-party aircraft (Fenix, Aerosoft CRJ, etc.) store light states in
/// proprietary LVARs rather than standard MSFS SimVars.
/// </summary>
public sealed record AircraftProfile
{
    /// <summary>Fallback profile used when no aircraft-specific profile matches.</summary>
    public static readonly AircraftProfile Default = new()
    {
        Name = "Default (Standard SimVars)",
        TitlePattern = string.Empty,
        RequiresLvarBridge = false,
    };

    /// <summary>Human-readable name shown in diagnostics.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Case-insensitive substring matched against the aircraft file path
    /// returned by SimConnect RequestSystemState("AircraftLoaded").
    /// </summary>
    public required string TitlePattern { get; init; }

    /// <summary>
    /// True when one or more light channels require an LVAR bridge
    /// (MobiFlight WASM) rather than standard SimVars.
    /// </summary>
    public bool RequiresLvarBridge { get; init; }

    public LightVariableMapping TaxiLight    { get; init; } = LightVariableMapping.StandardSimVar;
    public LightVariableMapping LandingLight { get; init; } = LightVariableMapping.StandardSimVar;
    public LightVariableMapping BeaconLight  { get; init; } = LightVariableMapping.StandardSimVar;
    public LightVariableMapping StrobeLight  { get; init; } = LightVariableMapping.StandardSimVar;
}

/// <summary>Points to either a standard MSFS SimVar or a proprietary LVAR for a single light channel.</summary>
public sealed record LightVariableMapping
{
    public static readonly LightVariableMapping StandardSimVar =
        new() { Source = LightVariableSource.StandardSimVar };

    public required LightVariableSource Source { get; init; }

    /// <summary>LVAR name without the "L:" prefix, e.g. "S_OH_EXT_LT_NOSE".</summary>
    public string? LvarName { get; init; }

    /// <summary>
    /// The light is considered ON when the LVAR value >= this threshold.
    /// Handles multi-position switches: e.g. Fenix nose light 0=OFF, 1=TAXI, 2=T.O.
    /// Setting OnThreshold = 1.0 treats both TAXI and T.O. as "on".
    /// </summary>
    public double OnThreshold { get; init; } = 0.5;
}

public enum LightVariableSource
{
    /// <summary>Use the standard MSFS SimVar (LIGHT BEACON, LIGHT TAXI, etc.).</summary>
    StandardSimVar,

    /// <summary>Use an aircraft-proprietary LVAR via the MobiFlight WASM bridge.</summary>
    LVar,
}
