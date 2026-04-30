using System.Text.Json.Serialization;

namespace SimCrewOps.Sync.Models;

public sealed record SimSessionUploadRequest
{
    [JsonPropertyName("trackerVersion")]
    public string TrackerVersion { get; init; } = "dev";

    [JsonPropertyName("flightMode")]
    public string FlightMode { get; init; } = "free_flight";

    [JsonPropertyName("bidId")]
    public string? BidId { get; init; }

    [JsonPropertyName("departure")]
    public string? Departure { get; init; }

    [JsonPropertyName("arrival")]
    public string? Arrival { get; init; }

    [JsonPropertyName("aircraft")]
    public string? Aircraft { get; init; }

    [JsonPropertyName("aircraftCategory")]
    public string? AircraftCategory { get; init; }

    [JsonPropertyName("actualBlocksOff")]
    public DateTimeOffset? ActualBlocksOff { get; init; }

    [JsonPropertyName("actualWheelsOff")]
    public DateTimeOffset? ActualWheelsOff { get; init; }

    [JsonPropertyName("actualWheelsOn")]
    public DateTimeOffset? ActualWheelsOn { get; init; }

    [JsonPropertyName("actualBlocksOn")]
    public DateTimeOffset? ActualBlocksOn { get; init; }

    [JsonPropertyName("blockTimeActual")]
    public double? BlockTimeActual { get; init; }

    [JsonPropertyName("blockTimeScheduled")]
    public double? BlockTimeScheduled { get; init; }

    [JsonPropertyName("scoringInput")]
    public ScoringInputDto ScoringInput { get; init; } = new();

    [JsonPropertyName("landingAnalysis")]
    public LandingAnalysisDto LandingAnalysis { get; init; } = new();

    [JsonPropertyName("flightPath")]
    public FlightPathPointDto[] FlightPath { get; init; } = [];
}

public sealed record ScoringInputDto
{
    [JsonPropertyName("departure")]
    public DepartureScoringDto Departure { get; init; } = new();

    [JsonPropertyName("climb")]
    public ClimbScoringDto Climb { get; init; } = new();

    [JsonPropertyName("cruise")]
    public CruiseScoringDto Cruise { get; init; } = new();

    [JsonPropertyName("descent")]
    public DescentScoringDto Descent { get; init; } = new();

    [JsonPropertyName("landing")]
    public LandingScoringDto Landing { get; init; } = new();

    [JsonPropertyName("safety")]
    public SafetyScoringDto Safety { get; init; } = new();
}

public sealed record DepartureScoringDto
{
    [JsonPropertyName("v1Kts")]
    public double? V1Kts { get; init; }

    [JsonPropertyName("vrKts")]
    public double? VrKts { get; init; }

    [JsonPropertyName("v2Kts")]
    public double? V2Kts { get; init; }

    [JsonPropertyName("takeoffPitchDeg")]
    public double TakeoffPitchDeg { get; init; }

    [JsonPropertyName("initialClimbFpm")]
    public double InitialClimbFpm { get; init; }

    [JsonPropertyName("flapsAtTakeoff")]
    public int FlapsAtTakeoff { get; init; }
}

public sealed record ClimbScoringDto
{
    [JsonPropertyName("avgClimbFpm")]
    public double AvgClimbFpm { get; init; }

    [JsonPropertyName("timeToFL100Min")]
    public double? TimeToFL100Min { get; init; }

    [JsonPropertyName("vsStabilityScore")]
    public double VsStabilityScore { get; init; }
}

public sealed record CruiseScoringDto
{
    [JsonPropertyName("altitudeDeviationFt")]
    public double AltitudeDeviationFt { get; init; }

    [JsonPropertyName("speedDeviationKts")]
    public double SpeedDeviationKts { get; init; }
}

public sealed record DescentScoringDto
{
    [JsonPropertyName("avgDescentFpm")]
    public double AvgDescentFpm { get; init; }

    [JsonPropertyName("speedAtFL100Kts")]
    public double? SpeedAtFL100Kts { get; init; }
}

public sealed record LandingScoringDto
{
    [JsonPropertyName("touchdownRateFpm")]
    public double TouchdownRateFpm { get; init; }

    [JsonPropertyName("touchdownPitchDeg")]
    public double TouchdownPitchDeg { get; init; }

    [JsonPropertyName("maxPitchWhileWowDeg")]
    public double MaxPitchWhileWowDeg { get; init; }

    [JsonPropertyName("touchdownBankDeg")]
    public double TouchdownBankDeg { get; init; }

    [JsonPropertyName("touchdownGForce")]
    public double TouchdownGForce { get; init; }

    [JsonPropertyName("bounceCount")]
    public int BounceCount { get; init; }

    [JsonPropertyName("gearUpAtTouchdown")]
    public bool GearUpAtTouchdown { get; init; }
}

public sealed record SafetyScoringDto
{
    [JsonPropertyName("crashDetected")]
    public bool CrashDetected { get; init; }

    [JsonPropertyName("overspeedWarningCount")]
    public int OverspeedWarningCount { get; init; }

    [JsonPropertyName("stallWarningCount")]
    public int StallWarningCount { get; init; }

    [JsonPropertyName("gpwsAlertCount")]
    public int GpwsAlertCount { get; init; }
}

public sealed record LandingAnalysisDto
{
    [JsonPropertyName("touchdownLat")]
    public double? TouchdownLat { get; init; }

    [JsonPropertyName("touchdownLon")]
    public double? TouchdownLon { get; init; }

    [JsonPropertyName("touchdownHeadingDeg")]
    public double? TouchdownHeadingDeg { get; init; }

    [JsonPropertyName("touchdownAltFt")]
    public double? TouchdownAltFt { get; init; }

    [JsonPropertyName("touchdownIAS")]
    public double? TouchdownIAS { get; init; }

    [JsonPropertyName("windSpeedAtTouchdownKnots")]
    public double? WindSpeedAtTouchdownKnots { get; init; }

    [JsonPropertyName("windDirectionAtTouchdownDegrees")]
    public double? WindDirectionAtTouchdownDegrees { get; init; }

    [JsonPropertyName("approachPath")]
    public ApproachPathPointDto[] ApproachPath { get; init; } = [];
}

public sealed record ApproachPathPointDto
{
    [JsonPropertyName("lat")]
    public double Lat { get; init; }

    [JsonPropertyName("lon")]
    public double Lon { get; init; }

    [JsonPropertyName("altitudeFt")]
    public double AltitudeFt { get; init; }

    [JsonPropertyName("iasKts")]
    public double IasKts { get; init; }

    [JsonPropertyName("vsFpm")]
    public double VsFpm { get; init; }

    [JsonPropertyName("distanceToThresholdNm")]
    public double? DistanceToThresholdNm { get; init; }
}

public sealed record FlightPathPointDto
{
    [JsonPropertyName("lat")]
    public double Lat { get; init; }

    [JsonPropertyName("lon")]
    public double Lon { get; init; }

    [JsonPropertyName("altFt")]
    public double AltFt { get; init; }

    [JsonPropertyName("tMin")]
    public double TMin { get; init; }
}
