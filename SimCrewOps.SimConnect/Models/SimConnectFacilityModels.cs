using System.Runtime.InteropServices;

namespace SimCrewOps.SimConnect.Models;

public sealed record SimConnectAirportFacilitySnapshot
{
    public required string AirportIcao { get; init; }
    public required IReadOnlyList<SimConnectFacilityRunway> Runways { get; init; }
}

public sealed record SimConnectFacilityRunway
{
    public required string AirportIcao { get; init; }
    public required double CenterLatitude { get; init; }
    public required double CenterLongitude { get; init; }
    public required double HeadingTrueDegrees { get; init; }
    public required double LengthFeet { get; init; }
    public required int PrimaryNumber { get; init; }
    public required int PrimaryDesignator { get; init; }
    public required int SecondaryNumber { get; init; }
    public required int SecondaryDesignator { get; init; }
    public required bool HasPrimaryThresholdData { get; init; }
    public required bool HasSecondaryThresholdData { get; init; }
    public double PrimaryThresholdLengthFeet { get; init; }
    public double SecondaryThresholdLengthFeet { get; init; }
}

/// <summary>Accumulates RUNWAY + PAVEMENT payloads during a facility request.</summary>
internal sealed class FacilityPendingRunwayNode
{
    private int _thresholdPayloadCount;

    public FacilityRunwayPayload? Runway { get; set; }
    public FacilityPavementPayload? PrimaryThreshold { get; private set; }
    public FacilityPavementPayload? SecondaryThreshold { get; private set; }

    public void AddThresholdPayload(FacilityPavementPayload? payload)
    {
        if (payload is null) return;
        if (_thresholdPayloadCount == 0) PrimaryThreshold = payload;
        else if (_thresholdPayloadCount == 1) SecondaryThreshold = payload;
        _thresholdPayloadCount++;
    }

    public SimConnectFacilityRunway? ToFacilityRunway(string airportIcao, double feetPerMeter)
    {
        if (Runway is null) return null;
        return new SimConnectFacilityRunway
        {
            AirportIcao = airportIcao,
            CenterLatitude = Runway.Value.Latitude,
            CenterLongitude = Runway.Value.Longitude,
            HeadingTrueDegrees = NormalizeHeading(Runway.Value.Heading),
            LengthFeet = Runway.Value.Length * feetPerMeter,
            PrimaryNumber = Runway.Value.PrimaryNumber,
            PrimaryDesignator = Runway.Value.PrimaryDesignator,
            SecondaryNumber = Runway.Value.SecondaryNumber,
            SecondaryDesignator = Runway.Value.SecondaryDesignator,
            HasPrimaryThresholdData = PrimaryThreshold is not null,
            HasSecondaryThresholdData = SecondaryThreshold is not null,
            PrimaryThresholdLengthFeet = (PrimaryThreshold?.Length ?? 0) * feetPerMeter,
            SecondaryThresholdLengthFeet = (SecondaryThreshold?.Length ?? 0) * feetPerMeter,
        };
    }

    private static double NormalizeHeading(double heading)
    {
        var normalized = heading % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct FacilityRunwayPayload
{
    public double Latitude;
    public double Longitude;
    public float Heading;
    public float Length;
    public int PrimaryNumber;
    public int PrimaryDesignator;
    public int SecondaryNumber;
    public int SecondaryDesignator;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct FacilityPavementPayload
{
    public float Length;
    public int Enable;
}

internal static class FacilityDefinitionFields
{
    public static readonly string[] All =
    [
        "OPEN AIRPORT",
        "OPEN RUNWAY",
        "LATITUDE",
        "LONGITUDE",
        "HEADING",
        "LENGTH",
        "PRIMARY_NUMBER",
        "PRIMARY_DESIGNATOR",
        "SECONDARY_NUMBER",
        "SECONDARY_DESIGNATOR",
        "OPEN PRIMARY_THRESHOLD",
        "LENGTH",
        "ENABLE",
        "CLOSE PRIMARY_THRESHOLD",
        "OPEN SECONDARY_THRESHOLD",
        "LENGTH",
        "ENABLE",
        "CLOSE SECONDARY_THRESHOLD",
        "CLOSE RUNWAY",
        "CLOSE AIRPORT",
    ];
}
