using System.Text.Json.Serialization;

namespace SimCrewOps.Hosting.Models;

/// <summary>
/// A live flight position returned by GET /api/tracker/live-flights.
/// Property names match the camelCase JSON the backend emits.
/// </summary>
public sealed record LiveFlight
{
    [JsonPropertyName("pilotId")]
    public required string PilotId { get; init; }

    [JsonPropertyName("pilotName")]
    public required string PilotName { get; init; }

    [JsonPropertyName("latitude")]
    public required double Latitude { get; init; }

    [JsonPropertyName("longitude")]
    public required double Longitude { get; init; }

    [JsonPropertyName("headingMagnetic")]
    public required double Heading { get; init; }

    [JsonPropertyName("altitudeFt")]
    public required double Altitude { get; init; }

    [JsonPropertyName("altitudeAglFt")]
    public double? AltitudeAgl { get; init; }

    [JsonPropertyName("groundSpeedKts")]
    public required double GroundSpeed { get; init; }

    [JsonPropertyName("indicatedAirspeedKts")]
    public double? IndicatedAirspeed { get; init; }

    [JsonPropertyName("verticalSpeedFpm")]
    public double? VerticalSpeed { get; init; }

    [JsonPropertyName("phase")]
    public string? Phase { get; init; }

    [JsonPropertyName("flightMode")]
    public string? FlightMode { get; init; }

    [JsonPropertyName("bidId")]
    public string? BidId { get; init; }

    [JsonPropertyName("lastSeenAt")]
    public required string LastSeenAt { get; init; }

    // Callsign shown on the map — use PilotName as the label (backend doesn't send a callsign)
    [JsonIgnore]
    public string Callsign => PilotName;

    /// <summary>True when this position belongs to the authenticated pilot.</summary>
    [JsonIgnore]
    public bool IsMyFlight { get; init; }
}
