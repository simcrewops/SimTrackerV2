namespace SimCrewOps.SimConnect.Services;

internal static class SimConnectLightStateDecoder
{
    private const int BeaconMask = 0x0002;
    private const int LandingMask = 0x0004;
    private const int TaxiMask = 0x0008;
    private const int StrobeMask = 0x0010;

    public static bool IsBeaconOn(int lightStates) => HasMask(lightStates, BeaconMask);

    public static bool IsLandingOn(int lightStates) => HasMask(lightStates, LandingMask);

    public static bool IsTaxiOn(int lightStates) => HasMask(lightStates, TaxiMask);

    public static bool IsStrobeOn(int lightStates) => HasMask(lightStates, StrobeMask);

    private static bool HasMask(int lightStates, int mask) => (lightStates & mask) == mask;
}
