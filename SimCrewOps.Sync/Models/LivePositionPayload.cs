using System.Text.Json.Serialization;

namespace SimCrewOps.Sync.Models;

public sealed record LivePositionPayload
{
    [JsonPropertyName("latitude")]
    public double Latitude { get; init; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; init; }

    [JsonPropertyName("headingMagnetic")]
    public double HeadingMagnetic { get; init; }

    [JsonPropertyName("altitudeFt")]
    public double AltitudeFt { get; init; }

    [JsonPropertyName("altitudeAglFt")]
    public double AltitudeAglFt { get; init; }

    [JsonPropertyName("indicatedAirspeedKts")]
    public double IndicatedAirspeedKts { get; init; }

    [JsonPropertyName("groundSpeedKts")]
    public double GroundSpeedKts { get; init; }

    [JsonPropertyName("verticalSpeedFpm")]
    public double VerticalSpeedFpm { get; init; }

    [JsonPropertyName("phase")]
    public string Phase { get; init; } = "UNKNOWN";

    [JsonPropertyName("flightMode")]
    public string FlightMode { get; init; } = "free_flight";

    [JsonPropertyName("bidId")]
    public string? BidId { get; init; }
}
