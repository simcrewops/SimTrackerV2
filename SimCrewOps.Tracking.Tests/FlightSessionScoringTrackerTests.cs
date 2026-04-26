using SimCrewOps.Scoring.Models;
using SimCrewOps.Tracking.Models;
using SimCrewOps.Tracking.Tracking;
using Xunit;

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
        tracker.Ingest(Frame(t0.AddSeconds(49), FlightPhase.TaxiIn, onGround: true, taxiLights: false, landingLights: false, strobes: false, groundSpeed: 3, heading: 90));
        tracker.Ingest(Frame(t0.AddSeconds(50), FlightPhase.Arrival, onGround: true, taxiLights: false, landingLights: false, strobes: false, parkingBrake: true, engine1: false, engine2: false));

        var input = tracker.BuildScoreInput();

        Assert.True(input.Preflight.BeaconOnBeforeTaxi);
        Assert.Equal(0, input.Takeoff.BounceCount);
        Assert.True(input.Takeoff.TailStrikeDetected);
        Assert.Equal(1.0, input.Takeoff.MaxGForce);
        Assert.True(input.Approach.GearDownBy1000Agl);
        Assert.Equal(3, input.Approach.FlapsHandleIndexAt500Agl);
        Assert.Equal(160, input.Landing.TouchdownVerticalSpeedFpm);
        Assert.Equal(0, input.Landing.TouchdownBankAngleDegrees);
        Assert.Equal(135, input.Landing.TouchdownIndicatedAirspeedKnots);
        Assert.Equal(0, input.Landing.TouchdownPitchAngleDegrees);
        Assert.Equal(1, input.Landing.BounceCount);
        Assert.Equal(120, input.Landing.TouchdownZoneExcessDistanceFeet);
        Assert.True(input.Arrival.TaxiLightsOffBeforeParkingBrakeSet);
        Assert.True(input.Arrival.AllEnginesOffBeforeParkingBrakeSet);
        Assert.True(input.Arrival.AllEnginesOffByEndOfSession);
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

    [Fact]
    public void TrackerCountsCruiseInstabilityAfterFiveSecondsAndAppliesCooldown()
    {
        var tracker = new FlightSessionScoringTracker();
        var t0 = new DateTimeOffset(2026, 4, 12, 23, 30, 0, TimeSpan.Zero);

        tracker.Ingest(Frame(t0.AddSeconds(0), FlightPhase.Cruise, onGround: false, altitude: 35000, agl: 33000, ias: 280, mach: 0.76, heading: 30));
        tracker.Ingest(Frame(t0.AddSeconds(1), FlightPhase.Cruise, onGround: false, altitude: 35010, agl: 33010, ias: 300, mach: 0.80, heading: 30));
        tracker.Ingest(Frame(t0.AddSeconds(3), FlightPhase.Cruise, onGround: false, altitude: 35010, agl: 33010, ias: 299, mach: 0.80, heading: 30));
        tracker.Ingest(Frame(t0.AddSeconds(6), FlightPhase.Cruise, onGround: false, altitude: 35010, agl: 33010, ias: 298, mach: 0.80, heading: 30));
        tracker.Ingest(Frame(t0.AddSeconds(12), FlightPhase.Cruise, onGround: false, altitude: 35020, agl: 33020, ias: 298, mach: 0.80, heading: 30));
        tracker.Ingest(Frame(t0.AddSeconds(17), FlightPhase.Cruise, onGround: false, altitude: 35020, agl: 33020, ias: 297, mach: 0.80, heading: 30));
        tracker.Ingest(Frame(t0.AddSeconds(18), FlightPhase.Cruise, onGround: false, altitude: 35020, agl: 33020, ias: 280, mach: 0.76, heading: 30));

        var input = tracker.BuildScoreInput();

        Assert.Equal(2, input.Cruise.SpeedInstabilityEvents);
    }

    [Fact]
    public void TrackerDoesNotPenaliseAltitudeDeviationDuringInitialLevelOff()
    {
        // Aircraft enters cruise while still climbing gently (VS 150 fpm).
        // The target should float for 30 s and lock at the settled altitude,
        // so the level-off overshoot never registers as a deviation.
        var tracker = new FlightSessionScoringTracker();
        var t0 = new DateTimeOffset(2026, 4, 13, 1, 0, 0, TimeSpan.Zero);

        // Entering cruise at 34850 ft, still climbing at 150 fpm.
        tracker.Ingest(Frame(t0.AddSeconds(0),  FlightPhase.Cruise, onGround: false, altitude: 34850, agl: 32850, ias: 280, mach: 0.76, vs: 150, heading: 90));
        tracker.Ingest(Frame(t0.AddSeconds(5),  FlightPhase.Cruise, onGround: false, altitude: 34900, agl: 32900, ias: 280, mach: 0.76, vs: 100, heading: 90));
        tracker.Ingest(Frame(t0.AddSeconds(10), FlightPhase.Cruise, onGround: false, altitude: 34950, agl: 32950, ias: 280, mach: 0.76, vs: 50,  heading: 90));
        // Aircraft fully levels at 35000 ft from t=15 s onwards.
        tracker.Ingest(Frame(t0.AddSeconds(15), FlightPhase.Cruise, onGround: false, altitude: 35000, agl: 33000, ias: 280, mach: 0.76, vs: 20,  heading: 90));
        tracker.Ingest(Frame(t0.AddSeconds(30), FlightPhase.Cruise, onGround: false, altitude: 35000, agl: 33000, ias: 280, mach: 0.76, vs: 10,  heading: 90));
        // t=45 s — 30 s after VS first dropped below 100 fpm at t=15 → now settled.
        tracker.Ingest(Frame(t0.AddSeconds(46), FlightPhase.Cruise, onGround: false, altitude: 35000, agl: 33000, ias: 280, mach: 0.76, vs: 0,   heading: 90));

        var input = tracker.BuildScoreInput();

        // Zero deviation: target tracked the aircraft during level-off and locked at 35000.
        Assert.Equal(0, input.Cruise.MaxAltitudeDeviationFeet);
    }

    [Fact]
    public void TrackerDoesNotPenaliseAltitudeDeviationDuringStepClimb()
    {
        // Settled at FL200, then a step climb to FL300.
        // Neither the climb itself nor the level-off at FL300 should register as
        // altitude deviations; the target should re-settle at FL300.
        var tracker = new FlightSessionScoringTracker();
        var t0 = new DateTimeOffset(2026, 4, 13, 1, 30, 0, TimeSpan.Zero);

        // Settle at FL200 — feed 35 s of low-VS frames so the target locks.
        tracker.Ingest(Frame(t0.AddSeconds(0),  FlightPhase.Cruise, onGround: false, altitude: 20000, agl: 18000, ias: 280, mach: 0.72, vs: 0,    heading: 90));
        tracker.Ingest(Frame(t0.AddSeconds(35), FlightPhase.Cruise, onGround: false, altitude: 20000, agl: 18000, ias: 280, mach: 0.72, vs: 0,    heading: 90));

        // Step climb begins — VS 2000 fpm.
        tracker.Ingest(Frame(t0.AddSeconds(40), FlightPhase.Cruise, onGround: false, altitude: 20500, agl: 18500, ias: 280, mach: 0.72, vs: 2000, heading: 90));
        tracker.Ingest(Frame(t0.AddSeconds(80), FlightPhase.Cruise, onGround: false, altitude: 25000, agl: 23000, ias: 280, mach: 0.74, vs: 2000, heading: 90));
        // Near FL300, leveling off — VS drops to 200 fpm.
        tracker.Ingest(Frame(t0.AddSeconds(120), FlightPhase.Cruise, onGround: false, altitude: 29800, agl: 27800, ias: 280, mach: 0.76, vs: 200, heading: 90));
        // FL300 reached, VS near zero from t=125.
        tracker.Ingest(Frame(t0.AddSeconds(125), FlightPhase.Cruise, onGround: false, altitude: 30000, agl: 28000, ias: 280, mach: 0.76, vs: 50,  heading: 90));
        // Settled at FL300 after 30 s of low VS (t=155+).
        tracker.Ingest(Frame(t0.AddSeconds(156), FlightPhase.Cruise, onGround: false, altitude: 30000, agl: 28000, ias: 280, mach: 0.76, vs: 0,   heading: 90));
        // A few more frames to confirm zero deviation at FL300.
        tracker.Ingest(Frame(t0.AddSeconds(160), FlightPhase.Cruise, onGround: false, altitude: 30000, agl: 28000, ias: 280, mach: 0.76, vs: 0,   heading: 90));

        var input = tracker.BuildScoreInput();

        // Target re-settled at FL300 — no deviation from the step climb.
        Assert.Equal(0, input.Cruise.MaxAltitudeDeviationFeet);
    }

    [Fact]
    public void TaxiInTaxiLights_PassWhenOffDuringGraceWindow()
    {
        // Taxi lights off immediately after vacating — should pass because the
        // 60-second grace window has not elapsed yet.
        var tracker = new FlightSessionScoringTracker();
        var t0 = new DateTimeOffset(2026, 4, 13, 2, 0, 0, TimeSpan.Zero);

        // Vacate runway: taxi in starts at t=0 with lights off at speed > 8 kts.
        tracker.Ingest(Frame(t0.AddSeconds(0),  FlightPhase.TaxiIn, onGround: true, taxiLights: false, landingLights: false, strobes: false, groundSpeed: 20, heading: 180));
        tracker.Ingest(Frame(t0.AddSeconds(30), FlightPhase.TaxiIn, onGround: true, taxiLights: false, landingLights: false, strobes: false, groundSpeed: 15, heading: 180));
        tracker.Ingest(Frame(t0.AddSeconds(59), FlightPhase.TaxiIn, onGround: true, taxiLights: false, landingLights: false, strobes: false, groundSpeed: 12, heading: 180));

        var input = tracker.BuildScoreInput();

        // Still within 60 s — no penalty.
        Assert.True(input.TaxiIn.TaxiLightsOn);
    }

    [Fact]
    public void TaxiInTaxiLights_FailWhenOffAfterGraceWindow()
    {
        // Taxi lights not turned on within 60 seconds of vacating — should fail.
        var tracker = new FlightSessionScoringTracker();
        var t0 = new DateTimeOffset(2026, 4, 13, 2, 30, 0, TimeSpan.Zero);

        tracker.Ingest(Frame(t0.AddSeconds(0),  FlightPhase.TaxiIn, onGround: true, taxiLights: false, landingLights: false, strobes: false, groundSpeed: 20, heading: 180));
        // Past 60 s + 3 s debounce, still no taxi lights, still above 8 kts.
        tracker.Ingest(Frame(t0.AddSeconds(64), FlightPhase.TaxiIn, onGround: true, taxiLights: false, landingLights: false, strobes: false, groundSpeed: 12, heading: 180));

        var input = tracker.BuildScoreInput();

        Assert.False(input.TaxiIn.TaxiLightsOn);
    }

    [Fact]
    public void TrackerPassesArrival_WhenAllEnginesOffBeforeParkingBrake()
    {
        // SOPs: engines must be shut down BEFORE setting the parking brake.
        // Engines off first, then PB set → no violation.
        var tracker = new FlightSessionScoringTracker();
        var t0 = new DateTimeOffset(2026, 4, 12, 23, 45, 0, TimeSpan.Zero);

        tracker.Ingest(Frame(t0.AddSeconds(0), FlightPhase.TaxiIn, onGround: true, taxiLights: true, landingLights: false, strobes: false, groundSpeed: 8, heading: 180));
        tracker.Ingest(Frame(t0.AddSeconds(5), FlightPhase.TaxiIn, onGround: true, taxiLights: false, landingLights: false, strobes: false, groundSpeed: 2, heading: 180, engine1: false, engine2: false));
        tracker.Ingest(Frame(t0.AddSeconds(8), FlightPhase.Arrival, onGround: true, taxiLights: false, landingLights: false, strobes: false, parkingBrake: true, engine1: false, engine2: false));

        var input = tracker.BuildScoreInput();

        Assert.True(input.Arrival.TaxiLightsOffBeforeParkingBrakeSet);
        Assert.True(input.Arrival.AllEnginesOffBeforeParkingBrakeSet);
        Assert.True(input.Arrival.AllEnginesOffByEndOfSession);
    }

    [Fact]
    public void TrackerMarksArrivalViolation_WhenParkingBrakeSetWhileEngineStillRunning()
    {
        // SOPs: parking brake must NOT be set while any engine is still running.
        var tracker = new FlightSessionScoringTracker();
        var t0 = new DateTimeOffset(2026, 4, 13, 0, 15, 0, TimeSpan.Zero);

        tracker.Ingest(Frame(t0.AddSeconds(0), FlightPhase.TaxiIn, onGround: true, taxiLights: true, landingLights: false, strobes: false, groundSpeed: 8, heading: 180));
        // Parking brake set while engine1 is still running → violation
        tracker.Ingest(Frame(t0.AddSeconds(5), FlightPhase.Arrival, onGround: true, taxiLights: false, landingLights: false, strobes: false, parkingBrake: true, engine1: true, engine2: false));
        tracker.Ingest(Frame(t0.AddSeconds(10), FlightPhase.Arrival, onGround: true, taxiLights: false, landingLights: false, strobes: false, parkingBrake: true, engine1: false, engine2: false));

        var input = tracker.BuildScoreInput();

        Assert.True(input.Arrival.TaxiLightsOffBeforeParkingBrakeSet);
        Assert.False(input.Arrival.AllEnginesOffBeforeParkingBrakeSet);
        Assert.True(input.Arrival.AllEnginesOffByEndOfSession);
    }

    [Fact]
    public void TrackerPassesTaxiLights_WhenOffOnSameFrameAsBrake()
    {
        // Taxi lights off on the exact frame the parking brake is set — counts as passing.
        // The checklist order (brake → lights off) is satisfied even if both happen in the
        // same telemetry frame.
        var tracker = new FlightSessionScoringTracker();
        var t0 = new DateTimeOffset(2026, 4, 13, 0, 0, 0, TimeSpan.Zero);

        tracker.Ingest(Frame(t0.AddSeconds(0), FlightPhase.TaxiIn, onGround: true, taxiLights: true, landingLights: false, strobes: false, groundSpeed: 5, heading: 270));
        tracker.Ingest(Frame(t0.AddSeconds(5), FlightPhase.Arrival, onGround: true, taxiLights: false, landingLights: false, strobes: false, parkingBrake: true, engine1: false, engine2: false));

        var input = tracker.BuildScoreInput();

        Assert.True(input.Arrival.TaxiLightsOffBeforeParkingBrakeSet);
        Assert.True(input.Arrival.AllEnginesOffBeforeParkingBrakeSet);
        Assert.True(input.Arrival.AllEnginesOffByEndOfSession);
    }

    [Fact]
    public void TrackerFailsTaxiLights_WhenStillOnAfterParkingBrake()
    {
        // Taxi lights left on after parking brake is set — deduction stays until lights go off.
        var tracker = new FlightSessionScoringTracker();
        var t0 = new DateTimeOffset(2026, 4, 13, 0, 0, 0, TimeSpan.Zero);

        tracker.Ingest(Frame(t0.AddSeconds(0), FlightPhase.TaxiIn, onGround: true, taxiLights: true, landingLights: false, strobes: false, groundSpeed: 5, heading: 270));
        tracker.Ingest(Frame(t0.AddSeconds(5), FlightPhase.Arrival, onGround: true, taxiLights: true, landingLights: false, strobes: false, parkingBrake: true, engine1: false, engine2: false));

        var input = tracker.BuildScoreInput();

        Assert.False(input.Arrival.TaxiLightsOffBeforeParkingBrakeSet);
        Assert.True(input.Arrival.AllEnginesOffBeforeParkingBrakeSet);
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
        double? touchdownZoneExcess = null)
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
        };
    }
}
