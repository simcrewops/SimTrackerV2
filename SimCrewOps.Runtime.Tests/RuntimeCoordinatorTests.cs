using SimCrewOps.PhaseEngine.Models;
using SimCrewOps.Runways.Models;
using SimCrewOps.Runways.Providers;
using SimCrewOps.Runways.Services;
using SimCrewOps.Runtime.Models;
using SimCrewOps.Runtime.Runtime;
using SimCrewOps.Scoring.Models;
using SimCrewOps.Tracking.Models;
using Xunit;

namespace SimCrewOps.Runtime.Tests;

public sealed class RuntimeCoordinatorTests
{
    [Fact]
    public async Task RuntimeCoordinator_CapturesBlockTimesAndInjectsTouchdownZoneExcess()
    {
        var runway = new RunwayEnd
        {
            AirportIcao = "KTEST",
            RunwayIdentifier = "18",
            TrueHeadingDegrees = 180,
            LengthFeet = 10_000,
            ThresholdLatitude = 40.0,
            ThresholdLongitude = -75.0,
            DataSource = RunwayDataSource.OurAirportsFallback,
        };

        var provider = new StubRunwayDataProvider(new AirportRunwayCatalog
        {
            AirportIcao = "KTEST",
            DataSource = RunwayDataSource.OurAirportsFallback,
            Runways = new[] { runway },
        });

        var coordinator = new RuntimeCoordinator(
            new FlightSessionContext { DepartureAirportIcao = "KDEP", ArrivalAirportIcao = "KTEST" },
            new RunwayResolver(provider));

        var t0 = new DateTimeOffset(2026, 4, 13, 12, 0, 0, TimeSpan.Zero);

        await coordinator.ProcessFrameAsync(Frame(t0, onGround: true, parkingBrake: true));
        var blocksOff = await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(1), onGround: true, parkingBrake: false, groundSpeed: 2));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(30), onGround: true, indicatedAirspeed: 55));
        var wheelsOff = await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(31), onGround: false, altitudeAgl: 20, indicatedAirspeed: 90, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(40), onGround: false, altitudeAgl: 500, verticalSpeed: 1500, indicatedAirspeed: 160, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(100), onGround: false, altitudeAgl: 35_000, verticalSpeed: 0, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(131), onGround: false, altitudeAgl: 35_000, verticalSpeed: 0, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(200), onGround: false, altitudeAgl: 35_000, verticalSpeed: -600, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(231), onGround: false, altitudeAgl: 35_000, verticalSpeed: -600, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(290), onGround: false, altitudeAgl: 2_800, gearDown: true, verticalSpeed: -500, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(300), onGround: false, altitudeAgl: 100, heading: 180));

        var touchdownPoint = Offset(runway.ThresholdLatitude, runway.ThresholdLongitude, runway.TrueHeadingDegrees, 3_250);
        var wheelsOn = await coordinator.ProcessFrameAsync(Frame(
            t0.AddSeconds(310),
            onGround: true,
            latitude: touchdownPoint.Latitude,
            longitude: touchdownPoint.Longitude,
            altitudeAgl: 0,
            groundSpeed: 100,
            heading: 178));

        Assert.Equal(BlockEventType.BlocksOff, blocksOff.PhaseFrame.BlockEvent!.Type);
        Assert.Equal(BlockEventType.WheelsOff, wheelsOff.PhaseFrame.BlockEvent!.Type);
        Assert.Equal(BlockEventType.WheelsOn, wheelsOn.PhaseFrame.BlockEvent!.Type);
        Assert.NotNull(wheelsOn.RunwayResolution);
        Assert.Equal("18", wheelsOn.RunwayResolution!.Runway.RunwayIdentifier);
        Assert.InRange(wheelsOn.EnrichedTelemetryFrame.TouchdownZoneExcessDistanceFeet ?? -1, 200, 300);
        Assert.InRange(wheelsOn.State.ScoreInput.Landing.TouchdownZoneExcessDistanceFeet, 200, 300);
        Assert.NotNull(wheelsOn.State.BlockTimes.BlocksOffUtc);
        Assert.NotNull(wheelsOn.State.BlockTimes.WheelsOffUtc);
        Assert.NotNull(wheelsOn.State.BlockTimes.WheelsOnUtc);
        Assert.Equal(FlightPhase.Landing, wheelsOn.State.CurrentPhase);
    }

    [Fact]
    public async Task RuntimeCoordinator_SkipsRunwayResolutionWhenArrivalAirportIsMissing()
    {
        var coordinator = new RuntimeCoordinator(
            new FlightSessionContext(),
            new RunwayResolver(new StubRunwayDataProvider(null)));

        var t0 = new DateTimeOffset(2026, 4, 13, 13, 0, 0, TimeSpan.Zero);

        await coordinator.ProcessFrameAsync(Frame(t0, onGround: true, parkingBrake: true));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(1), onGround: true, parkingBrake: false, groundSpeed: 2));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(30), onGround: true, indicatedAirspeed: 55));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(31), onGround: false, altitudeAgl: 20, indicatedAirspeed: 90, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(40), onGround: false, altitudeAgl: 500, verticalSpeed: 1500, indicatedAirspeed: 160, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(100), onGround: false, altitudeAgl: 35_000, verticalSpeed: 0, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(131), onGround: false, altitudeAgl: 35_000, verticalSpeed: 0, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(200), onGround: false, altitudeAgl: 35_000, verticalSpeed: -600, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(231), onGround: false, altitudeAgl: 35_000, verticalSpeed: -600, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(290), onGround: false, altitudeAgl: 2_800, gearDown: true, verticalSpeed: -500, heading: 180));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(300), onGround: false, altitudeAgl: 100, heading: 180));

        var touchdown = await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(310), onGround: true, altitudeAgl: 0, groundSpeed: 100, heading: 180));

        Assert.Null(touchdown.RunwayResolution);
        Assert.Null(touchdown.EnrichedTelemetryFrame.TouchdownZoneExcessDistanceFeet);
        Assert.Equal(0, touchdown.State.ScoreInput.Landing.TouchdownZoneExcessDistanceFeet);
    }

    [Fact]
    public async Task RuntimeCoordinator_Restore_ContinuesSavedSessionWithoutRestarting()
    {
        var coordinator = new RuntimeCoordinator(
            new FlightSessionContext(),
            new RunwayResolver(new StubRunwayDataProvider(null)));

        var t0 = new DateTimeOffset(2026, 4, 13, 14, 0, 0, TimeSpan.Zero);
        coordinator.Restore(new FlightSessionRuntimeState
        {
            Context = new FlightSessionContext
            {
                DepartureAirportIcao = "KDEP",
                ArrivalAirportIcao = "KARR",
                Profile = new FlightSessionProfile
                {
                    HeavyFourEngineAircraft = true,
                    EngineCount = 4,
                },
            },
            CurrentPhase = FlightPhase.TaxiIn,
            BlockTimes = new FlightSessionBlockTimes
            {
                BlocksOffUtc = t0.AddHours(-2),
                WheelsOffUtc = t0.AddHours(-1.9),
                WheelsOnUtc = t0.AddMinutes(-5),
            },
            LastTelemetryFrame = Frame(
                t0,
                onGround: true,
                groundSpeed: 18,
                heading: 180) with
            {
                Phase = FlightPhase.TaxiIn,
                TaxiLightsOn = true,
                Engine1Running = true,
                Engine2Running = true,
                Engine3Running = true,
                Engine4Running = true,
            },
            ScoreInput = new FlightScoreInput
            {
                Preflight = new PreflightMetrics
                {
                    BeaconOnBeforeTaxi = true,
                },
                Climb = new ClimbMetrics
                {
                    HeavyFourEngineAircraft = true,
                },
            },
            ScoreResult = new ScoreResult(100, 92, "A", false, Array.Empty<PhaseScoreResult>(), Array.Empty<ScoreFinding>()),
        });

        var arrival = await coordinator.ProcessFrameAsync(
            Frame(
                t0.AddSeconds(1),
                onGround: true,
                parkingBrake: true,
                groundSpeed: 0,
                heading: 180) with
            {
                TaxiLightsOn = false,
                Engine1Running = true,
                Engine2Running = true,
                Engine3Running = true,
                Engine4Running = true,
            });

        Assert.Equal(FlightPhase.Arrival, arrival.State.CurrentPhase);
        Assert.NotNull(arrival.State.BlockTimes.BlocksOffUtc);
        Assert.NotNull(arrival.State.BlockTimes.WheelsOffUtc);
        Assert.NotNull(arrival.State.BlockTimes.WheelsOnUtc);
        Assert.NotNull(arrival.State.BlockTimes.BlocksOnUtc);
        Assert.True(arrival.State.ScoreInput.Preflight.BeaconOnBeforeTaxi);
        Assert.True(arrival.State.ScoreInput.Climb.HeavyFourEngineAircraft);
        Assert.Equal("KARR", arrival.State.Context.ArrivalAirportIcao);
    }

    [Fact]
    public async Task RuntimeCoordinator_SendsLivePositionDuringActiveFlight()
    {
        var uploader = new SpyLivePositionUploader();
        var coordinator = new RuntimeCoordinator(
            new FlightSessionContext
            {
                FlightMode = "career",
                BidId = "48291",
            },
            new RunwayResolver(new StubRunwayDataProvider(null)),
            livePositionUploader: uploader);

        var t0 = new DateTimeOffset(2026, 4, 13, 16, 0, 0, TimeSpan.Zero);

        await coordinator.ProcessFrameAsync(Frame(t0, onGround: true, parkingBrake: true));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(1), onGround: true, parkingBrake: false, groundSpeed: 2));

        Assert.Single(uploader.Payloads);
        Assert.Equal("TaxiOut", uploader.Payloads[0].Phase);
        Assert.Equal("career", uploader.Payloads[0].FlightMode);
        Assert.Equal("48291", uploader.Payloads[0].BidId);
    }

    [Fact]
    public async Task RuntimeCoordinator_ThrottlesLivePositionUploads()
    {
        var uploader = new SpyLivePositionUploader();
        var coordinator = new RuntimeCoordinator(
            new FlightSessionContext(),
            new RunwayResolver(new StubRunwayDataProvider(null)),
            livePositionUploader: uploader);

        var t0 = new DateTimeOffset(2026, 4, 13, 17, 0, 0, TimeSpan.Zero);

        await coordinator.ProcessFrameAsync(Frame(t0, onGround: true, parkingBrake: true, latitude: 40.0, longitude: -75.0));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(1), onGround: true, parkingBrake: false, groundSpeed: 2, latitude: 40.0, longitude: -75.0));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(2), onGround: true, groundSpeed: 4, latitude: 40.00001, longitude: -75.00001));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(3), onGround: true, groundSpeed: 5, latitude: 40.00002, longitude: -75.00002));
        await coordinator.ProcessFrameAsync(Frame(t0.AddSeconds(5), onGround: true, groundSpeed: 6, latitude: 40.00003, longitude: -75.00003));

        Assert.Equal(2, uploader.Payloads.Count);
    }

    private static TelemetryFrame Frame(
        DateTimeOffset timestampUtc,
        bool onGround,
        bool parkingBrake = false,
        bool gearDown = false,
        double latitude = 40.0,
        double longitude = -75.0,
        double indicatedAirspeed = 0,
        double altitudeAgl = 0,
        double groundSpeed = 0,
        double verticalSpeed = 0,
        double heading = 0)
    {
        return new TelemetryFrame
        {
            TimestampUtc = timestampUtc,
            Phase = FlightPhase.Preflight,
            OnGround = onGround,
            ParkingBrakeSet = parkingBrake,
            GearDown = gearDown,
            Latitude = latitude,
            Longitude = longitude,
            IndicatedAirspeedKnots = indicatedAirspeed,
            AltitudeFeet = altitudeAgl + 1000,
            AltitudeAglFeet = altitudeAgl,
            GroundSpeedKnots = groundSpeed,
            VerticalSpeedFpm = verticalSpeed,
            HeadingMagneticDegrees = heading,
            HeadingTrueDegrees = heading,
        };
    }

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

    private sealed class SpyLivePositionUploader : ILivePositionUploader
    {
        public List<LivePositionPayload> Payloads { get; } = [];

        public Task<bool> SendPositionAsync(LivePositionPayload payload, CancellationToken cancellationToken = default)
        {
            Payloads.Add(payload);
            return Task.FromResult(true);
        }
    }
}
