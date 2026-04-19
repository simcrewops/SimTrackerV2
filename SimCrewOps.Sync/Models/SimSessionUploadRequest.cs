using System.Text.Json.Serialization;

namespace SimCrewOps.Sync.Models;

public sealed record SimSessionUploadRequest
{
    [JsonPropertyName("bounces")]
    public int Bounces { get; init; }

    [JsonPropertyName("touchdownVS")]
    public double TouchdownVS { get; init; }

    [JsonPropertyName("touchdownBank")]
    public double TouchdownBank { get; init; }

    [JsonPropertyName("touchdownIAS")]
    public double TouchdownIAS { get; init; }

    [JsonPropertyName("touchdownPitch")]
    public double TouchdownPitch { get; init; }

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

    [JsonPropertyName("crashDetected")]
    public bool CrashDetected { get; init; }

    [JsonPropertyName("overspeedEvents")]
    public int OverspeedEvents { get; init; }

    [JsonPropertyName("stallEvents")]
    public int StallEvents { get; init; }

    [JsonPropertyName("gpwsEvents")]
    public int GpwsEvents { get; init; }

    [JsonPropertyName("grade")]
    public string Grade { get; init; } = "F";

    [JsonPropertyName("scoreFinal")]
    public double ScoreFinal { get; init; }

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
}
