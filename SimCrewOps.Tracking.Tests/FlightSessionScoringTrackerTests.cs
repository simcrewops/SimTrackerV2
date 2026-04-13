using SimCrewOps.Scoring.Models;
using SimCrewOps.Tracking.Models;
using SimCrewOps.Tracking.Tracking;

namespace SimCrewOps.Tracking.Tests;

public sealed class FlightSessionScoringTrackerTests
{
    [Fact]
    public void TrackerCapturesApproachTouchdownAndLandingBounceMetrics()
    {
        var tracker = new FlightSessionScoringTracker();
        var t0 = new DateTimeOffset(2026, 4, 12, 22, 0, 0, TimeSpan.Zero);

        tracker.Ingest(Frame(t0.AddSeconds(0), FlightPhase.Preflight, onGround: true, beacon: true, parkingBrake: true));
        tracker.Ingest(Frame(t0.AddSeconds(10), FlightPhase.TaxiOut, onGround: true, taxiLights: true, groundSpeed: 18, heading: 0));
        tracker.Ingest(Frame(t0.AddSeconds(12), FlightPhase.TaxiOut, onGround: true, taxiLights: true, groundSpeed: 20, heading: 52));

        tracker.Ingest(Frame(t0.AddSeconds(20), FlightPhase.Takeoff, onGround: true, landingLights: true, strobes: true, ias: 80, pitch: 11, heading: 90));
        tracker.Ingest(Frame(t0.AddSeconds(22), FlightPhase.Takeoff, onGround: false, landingLights: true, strobes: true, agl: 20, ias: 145, vs: -100, heading: 90));
        tracker.Ingest(Frame(t0.AddSeconds(23), FlightPhase.Climb, onGround: false, landingLights: true, strobes: true, altitude: 4000, agl: 1200, ias: 240, bank: 12, g: 1.2, heading: 90));

        tracker.Ingest(Frame(t0.AddSeconds(30), FlightPhase.Approach, onGround: false, gearDown: true, flaps: 2, altitude: 3200, agl: 1200, ias: 155, vs: -700, bank: 6, pitch: 3, heading: 90));
        tracker.Ingest(Frame(t0.AddSeconds(31), FlightPhase.Approach, onGround: false, gearDown: true, flaps: 3, altitude: 2800, agl: 950, ias: 150, vs: -750, bank: 5, pitch: 2, heading: 90));
        tracker.Ingest(Frame(t0.AddSeconds(32), FlightPhase.Approach, onGround: false, gearDown: true, flaps: 3, altitude: 2300, agl: 480, ias: 145, vs: -180, bank: 4, pitch: 2, heading: 90, g: 1.18));
        tracker.Ingest(Frame(t0.AddSeconds(33), FlightPhase.Landing, onGround: true, gearDown: true, flaps: 3, altitude: 2200, agl: 0, ias: 135, vs: -160, heading: 90, g: 1.23, touchdownZoneExcess: 120));
        tracker.Ingest(Frame(t0.AddSeconds(34), FlightPhase.Landing, onGround: false, gearDown: true, flaps: 3, altitude: 2210, agl: 10, ias: 120, vs: 400, heading: 90));
        tracker.Ingest(Frame(t0.AddSeconds(35), FlightPhase.Landing, onGround: true, gearDown: true, flaps: 3, altitude: 2200, agl: 0, ias: 110, vs: -120, heading: 90, g: 1.12));

        tracker.Ingest(Frame(t0.AddSeconds(40), FlightPhase.TaxiIn, onGround: true, taxiLights: true, landingLights: false, strobes: false, groundSpeed: 18, heading: 90));
        tracker.Ingest(Frame(t0.AddSeconds(50), FlightPhase.Arrival, onGround: true, taxiLights: true, landingLights: false, strobes: false, parkingBrake: true, gateDistance: 14));

        var input = tracker.BuildScoreInput();

        Assert.True(input.Preflight.BeaconOnBeforeTaxi);
        Assert.Equal(0, input.Takeoff.BounceCount);
        Assert.True(input.Takeoff.TailStrikeDetected);
        Assert.True(input.Approach.GearDownBy1000Agl);
        Assert.Equal(3, input.Approach.FlapsHandleIndexAt500Agl);
        Assert.Equal(160, input.Landing.TouchdownVerticalSpeedFpm);
        Assert.Equal(1, input.Landing.BounceCount);
        Assert.Equal(120, input.Landing.TouchdownZoneExcessDistanceFeet);
        Assert.True(input.Arrival.ParkingBrakeSetAtGate);
        Assert.Equal(14, input.Arrival.GateArrivalDistanceFeet);
    }

