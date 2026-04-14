using SimCrewOps.Runways.Models;
using SimCrewOps.Runways.Providers;
using SimCrewOps.Runways.Services;
using Xunit;

namespace SimCrewOps.Runways.Tests;

public sealed class RunwayResolverTests
{
    [Fact]
    public void TouchdownZoneCalculator_ReturnsZeroWhenTouchdownIsInsideZone()
    {
        var calculator = new TouchdownZoneCalculator();
        var runway = BuildRunway(headingDegrees: 180);
        var touchdown = Offset(runway.ThresholdLatitude, runway.ThresholdLongitude, runway.TrueHeadingDegrees, 2_500);

        var projection = calculator.ProjectTouchdown(runway, touchdown.Latitude, touchdown.Longitude);

        Assert.InRange(projection.DistanceFromThresholdFeet, 2_450, 2_550);
        Assert.Equal(0, projection.TouchdownZoneExcessDistanceFeet);
    }

    [Fact]
    public void TouchdownZoneCalculator_ReturnsExcessWhenTouchdownIsBeyondZone()
    {
        var calculator = new TouchdownZoneCalculator();
        var runway = BuildRunway(headingDegrees: 180);
        var touchdown = Offset(runway.ThresholdLatitude, runway.ThresholdLongitude, runway.TrueHeadingDegrees, 3_450);

        var projection = calculator.ProjectTouchdown(runway, touchdown.Latitude, touchdown.Longitude);

        Assert.InRange(projection.DistanceFromThresholdFeet, 3_400, 3_500);
        Assert.InRange(projection.TouchdownZoneExcessDistanceFeet, 400, 500);
    }

    [Fact]
    public async Task RunwayResolver_SelectsRunwayByClosestHeadingWithinTolerance()
    {
        var northSouthRunway = BuildRunway(identifier: "18", headingDegrees: 180, thresholdLatitude: 40.0, thresholdLongitude: -75.0);
        var eastWestRunway = BuildRunway(identifier: "27", headingDegrees: 270, thresholdLatitude: 40.0, thresholdLongitude: -75.0);
        var provider = new StubRunwayDataProvider(
            new AirportRunwayCatalog
            {
                AirportIcao = "KTEST",
                DataSource = RunwayDataSource.SimConnectFacilityApi,
                Runways = new[] { northSouthRunway, eastWestRunway },
            });

        var touchdown = Offset(northSouthRunway.ThresholdLatitude, northSouthRunway.ThresholdLongitude, northSouthRunway.TrueHeadingDegrees, 3_200);
        var resolver = new RunwayResolver(provider);

        var result = await resolver.ResolveAsync(new RunwayResolutionRequest
        {
            ArrivalAirportIcao = "KTEST",
            TouchdownLatitude = touchdown.Latitude,
            TouchdownLongitude = touchdown.Longitude,
            TouchdownHeadingTrueDegrees = 176,
        });

        Assert.NotNull(result);
        Assert.Equal("18", result!.Runway.RunwayIdentifier);
        Assert.InRange(result.Projection.TouchdownZoneExcessDistanceFeet, 150, 250);
    }

    [Fact]
    public async Task RunwayResolver_ReturnsNullWhenNoRunwayMatchesHeadingTolerance()
    {
        var provider = new StubRunwayDataProvider(
            new AirportRunwayCatalog
            {
                AirportIcao = "KTEST",
                DataSource = RunwayDataSource.SimConnectFacilityApi,
                Runways = new[] { BuildRunway(identifier: "18", headingDegrees: 180) },
            });

        var resolver = new RunwayResolver(provider);
        var result = await resolver.ResolveAsync(new RunwayResolutionRequest
        {
            ArrivalAirportIcao = "KTEST",
            TouchdownLatitude = 40.0,
            TouchdownLongitude = -75.0,
            TouchdownHeadingTrueDegrees = 90,
        });

        Assert.Null(result);
    }

    [Fact]
    public async Task FallbackRunwayDataProvider_UsesFallbackWhenPrimaryHasNoRunways()
    {
        var primary = new StubRunwayDataProvider(null);
        var fallbackCatalog = new AirportRunwayCatalog
        {
            AirportIcao = "KTEST",
            DataSource = RunwayDataSource.OurAirportsFallback,
            Runways = new[] { BuildRunway(identifier: "18", headingDegrees: 180) },
        };

        var provider = new FallbackRunwayDataProvider(primary, new StubRunwayDataProvider(fallbackCatalog));
        var result = await provider.GetRunwaysAsync("KTEST");

        Assert.NotNull(result);
        Assert.Equal(RunwayDataSource.OurAirportsFallback, result!.DataSource);
        Assert.Single(result.Runways);
    }

