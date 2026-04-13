using SimCrewOps.Runways.Models;
using SimCrewOps.Runways.Providers;
using SimCrewOps.SimConnect.Services;
using Xunit;

namespace SimCrewOps.SimConnect.Tests;

public sealed class SimConnectFacilityRunwayProviderTests
{
    [Fact]
    public async Task GetRunwaysAsync_MapsRunwayEndsFromFacilitySnapshot()
    {
        var provider = new SimConnectFacilityRunwayProvider(
            new StubFacilityDataSource(
                new SimConnectFacilityRunwayProvider.SimConnectAirportFacilitySnapshot
                {
                    AirportIcao = "KJFK",
                    Runways =
                    [
                        new SimConnectFacilityRunwayProvider.SimConnectFacilityRunway
                        {
                            AirportIcao = "KJFK",
                            CenterLatitude = 40.6400,
                            CenterLongitude = -73.7800,
                            HeadingTrueDegrees = 44.0,
                            LengthFeet = 12_000,
                            PrimaryNumber = 4,
                            PrimaryDesignator = 2,
                            SecondaryNumber = 22,
                            SecondaryDesignator = 1,
                            HasPrimaryThresholdData = true,
                            HasSecondaryThresholdData = true,
                            PrimaryThresholdLengthFeet = 1_200,
                            SecondaryThresholdLengthFeet = 0,
                        },
                    ],
                }));

        var catalog = await provider.GetRunwaysAsync("kjfk");

        Assert.NotNull(catalog);
        Assert.Equal("KJFK", catalog!.AirportIcao);
        Assert.Equal(RunwayDataSource.SimConnectFacilityApi, catalog.DataSource);
        Assert.Equal(2, catalog.Runways.Count);

        var runway04R = catalog.Runways.Single(runway => runway.RunwayIdentifier == "04R");
        var runway22L = catalog.Runways.Single(runway => runway.RunwayIdentifier == "22L");

        Assert.Equal(1_200, runway04R.DisplacedThresholdFeet);
        Assert.Equal(0, runway22L.DisplacedThresholdFeet);
        Assert.Equal(44.0, runway04R.TrueHeadingDegrees, 1);
        Assert.Equal(224.0, runway22L.TrueHeadingDegrees, 1);
        Assert.Equal(RunwayDataSource.SimConnectFacilityApi, runway04R.DataSource);
        Assert.NotEqual(runway04R.ThresholdLatitude, runway22L.ThresholdLatitude);
    }

    [Fact]
    public async Task GetRunwaysAsync_ReturnsNullWhenThresholdDataIsIncomplete()
    {
        var provider = new SimConnectFacilityRunwayProvider(
            new StubFacilityDataSource(
                new SimConnectFacilityRunwayProvider.SimConnectAirportFacilitySnapshot
                {
                    AirportIcao = "KSEA",
                    Runways =
                    [
                        new SimConnectFacilityRunwayProvider.SimConnectFacilityRunway
                        {
                            AirportIcao = "KSEA",
                            CenterLatitude = 47.4489,
                            CenterLongitude = -122.3094,
                            HeadingTrueDegrees = 164.0,
                            LengthFeet = 11_900,
                            PrimaryNumber = 16,
                            PrimaryDesignator = 0,
                            SecondaryNumber = 34,
                            SecondaryDesignator = 0,
                            HasPrimaryThresholdData = true,
                            HasSecondaryThresholdData = false,
                            PrimaryThresholdLengthFeet = 0,
                            SecondaryThresholdLengthFeet = 0,
                        },
                    ],
                }));

        var catalog = await provider.GetRunwaysAsync("KSEA");

        Assert.Null(catalog);
    }

    [Theory]
    [InlineData(4, 1, "04L")]
    [InlineData(4, 2, "04R")]
    [InlineData(22, 3, "22C")]
    [InlineData(41, 0, "S")]
    [InlineData(0, 0, null)]
    public void BuildRunwayIdentifier_FormatsExpectedLabels(int number, int designator, string? expected)
    {
        Assert.Equal(expected, SimConnectFacilityRunwayProvider.BuildRunwayIdentifier(number, designator));
    }

    private sealed class StubFacilityDataSource(
        SimConnectFacilityRunwayProvider.SimConnectAirportFacilitySnapshot? snapshot)
        : SimConnectFacilityRunwayProvider.ISimConnectFacilityDataSource
    {
        public Task<SimConnectFacilityRunwayProvider.SimConnectAirportFacilitySnapshot?> GetRunwaysAsync(
            string airportIcao,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);
    }
}
