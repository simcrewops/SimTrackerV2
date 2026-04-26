namespace SimCrewOps.Scoring.Scoring;

public sealed class ScoringWeights
{
    public static ScoringWeights Default { get; } = new();

    public PreflightWeights Preflight { get; init; } = new();
    public TaxiWeights TaxiOut { get; init; } = new();
    public TakeoffWeights Takeoff { get; init; } = new();
    public ClimbWeights Climb { get; init; } = new();
    public CruiseWeights Cruise { get; init; } = new();
    public DescentWeights Descent { get; init; } = new();
    public ApproachWeights Approach { get; init; } = new();
    public LandingWeights Landing { get; init; } = new();
    public TaxiInWeights TaxiIn { get; init; } = new();
    public ArrivalWeights Arrival { get; init; } = new();
    public SafetyWeights Safety { get; init; } = new();
}

public sealed class PreflightWeights
{
    public double BeaconOnBeforeTaxi { get; init; } = 5.0;
    public double Total => BeaconOnBeforeTaxi;
}

public class TaxiWeights
{
    public double MaxGroundSpeed { get; init; } = 3.0;
    public double TurnSpeed { get; init; } = 3.0;
    public double TaxiLights { get; init; } = 2.0;
    public double Total => MaxGroundSpeed + TurnSpeed + TaxiLights;
}

public sealed class TakeoffWeights
{
    public double Bounce { get; init; } = 1.0;
    public double TailStrike { get; init; } = 1.5;
    public double BankAngle { get; init; } = 1.5;
    public double PitchAngle { get; init; } = 1.0;
    public double GForce { get; init; } = 3.0;
    public double LandingLightsOnBeforeTakeoff { get; init; } = 0.75;
    public double LandingLightsOffByFl180 { get; init; } = 0.75;
    public double StrobesOnFromTakeoffToLanding { get; init; } = 0.5;
    public double GForcePerfect { get; init; } = 1.5;
    public double GForceMax { get; init; } = 2.5;
    public double Total =>
        Bounce +
        TailStrike +
        BankAngle +
        PitchAngle +
        GForce +
        LandingLightsOnBeforeTakeoff +
        LandingLightsOffByFl180 +
        StrobesOnFromTakeoffToLanding;
}

public sealed class ClimbWeights
{
    public double SpeedCompliance { get; init; } = 4.0;
    public double BankAngle { get; init; } = 3.0;
    public double GForce { get; init; } = 3.0;
    public double GForcePerfect { get; init; } = 1.7;
    public double GForceMax { get; init; } = 2.5;
    public double Total => SpeedCompliance + BankAngle + GForce;
}

public sealed class CruiseWeights
{
    public double AltitudeHold { get; init; } = 2.0;
    public double FlightLevelCapture { get; init; } = 1.0;
    public double SpeedStability { get; init; } = 4.0;
    public double BankAngle { get; init; } = 1.0;
    public double GForce { get; init; } = 2.0;
    public double GForcePerfect { get; init; } = 1.4;
    public double GForceMax { get; init; } = 2.0;
    public double Total => AltitudeHold + FlightLevelCapture + SpeedStability + BankAngle + GForce;
}

public sealed class DescentWeights
{
    public double SpeedCompliance { get; init; } = 2.5;
    public double BankAngle { get; init; } = 2.5;
    public double PitchAngle { get; init; } = 1.5;
    public double GForce { get; init; } = 3.0;
    public double LandingLightsOnByFl180 { get; init; } = 0.5;
    public double GForcePerfect { get; init; } = 1.7;
    public double GForceMax { get; init; } = 2.5;
    public double Total => SpeedCompliance + BankAngle + PitchAngle + GForce + LandingLightsOnByFl180;
}

public sealed class ApproachWeights
{
    public double GearDownBy1000Agl { get; init; } = 5.0;
    public double FlapsConfiguredAt500Agl { get; init; } = 1.5;
    public double StabilizedVerticalSpeed { get; init; } = 2.0;
    public double StabilizedBankAngle { get; init; } = 1.0;
    public double StabilizedPitchAngle { get; init; } = 1.0;
    public double GearDownAt500Agl { get; init; } = 1.0;
    public double StabilizedFlapsConfiguredAt500Agl { get; init; } = 0.5;
    public double Total =>
        GearDownBy1000Agl +
        FlapsConfiguredAt500Agl +
        StabilizedVerticalSpeed +
        StabilizedBankAngle +
        StabilizedPitchAngle +
        GearDownAt500Agl +
        StabilizedFlapsConfiguredAt500Agl;
}

public sealed class LandingWeights
{
    public double TouchdownZone { get; init; } = 5.0;
    public double VerticalSpeed { get; init; } = 6.0;
    public double GForce { get; init; } = 6.0;
    public double Bounce { get; init; } = 3.0;
    public double CenterlineDeviation { get; init; } = 10.0;
    public double CrabAngle { get; init; } = 10.0;

    /// <summary>VS at or below this threshold earns full marks.</summary>
    public double VerticalSpeedPerfectFpm { get; init; } = 300;

    /// <summary>VS above this threshold fails the landing section.</summary>
    public double VerticalSpeedAutoFailFpm { get; init; } = 600;

    /// <summary>G-force at or below this threshold earns full marks.</summary>
    public double GForcePerfect { get; init; } = 1.2;

    /// <summary>G-force above this threshold fails the landing section.</summary>
    public double GForceAutoFail { get; init; } = 2.0;

    public double Total => TouchdownZone + VerticalSpeed + GForce + Bounce + CenterlineDeviation + CrabAngle;
}

public sealed class TaxiInWeights : TaxiWeights
{
    public double LandingLightsOff { get; init; } = 1.5;
    public double StrobesOff { get; init; } = 1.5;
    public new double MaxGroundSpeed { get; init; } = 2.5;
    public new double TurnSpeed { get; init; } = 1.5;
    public new double TaxiLights { get; init; } = 1.0;
    public new double Total => LandingLightsOff + StrobesOff + MaxGroundSpeed + TurnSpeed + TaxiLights;
}

public sealed class ArrivalWeights
{
    public double TaxiLightsOffBeforeParkingBrakeSet { get; init; } = 2.0;
    public double ParkingBrakeBeforeAllEnginesShutdown { get; init; } = 3.0;
    public double AllEnginesOffByEndOfSession { get; init; } = 2.0;
    public double Total => TaxiLightsOffBeforeParkingBrakeSet + ParkingBrakeBeforeAllEnginesShutdown + AllEnginesOffByEndOfSession;
}

public sealed class SafetyWeights
{
    public double CrashPenalty { get; init; } = 15.0;
    public double OverspeedEvent { get; init; } = 3.0;
    public double MaxOverspeedPenalty { get; init; } = 9.0;
    public double SustainedOverspeedEvent { get; init; } = 5.0;
    public double MaxSustainedOverspeedPenalty { get; init; } = 10.0;
    public double StallEvent { get; init; } = 3.0;
    public double MaxStallPenalty { get; init; } = 9.0;
    public double GpwsEvent { get; init; } = 3.0;
    public double MaxGpwsPenalty { get; init; } = 9.0;
    public double EngineShutdownInFlight { get; init; } = 5.0;
}
