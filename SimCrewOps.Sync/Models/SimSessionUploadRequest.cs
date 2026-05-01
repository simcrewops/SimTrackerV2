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
    public FlightScoreInputV5Upload ScoringInput { get; init; } = new();

    [JsonPropertyName("landingAnalysis")]
    public LandingAnalysisDto LandingAnalysis { get; init; } = new();

    [JsonPropertyName("flightPath")]
    public FlightPathPointDto[] FlightPath { get; init; } = [];
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
