using System.Text.Json.Serialization;

namespace SimCrewOps.Sync.Models;

public sealed record PreflightStatusResponse
{
    [JsonPropertyName("pilotUsername")]
    public string? PilotUsername { get; init; }

    [JsonPropertyName("pilotDisplayName")]
    public string? PilotDisplayName { get; init; }

    [JsonPropertyName("rank")]
    public string? Rank { get; init; }

    [JsonPropertyName("isGrounded")]
    public bool IsGrounded { get; init; }

    [JsonPropertyName("groundingReason")]
    public string? GroundingReason { get; init; }

    [JsonPropertyName("groundingAction")]
    public string? GroundingAction { get; init; }

    [JsonPropertyName("strikeId")]
    public string? StrikeId { get; init; }

    [JsonPropertyName("strikeType")]
    public string? StrikeType { get; init; }

    [JsonPropertyName("inCrewRest")]
    public bool InCrewRest { get; init; }

    [JsonPropertyName("crewRestEndsAt")]
    public DateTimeOffset? CrewRestEndsAt { get; init; }

    [JsonPropertyName("checkedAt")]
    public DateTimeOffset CheckedAt { get; init; }
}