    [Fact]
    public async Task FallbackRunwayDataProvider_UsesFallbackWhenPrimaryThrows()
    {
        var fallbackCatalog = new AirportRunwayCatalog
        {
            AirportIcao = "KTEST",
            DataSource = RunwayDataSource.OurAirportsFallback,
            Runways = new[] { BuildRunway(identifier: "18", headingDegrees: 180) },
        };

        var provider = new FallbackRunwayDataProvider(
            new ThrowingRunwayDataProvider(),
            new StubRunwayDataProvider(fallbackCatalog));

        var result = await provider.GetRunwaysAsync("KTEST");

        Assert.NotNull(result);
        Assert.Equal(RunwayDataSource.OurAirportsFallback, result!.DataSource);
    }

    [Fact]
    public async Task OurAirportsCsvRunwayDataProvider_ComputesThresholdFromDisplacement()
    {
        const string csv = """
id,airport_ref,airport_ident,length_ft,width_ft,surface,lighted,closed,le_ident,le_latitude_deg,le_longitude_deg,le_elevation_ft,le_heading_degT,le_displaced_threshold_ft,he_ident,he_latitude_deg,he_longitude_deg,he_elevation_ft,he_heading_degT,he_displaced_threshold_ft
1,1,KXYZ,10000,150,ASP,1,0,18,40.0000,-75.0000,0,180,500,36,39.9725,-75.0000,0,360,0
""";

        using var reader = new StringReader(csv);
        var provider = new OurAirportsCsvRunwayDataProvider(reader);

        var catalog = await provider.GetRunwaysAsync("KXYZ");

        Assert.NotNull(catalog);
        Assert.Equal(2, catalog!.Runways.Count);

        var runway18 = catalog.Runways.Single(runway => runway.RunwayIdentifier == "18");
        Assert.Equal(500, runway18.DisplacedThresholdFeet);
        Assert.True(runway18.ThresholdLatitude < 40.0000);
        Assert.Equal(RunwayDataSource.OurAirportsFallback, runway18.DataSource);
    }

    private static RunwayEnd BuildRunway(
        string identifier = "18",
        double headingDegrees = 180,
        double thresholdLatitude = 40.0,
        double thresholdLongitude = -75.0) =>
        new()
        {
            AirportIcao = "KTEST",
            RunwayIdentifier = identifier,
            TrueHeadingDegrees = headingDegrees,
            LengthFeet = 10_000,
            ThresholdLatitude = thresholdLatitude,
            ThresholdLongitude = thresholdLongitude,
            DataSource = RunwayDataSource.SimConnectFacilityApi,
        };

    private static (double Latitude, double Longitude) Offset(
        double latitude,
        double longitude,
        double headingDegrees,
        double distanceFeet)
    {
        const double EarthRadiusFeet = 20_925_524.9;
        var headingRad = headingDegrees * Math.PI / 180.0;
        var distanceRad = distanceFeet / EarthRadiusFeet;
        var startLatRad = latitude * Math.PI / 180.0;
        var startLonRad = longitude * Math.PI / 180.0;

        var destLatRad = Math.Asin(
            Math.Sin(startLatRad) * Math.Cos(distanceRad) +
            Math.Cos(startLatRad) * Math.Sin(distanceRad) * Math.Cos(headingRad));

        var destLonRad = startLonRad + Math.Atan2(
            Math.Sin(headingRad) * Math.Sin(distanceRad) * Math.Cos(startLatRad),
            Math.Cos(distanceRad) - Math.Sin(startLatRad) * Math.Sin(destLatRad));

        return (destLatRad * 180.0 / Math.PI, destLonRad * 180.0 / Math.PI);
    }

    private sealed class StubRunwayDataProvider(AirportRunwayCatalog? catalog) : IRunwayDataProvider
    {
        public Task<AirportRunwayCatalog?> GetRunwaysAsync(string airportIcao, CancellationToken cancellationToken = default) =>
            Task.FromResult(catalog);
    }

    private sealed class ThrowingRunwayDataProvider : IRunwayDataProvider
    {
        public Task<AirportRunwayCatalog?> GetRunwaysAsync(string airportIcao, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("primary failed");
    }
}
