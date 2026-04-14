using System.Text.Json.Serialization;

namespace SimCrewOps.Sync.Models;

public sealed record PositionPayload
{
    [JsonPropertyName("token")]
    public string Token { get; init; } = string.Empty;

    [JsonPropertyName("callsign")]
    public string Callsign { get; init; } = string.Empty;

    [JsonPropertyName("latitude")]
    public double Latitude { get; init; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; init; }

    [JsonPropertyName("altitude")]
    public double Altitude { get; init; }

    [JsonPropertyName("heading")]
    public double Heading { get; init; }

    [JsonPropertyName("groundSpeed")]
    public double GroundSpeed { get; init; }

    [JsonPropertyName("phase")]
    public string Phase { get; init; } = string.Empty;

    [JsonPropertyName("flightId")]
    public string? FlightId { get; init; }
}
