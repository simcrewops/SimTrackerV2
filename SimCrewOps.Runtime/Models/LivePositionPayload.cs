using System.Text.Json.Serialization;

namespace SimCrewOps.Runtime.Models;

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

    // Route and aircraft info — sourced from the active flight assignment fetched from the web app.
    // These populate the LiveFlight row so the world map can show departure/arrival/flight number.
    [JsonPropertyName("departure")]
    public string? Departure { get; init; }

    [JsonPropertyName("arrival")]
    public string? Arrival { get; init; }

    [JsonPropertyName("flightNumber")]
    public string? FlightNumber { get; init; }

    [JsonPropertyName("aircraft")]
    public string? Aircraft { get; init; }

    [JsonPropertyName("aircraftCategory")]
    public string? AircraftCategory { get; init; }
}
