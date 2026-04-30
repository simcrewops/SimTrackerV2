using System.Text.Json.Serialization;

namespace SimCrewOps.Sync.Models;

public sealed record SimSessionUploadResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("career")]
    public CareerResultDto? Career { get; init; }

    [JsonPropertyName("postFlight")]
    public PostFlightStatusDto? PostFlight { get; init; }
}

public sealed record CareerResultDto
{
    [JsonPropertyName("grade")]
    public string Grade { get; init; } = "F";

    [JsonPropertyName("score")]
    public double Score { get; init; }

    [JsonPropertyName("phaseScores")]
    public PhaseScoreDto[] PhaseScores { get; init; } = [];

    [JsonPropertyName("hoursAdded")]
    public double HoursAdded { get; init; }

    [JsonPropertyName("newTotalHours")]
    public double NewTotalHours { get; init; }

    [JsonPropertyName("pay")]
    public double Pay { get; init; }

    [JsonPropertyName("reputationDelta")]
    public int ReputationDelta { get; init; }

    [JsonPropertyName("newReputation")]
    public int NewReputation { get; init; }

    [JsonPropertyName("tierChange")]
    public string? TierChange { get; init; }

    [JsonPropertyName("contractCompleted")]
    public bool ContractCompleted { get; init; }

    [JsonPropertyName("safetyIncidents")]
    public object[] SafetyIncidents { get; init; } = [];

    [JsonPropertyName("flightLogId")]
    public string? FlightLogId { get; init; }
}

public sealed record PhaseScoreDto
{
    [JsonPropertyName("phase")]
    public string Phase { get; init; } = string.Empty;

    [JsonPropertyName("score")]
    public double Score { get; init; }

    [JsonPropertyName("maxScore")]
    public double MaxScore { get; init; }

    [JsonPropertyName("deductions")]
    public object[] Deductions { get; init; } = [];
}

public sealed record PostFlightStatusDto
{
    [JsonPropertyName("isGrounded")]
    public bool IsGrounded { get; init; }

    [JsonPropertyName("strikeType")]
    public string? StrikeType { get; init; }

    [JsonPropertyName("strikeAction")]
    public string? StrikeAction { get; init; }
}
