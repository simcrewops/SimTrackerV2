namespace SimCrewOps.SimConnect.Services.Aircraft;

/// <summary>
/// Built-in aircraft profiles for popular third-party MSFS addons.
///
/// Matching is done by case-insensitive substring against the aircraft file path
/// returned by SimConnect_RequestSystemState("AircraftLoaded"), e.g.:
///   "Community\fenix-a319\SimObjects\Airplanes\fenix_a319\fenix_a319.air"
///
/// LVAR names are confirmed from MobiFlight preset databases and community sources.
/// </summary>
public static class AircraftProfileCatalog
{
    private static LightVariableMapping Lvar(string name, double onThreshold = 0.5) =>
        new() { Source = LightVariableSource.LVar, LvarName = name, OnThreshold = onThreshold };

    public static readonly IReadOnlyList<AircraftProfile> Profiles =
    [
        // ── Fenix Simulations A319 ────────────────────────────────────────────────
        // Matched before the generic "fenix" pattern so A319 path wins over A320.
        // Fenix path: ...Community\fenix-a319\SimObjects\Airplanes\fnx_a319\...
        // Nose light: 3-position switch — 0=OFF, 1=TAXI, 2=T.O. (both ≥1 = "on")
        // Landing:    3-position per side — 0=RETRACT, 1=OFF, 2=ON (only 2 = "on")
        // Sources: MobiFlight preset DB, YourControls Fenix config, Rowsfire connector
        new AircraftProfile
        {
            Name = "Fenix A319",
            TitlePattern = "fnx_a319",
            IcaoType = "A319",
            RequiresLvarBridge = true,
            TaxiLight    = Lvar("S_OH_EXT_LT_NOSE",      onThreshold: 1.0),
            LandingLight = Lvar("S_OH_EXT_LT_LANDING_L", onThreshold: 2.0),
            BeaconLight  = Lvar("S_OH_EXT_LT_BEACON",    onThreshold: 1.0),
            StrobeLight  = Lvar("S_OH_EXT_LT_STROBE",    onThreshold: 1.0),
        },

        // ── Fenix Simulations A320 ────────────────────────────────────────────────
        // Fenix path: ...Community\fenix-a320\SimObjects\Airplanes\fnx_a320\...
        new AircraftProfile
        {
            Name = "Fenix A320",
            TitlePattern = "fnx_a320",
            IcaoType = "A320",
            RequiresLvarBridge = true,
            TaxiLight    = Lvar("S_OH_EXT_LT_NOSE",      onThreshold: 1.0),
            LandingLight = Lvar("S_OH_EXT_LT_LANDING_L", onThreshold: 2.0),
            BeaconLight  = Lvar("S_OH_EXT_LT_BEACON",    onThreshold: 1.0),
            StrobeLight  = Lvar("S_OH_EXT_LT_STROBE",    onThreshold: 1.0),
        },

        // ── Fenix Simulations A321 ────────────────────────────────────────────────
        // Fenix path: ...Community\fenix-a321\SimObjects\Airplanes\fnx_a321\...
        // Same LVAR names as A319/A320 — shared cockpit logic across the family.
        new AircraftProfile
        {
            Name = "Fenix A321",
            TitlePattern = "fnx_a321",
            IcaoType = "A321",
            RequiresLvarBridge = true,
            TaxiLight    = Lvar("S_OH_EXT_LT_NOSE",      onThreshold: 1.0),
            LandingLight = Lvar("S_OH_EXT_LT_LANDING_L", onThreshold: 2.0),
            BeaconLight  = Lvar("S_OH_EXT_LT_BEACON",    onThreshold: 1.0),
            StrobeLight  = Lvar("S_OH_EXT_LT_STROBE",    onThreshold: 1.0),
        },

        // ── Fenix Simulations A32X (catch-all) ────────────────────────────────────
        // Catches community livery packages (e.g. "FMK_32X", "fnx_32x") whose folder
        // paths contain "fnx" or "fenix" but not a specific variant string above.
        // Reported aircraft type defaults to A320 — correct for most of the family.
        new AircraftProfile
        {
            Name = "Fenix A32X",
            TitlePattern = "fnx",
            IcaoType = "A320",
            RequiresLvarBridge = true,
            TaxiLight    = Lvar("S_OH_EXT_LT_NOSE",      onThreshold: 1.0),
            LandingLight = Lvar("S_OH_EXT_LT_LANDING_L", onThreshold: 2.0),
            BeaconLight  = Lvar("S_OH_EXT_LT_BEACON",    onThreshold: 1.0),
            StrobeLight  = Lvar("S_OH_EXT_LT_STROBE",    onThreshold: 1.0),
        },

        // ── Fenix Simulations (fenix folder name fallback) ────────────────────────
        // Some livery packages use the base "fenix" folder rather than "fnx_xxx".
        new AircraftProfile
        {
            Name = "Fenix A32X",
            TitlePattern = "fenix",
            IcaoType = "A320",
            RequiresLvarBridge = true,
            TaxiLight    = Lvar("S_OH_EXT_LT_NOSE",      onThreshold: 1.0),
            LandingLight = Lvar("S_OH_EXT_LT_LANDING_L", onThreshold: 2.0),
            BeaconLight  = Lvar("S_OH_EXT_LT_BEACON",    onThreshold: 1.0),
            StrobeLight  = Lvar("S_OH_EXT_LT_STROBE",    onThreshold: 1.0),
        },

        // ── Aerosoft CRJ 550 / 700 / 900 / 1000 ──────────────────────────────────
        // Strobe uses standard SimVar (no confirmed ASCRJ_EXTL_STROBE read LVAR).
        // Sources: MobiFlight msfs2020_simvars.cip, MobiFlight event ID presets
        new AircraftProfile
        {
            Name = "Aerosoft CRJ",
            TitlePattern = "aerosoft crj",
            IcaoType = "CRJ7",
            RequiresLvarBridge = true,
            TaxiLight    = Lvar("ASCRJ_OVHD_TAXI",     onThreshold: 1.0),
            LandingLight = Lvar("ASCRJ_OVHD_LDG_NOSE", onThreshold: 1.0),
            BeaconLight  = Lvar("ASCRJ_EXTL_BEACON",   onThreshold: 1.0),
            StrobeLight  = LightVariableMapping.StandardSimVar,
        },

        // ── FlyByWire A32NX ───────────────────────────────────────────────────────
        // FBW wires all lights through standard MSFS/ASOBO lighting templates.
        // LIGHT TAXI, LIGHT BEACON, LIGHT STROBE, LIGHT LANDING all work normally.
        // Source: flybywiresim/aircraft behavior XML files
        new AircraftProfile
        {
            Name = "FlyByWire A32NX",
            TitlePattern = "flybywire",
            IcaoType = "A320",
        },

        // ── iniBuilds A310 ────────────────────────────────────────────────────────
        // Checked before generic "inibuilds" pattern.
        new AircraftProfile
        {
            Name = "iniBuilds A310",
            TitlePattern = "ini_a310",
            IcaoType = "A310",
        },

        // ── iniBuilds A320neo ─────────────────────────────────────────────────────
        // iniBuilds uses the standard MSFS lighting SimVars — no custom LVARs needed.
        new AircraftProfile
        {
            Name = "iniBuilds A320neo",
            TitlePattern = "inibuilds",
            IcaoType = "A20N",
        },

        // ── PMDG 737 ──────────────────────────────────────────────────────────────
        // PMDG mirrors standard SimVars for basic light on/off detection.
        // Full switch-position detail requires the PMDG SDK struct (future feature).
        // Sources: PMDG SDK docs, community testing
        new AircraftProfile
        {
            Name = "PMDG 737",
            TitlePattern = "pmdg 737",
            IcaoType = "B738",
        },

        // ── PMDG 777 ──────────────────────────────────────────────────────────────
        new AircraftProfile
        {
            Name = "PMDG 777",
            TitlePattern = "pmdg 777",
            IcaoType = "B77W",
        },

        // ── PMDG 747 ──────────────────────────────────────────────────────────────
        new AircraftProfile
        {
            Name = "PMDG 747",
            TitlePattern = "pmdg 747",
            IcaoType = "B744",
        },

        // ── Headwind A330neo ──────────────────────────────────────────────────────
        new AircraftProfile
        {
            Name = "Headwind A330neo",
            TitlePattern = "headwind",
            IcaoType = "A339",
        },
    ];

    /// <summary>
    /// Returns the first profile whose <see cref="AircraftProfile.TitlePattern"/> appears
    /// (case-insensitive) in <paramref name="aircraftFilePath"/>, or
    /// <see cref="AircraftProfile.Default"/> when nothing matches.
    /// </summary>
    public static AircraftProfile MatchOrDefault(string? aircraftFilePath)
    {
        if (string.IsNullOrEmpty(aircraftFilePath))
        {
            return AircraftProfile.Default;
        }

        foreach (var profile in Profiles)
        {
            if (!string.IsNullOrEmpty(profile.TitlePattern) &&
                aircraftFilePath.Contains(profile.TitlePattern, StringComparison.OrdinalIgnoreCase))
            {
                return profile;
            }
        }

        return AircraftProfile.Default;
    }
}