    [Fact]
    public void TrackerCountsTakeoffBounceAndSafetyEvents()
    {
        var tracker = new FlightSessionScoringTracker(new FlightSessionProfile { HeavyFourEngineAircraft = true, EngineCount = 4 });
        var t0 = new DateTimeOffset(2026, 4, 12, 23, 0, 0, TimeSpan.Zero);

        tracker.Ingest(Frame(t0.AddSeconds(0), FlightPhase.Preflight, onGround: true, beacon: true));
        tracker.Ingest(Frame(t0.AddSeconds(5), FlightPhase.TaxiOut, onGround: true, taxiLights: true, groundSpeed: 12, heading: 10));
        tracker.Ingest(Frame(t0.AddSeconds(10), FlightPhase.Takeoff, onGround: true, landingLights: true, strobes: true, ias: 90, pitch: 12, heading: 20, engine3: true, engine4: true));
        tracker.Ingest(Frame(t0.AddSeconds(11), FlightPhase.Takeoff, onGround: false, landingLights: true, strobes: true, agl: 15, ias: 130, vs: -90, heading: 20, engine3: true, engine4: true));
        tracker.Ingest(Frame(t0.AddSeconds(13), FlightPhase.Takeoff, onGround: true, landingLights: true, strobes: true, agl: 0, ias: 115, vs: -50, heading: 20, engine3: true, engine4: true));
        tracker.Ingest(Frame(t0.AddSeconds(20), FlightPhase.Climb, onGround: false, altitude: 9000, agl: 2500, ias: 301, bank: 35, g: 1.9, overspeed: true, heading: 20, engine3: true, engine4: true));
        tracker.Ingest(Frame(t0.AddSeconds(55), FlightPhase.Climb, onGround: false, altitude: 12000, agl: 5000, ias: 305, bank: 22, g: 1.3, overspeed: true, gpws: true, stall: true, heading: 20, engine3: false, engine4: true));
        tracker.Ingest(Frame(t0.AddSeconds(60), FlightPhase.Cruise, onGround: false, altitude: 35000, agl: 33000, ias: 280, mach: 0.78, bank: 5, g: 1.0, overspeed: false, gpws: false, stall: false, heading: 20, engine3: false, engine4: true));

        var input = tracker.BuildScoreInput();

        Assert.Equal(1, input.Takeoff.BounceCount);
        Assert.True(input.Takeoff.TailStrikeDetected);
        Assert.Equal(301, input.Climb.MaxIasBelowFl100Knots);
        Assert.Equal(1, input.Safety.OverspeedEvents);
        Assert.Equal(1, input.Safety.SustainedOverspeedEvents);
        Assert.Equal(1, input.Safety.StallEvents);
        Assert.Equal(1, input.Safety.GpwsEvents);
        Assert.Equal(1, input.Safety.EngineShutdownsInFlight);
    }

    private static TelemetryFrame Frame(
        DateTimeOffset timestamp,
        FlightPhase phase,
        bool onGround,
        bool beacon = false,
        bool taxiLights = false,
        bool landingLights = false,
        bool strobes = false,
        bool parkingBrake = false,
        bool gearDown = true,
        int flaps = 0,
        double groundSpeed = 0,
        double ias = 0,
        double mach = 0,
        double altitude = 0,
        double agl = 0,
        double vs = 0,
        double bank = 0,
        double pitch = 0,
        double heading = 0,
        double g = 1.0,
        bool overspeed = false,
        bool stall = false,
        bool gpws = false,
        bool engine1 = true,
        bool engine2 = true,
        bool engine3 = false,
        bool engine4 = false,
        double? touchdownZoneExcess = null,
        double? gateDistance = null)
    {
        return new TelemetryFrame
        {
            TimestampUtc = timestamp,
            Phase = phase,
            OnGround = onGround,
            BeaconLightOn = beacon,
            TaxiLightsOn = taxiLights,
            LandingLightsOn = landingLights,
            StrobesOn = strobes,
            ParkingBrakeSet = parkingBrake,
            GearDown = gearDown,
            FlapsHandleIndex = flaps,
            GroundSpeedKnots = groundSpeed,
            IndicatedAirspeedKnots = ias,
            Mach = mach,
            IndicatedAltitudeFeet = altitude,
            AltitudeAglFeet = agl,
            VerticalSpeedFpm = vs,
            BankAngleDegrees = bank,
            PitchAngleDegrees = pitch,
            HeadingTrueDegrees = heading,
            GForce = g,
            OverspeedWarning = overspeed,
            StallWarning = stall,
            GpwsAlert = gpws,
            Engine1Running = engine1,
            Engine2Running = engine2,
            Engine3Running = engine3,
            Engine4Running = engine4,
            TouchdownZoneExcessDistanceFeet = touchdownZoneExcess,
            GateArrivalDistanceFeet = gateDistance,
        };
    }
}
