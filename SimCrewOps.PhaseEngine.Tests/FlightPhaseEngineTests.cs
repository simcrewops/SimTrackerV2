using SimCrewOps.PhaseEngine.Models;
using SimCrewOps.PhaseEngine.PhaseEngine;
using SimCrewOps.Scoring.Models;
using SimCrewOps.Tracking.Models;
using Xunit;

namespace SimCrewOps.PhaseEngine.Tests;

public sealed class FlightPhaseEngineTests
{
    // -------------------------------------------------------------------------
    //  Test 1 — Full flight: all four block events fire in order
    // -------------------------------------------------------------------------

    [Fact]
    public void FullFlight_FiresAllFourBlockEventsInOrder()
    {
        var engine = new FlightPhaseEngine();
        var t0 = new DateTimeOffset(2026, 4, 12, 10, 0, 0, TimeSpan.Zero);

        // PREFLIGHT — parked, brake on
        engine.Process(Frame(t0, onGround: true, parkingBrake: true, gs: 0));

        // PREFLIGHT → TAXI OUT — brake released, rolling → BlocksOff
        var blocksOffFrame = engine.Process(Frame(t0.AddSeconds(1), onGround: true, parkingBrake: false, gs: 2));
        Assert.Equal(FlightPhase.TaxiOut, blocksOffFrame.Phase);
        Assert.NotNull(blocksOffFrame.BlockEvent);
        Assert.Equal(BlockEventType.BlocksOff, blocksOffFrame.BlockEvent!.Type);

        // TAXI OUT → TAKEOFF — IAS climbs above 40 kts
        engine.Process(Frame(t0.AddSeconds(30), onGround: true, ias: 50));

        // TAKEOFF — lift off → WheelsOff
        engine.Process(Frame(t0.AddSeconds(31), onGround: true, ias: 80, agl: 0));
        var wheelsOffFrame = engine.Process(Frame(t0.AddSeconds(32), onGround: false, ias: 100, agl: 20));
        Assert.Equal(BlockEventType.WheelsOff, wheelsOffFrame.BlockEvent!.Type);

        // TAKEOFF → CLIMB — past 400 ft AGL
        engine.Process(Frame(t0.AddSeconds(40), onGround: false, ias: 160, agl: 500, vs: 1500));

        // CLIMB → CRUISE — VS level for 31 s
        engine.Process(Frame(t0.AddSeconds(100), onGround: false, agl: 35000, vs: 50));
        var cruiseFrame = engine.Process(Frame(t0.AddSeconds(132), onGround: false, agl: 35000, vs: 50));
        Assert.Equal(FlightPhase.Cruise, cruiseFrame.Phase);

        // CRUISE → DESCENT — VS negative for 31 s
        engine.Process(Frame(t0.AddSeconds(200), onGround: false, agl: 35000, vs: -500));
        var descentFrame = engine.Process(Frame(t0.AddSeconds(232), onGround: false, agl: 35000, vs: -500));
        Assert.Equal(FlightPhase.Descent, descentFrame.Phase);

        // DESCENT → APPROACH — below 3000 ft AGL with gear down
        var approachFrame = engine.Process(Frame(t0.AddSeconds(300), onGround: false, agl: 2500, gearDown: true));
        Assert.Equal(FlightPhase.Approach, approachFrame.Phase);

        // APPROACH → LANDING — touchdown → WheelsOn
        engine.Process(Frame(t0.AddSeconds(310), onGround: false, agl: 200));
        var wheelsOnFrame = engine.Process(Frame(t0.AddSeconds(320), onGround: true, agl: 0, gs: 120));
        Assert.Equal(FlightPhase.Landing, wheelsOnFrame.Phase);
        Assert.NotNull(wheelsOnFrame.BlockEvent);
        Assert.Equal(BlockEventType.WheelsOn, wheelsOnFrame.BlockEvent!.Type);

        // LANDING → TAXI IN — GS below 40 kts for 6 s
        engine.Process(Frame(t0.AddSeconds(340), onGround: true, gs: 30));
        var taxiInFrame = engine.Process(Frame(t0.AddSeconds(347), onGround: true, gs: 20));
        Assert.Equal(FlightPhase.TaxiIn, taxiInFrame.Phase);

        // TAXI IN → ARRIVAL — brake on, stationary → BlocksOn
        var blocksOnFrame = engine.Process(Frame(t0.AddSeconds(400), onGround: true, parkingBrake: true, gs: 0));
        Assert.Equal(FlightPhase.Arrival, blocksOnFrame.Phase);
        Assert.NotNull(blocksOnFrame.BlockEvent);
        Assert.Equal(BlockEventType.BlocksOn, blocksOnFrame.BlockEvent!.Type);

        // All four events present in order
        var events = engine.BlockEvents;
        Assert.Equal(4, events.Count);
        Assert.Equal(BlockEventType.BlocksOff, events[0].Type);
        Assert.Equal(BlockEventType.WheelsOff, events[1].Type);
        Assert.Equal(BlockEventType.WheelsOn, events[2].Type);
        Assert.Equal(BlockEventType.BlocksOn, events[3].Type);

        // Events are strictly ascending in time
        for (var i = 1; i < events.Count; i++)
        {
            Assert.True(events[i].TimestampUtc >= events[i - 1].TimestampUtc);
        }
    }

    // -------------------------------------------------------------------------
    //  Test 2 — Go-around: Approach → climb → second approach → Landing
    // -------------------------------------------------------------------------

    [Fact]
    public void GoAround_TransitionsBackToClimbThenLandsOnSecondApproach()
    {
        var engine = new FlightPhaseEngine();
        var t0 = new DateTimeOffset(2026, 4, 12, 11, 0, 0, TimeSpan.Zero);

        AdvanceToApproach(engine, t0);

        // First approach — low and slow, gear down
        engine.Process(Frame(t0.AddSeconds(300), onGround: false, agl: 1000, gearDown: true));
        engine.Process(Frame(t0.AddSeconds(305), onGround: false, agl: 350, gearDown: true, vs: -500));

        // Go-around: aircraft climbs back through 400 ft while still airborne
        var goAroundFrame = engine.Process(Frame(t0.AddSeconds(310), onGround: false, agl: 600, gearDown: true, vs: 1200));
        Assert.Equal(FlightPhase.Climb, goAroundFrame.Phase);

        // Descend again into another approach
        engine.Process(Frame(t0.AddSeconds(330), onGround: false, agl: 3200, vs: -300));
        var backToApproach = engine.Process(Frame(t0.AddSeconds(340), onGround: false, agl: 2800, gearDown: true, vs: -300));
        Assert.Equal(FlightPhase.Approach, backToApproach.Phase);

        // Touchdown on second approach
        engine.Process(Frame(t0.AddSeconds(350), onGround: false, agl: 100));
        var landingFrame = engine.Process(Frame(t0.AddSeconds(360), onGround: true, agl: 0, gs: 100));
        Assert.Equal(FlightPhase.Landing, landingFrame.Phase);
        Assert.Equal(BlockEventType.WheelsOn, landingFrame.BlockEvent!.Type);
    }

    // -------------------------------------------------------------------------
    //  Test 3 — Aborted takeoff: Takeoff → IAS drops → back to TaxiOut
    // -------------------------------------------------------------------------

    [Fact]
    public void AbortedTakeoff_TransitionsBackToTaxiOut()
    {
        var engine = new FlightPhaseEngine();
        var t0 = new DateTimeOffset(2026, 4, 12, 12, 0, 0, TimeSpan.Zero);

        // Preflight → TaxiOut
        engine.Process(Frame(t0, onGround: true, parkingBrake: true));
        engine.Process(Frame(t0.AddSeconds(1), onGround: true, parkingBrake: false, gs: 2));
        Assert.Equal(FlightPhase.TaxiOut, engine.CurrentPhase);

        // Accelerate onto runway → Takeoff
        engine.Process(Frame(t0.AddSeconds(30), onGround: true, ias: 50));
        Assert.Equal(FlightPhase.Takeoff, engine.CurrentPhase);

        // Reject: IAS drops back below 40 while still on ground
        var abortFrame = engine.Process(Frame(t0.AddSeconds(35), onGround: true, ias: 30));
        Assert.Equal(FlightPhase.TaxiOut, abortFrame.Phase);

        // Can accelerate again and re-enter Takeoff
        engine.Process(Frame(t0.AddSeconds(60), onGround: true, ias: 55));
        Assert.Equal(FlightPhase.Takeoff, engine.CurrentPhase);
    }

    // -------------------------------------------------------------------------
    //  Test 4 — Sustained VS thresholds drive Climb→Cruise and Cruise→Descent
    // -------------------------------------------------------------------------

    [Fact]
    public void SustainedVerticalSpeed_DrivesClimbToCruiseAndCruiseToDescent()
    {
        var engine = new FlightPhaseEngine();
        var t0 = new DateTimeOffset(2026, 4, 12, 13, 0, 0, TimeSpan.Zero);

        // Advance to Climb
        AdvanceToClimb(engine, t0);
        Assert.Equal(FlightPhase.Climb, engine.CurrentPhase);

        // Level VS but only for 29 s — should NOT transition yet
        engine.Process(Frame(t0.AddSeconds(100), onGround: false, agl: 35000, vs: 0));
        var notYetCruise = engine.Process(Frame(t0.AddSeconds(129), onGround: false, agl: 35000, vs: 0));
        Assert.Equal(FlightPhase.Climb, notYetCruise.Phase);

        // One more frame at 30 s — transitions to Cruise
        var cruiseFrame = engine.Process(Frame(t0.AddSeconds(131), onGround: false, agl: 35000, vs: 0));
        Assert.Equal(FlightPhase.Cruise, cruiseFrame.Phase);

        // Descending VS but only for 29 s — should NOT transition to Descent yet
        engine.Process(Frame(t0.AddSeconds(200), onGround: false, agl: 35000, vs: -500));
        var notYetDescent = engine.Process(Frame(t0.AddSeconds(229), onGround: false, agl: 35000, vs: -500));
        Assert.Equal(FlightPhase.Cruise, notYetDescent.Phase);

        // One more frame beyond 30 s — transitions to Descent
        var descentFrame = engine.Process(Frame(t0.AddSeconds(231), onGround: false, agl: 35000, vs: -500));
        Assert.Equal(FlightPhase.Descent, descentFrame.Phase);
    }

    // -------------------------------------------------------------------------
    //  Test 5 — Touch-and-go: WheelsOn fired then cancelled; second landing OK
    // -------------------------------------------------------------------------

    [Fact]
    public void TouchAndGo_WheelsOnCancelledWhenAirborneWithinThreeSeconds()
    {
        var engine = new FlightPhaseEngine();
        var t0 = new DateTimeOffset(2026, 4, 12, 14, 0, 0, TimeSpan.Zero);

        AdvanceToApproach(engine, t0);

        // Descend into Approach
        engine.Process(Frame(t0.AddSeconds(300), onGround: false, agl: 1000, gearDown: true));

        // First touchdown — WheelsOn fires, phase → Landing
        engine.Process(Frame(t0.AddSeconds(310), onGround: false, agl: 100));
        var touchdownFrame = engine.Process(Frame(t0.AddSeconds(320), onGround: true, agl: 0, gs: 100));
        Assert.Equal(FlightPhase.Landing, touchdownFrame.Phase);
        Assert.Equal(BlockEventType.WheelsOn, touchdownFrame.BlockEvent!.Type);
        Assert.Single(engine.BlockEvents.Where(e => e.Type == BlockEventType.WheelsOn));

        // Touch-and-go: airborne again within 3 s — WheelsOn cancelled, phase → Climb
        var tagFrame = engine.Process(Frame(t0.AddSeconds(322), onGround: false, agl: 50, gs: 120));
        Assert.Equal(FlightPhase.Climb, tagFrame.Phase);
        Assert.Null(tagFrame.BlockEvent);
        Assert.DoesNotContain(engine.BlockEvents, e => e.Type == BlockEventType.WheelsOn);

        // Fly a normal second approach and landing — WheelsOn fires again
        engine.Process(Frame(t0.AddSeconds(330), onGround: false, agl: 3200, vs: -300));
        engine.Process(Frame(t0.AddSeconds(350), onGround: false, agl: 2800, gearDown: true, vs: -300));
        engine.Process(Frame(t0.AddSeconds(360), onGround: false, agl: 200));
        var secondTouchdown = engine.Process(Frame(t0.AddSeconds(370), onGround: true, agl: 0, gs: 90));
        Assert.Equal(FlightPhase.Landing, secondTouchdown.Phase);
        Assert.Equal(BlockEventType.WheelsOn, secondTouchdown.BlockEvent!.Type);
        Assert.Single(engine.BlockEvents.Where(e => e.Type == BlockEventType.WheelsOn));
    }

    [Fact]
    public void Restore_ContinuesTaxiInSessionIntoArrival()
    {
        var engine = new FlightPhaseEngine();
        var t0 = new DateTimeOffset(2026, 4, 12, 15, 0, 0, TimeSpan.Zero);
        var previousFrame = Frame(t0, onGround: true, gs: 18);

        engine.Restore(
            FlightPhase.TaxiIn,
            previousFrame,
            new[]
            {
                new BlockEvent { Type = BlockEventType.BlocksOff, TimestampUtc = t0.AddMinutes(-45) },
                new BlockEvent { Type = BlockEventType.WheelsOff, TimestampUtc = t0.AddMinutes(-44) },
                new BlockEvent { Type = BlockEventType.WheelsOn, TimestampUtc = t0.AddMinutes(-1) },
            });

        var blocksOn = engine.Process(Frame(t0.AddSeconds(1), onGround: true, parkingBrake: true, gs: 0));

        Assert.Equal(FlightPhase.Arrival, blocksOn.Phase);
        Assert.NotNull(blocksOn.BlockEvent);
        Assert.Equal(BlockEventType.BlocksOn, blocksOn.BlockEvent!.Type);
        Assert.Equal(4, engine.BlockEvents.Count);
    }

    // -------------------------------------------------------------------------
    //  Helpers
    // -------------------------------------------------------------------------

    /// <summary>Advance the engine from Preflight to Climb in minimal frames.</summary>
    private static void AdvanceToClimb(FlightPhaseEngine engine, DateTimeOffset t0)
    {
        engine.Process(Frame(t0, onGround: true, parkingBrake: true));
        engine.Process(Frame(t0.AddSeconds(1), onGround: true, parkingBrake: false, gs: 2));
        engine.Process(Frame(t0.AddSeconds(30), onGround: true, ias: 50));
        engine.Process(Frame(t0.AddSeconds(31), onGround: false, ias: 80, agl: 20));
        engine.Process(Frame(t0.AddSeconds(40), onGround: false, ias: 160, agl: 500, vs: 1500));
    }

    /// <summary>Advance the engine from Preflight through Climb → Cruise → Descent → Approach.</summary>
    private static void AdvanceToApproach(FlightPhaseEngine engine, DateTimeOffset t0)
    {
        AdvanceToClimb(engine, t0);

        // Cruise: level VS for 31 s
        engine.Process(Frame(t0.AddSeconds(100), onGround: false, agl: 35000, vs: 50));
        engine.Process(Frame(t0.AddSeconds(132), onGround: false, agl: 35000, vs: 50));

        // Descent: descending VS for 31 s
        engine.Process(Frame(t0.AddSeconds(200), onGround: false, agl: 35000, vs: -500));
        engine.Process(Frame(t0.AddSeconds(232), onGround: false, agl: 35000, vs: -500));

        // Approach: below 3000 ft AGL with gear down
        engine.Process(Frame(t0.AddSeconds(290), onGround: false, agl: 2800, gearDown: true));
    }

    private static TelemetryFrame Frame(
        DateTimeOffset timestamp,
        bool onGround = false,
        bool parkingBrake = false,
        bool gearDown = false,
        double gs = 0,
        double ias = 0,
        double agl = 0,
        double vs = 0,
        double lat = 0,
        double lon = 0,
        bool engine1Running = true)   // default true: engine running during normal taxi/flight
    {
        return new TelemetryFrame
        {
            TimestampUtc = timestamp,
            Phase = FlightPhase.Preflight, // engine ignores this; it computes its own
            OnGround = onGround,
            ParkingBrakeSet = parkingBrake,
            GearDown = gearDown,
            GroundSpeedKnots = gs,
            IndicatedAirspeedKnots = ias,
            AltitudeAglFeet = agl,
            VerticalSpeedFpm = vs,
            Latitude = lat,
            Longitude = lon,
            Engine1Running = engine1Running,
        };
    }

    // -------------------------------------------------------------------------
    //  Test 6 — Pushback: no engine → stays Preflight; engine start → TaxiOut
    // -------------------------------------------------------------------------

    [Fact]
    public void Pushback_WithEnginesOff_DoesNotTransitionToTaxiOut()
    {
        var engine = new FlightPhaseEngine();
        var t0 = new DateTimeOffset(2026, 4, 12, 16, 0, 0, TimeSpan.Zero);

        // At gate, brake on, engines off
        engine.Process(Frame(t0, onGround: true, parkingBrake: true, engine1Running: false));

        // Tug pushback: brake released and aircraft moving — but no engine running.
        // Should stay Preflight; no BlocksOff should fire.
        var pushbackFrame1 = engine.Process(Frame(t0.AddSeconds(1), onGround: true, parkingBrake: false, gs: 1.5, engine1Running: false));
        Assert.Equal(FlightPhase.Preflight, pushbackFrame1.Phase);
        Assert.Null(pushbackFrame1.BlockEvent);

        var pushbackFrame2 = engine.Process(Frame(t0.AddSeconds(5), onGround: true, parkingBrake: false, gs: 2, engine1Running: false));
        Assert.Equal(FlightPhase.Preflight, pushbackFrame2.Phase);
        Assert.Empty(engine.BlockEvents);  // still no block events at all

        // Pilot starts engine and taxis under own power → TaxiOut + BlocksOff fire
        var taxiOutFrame = engine.Process(Frame(t0.AddSeconds(20), onGround: true, parkingBrake: false, gs: 3, engine1Running: true));
        Assert.Equal(FlightPhase.TaxiOut, taxiOutFrame.Phase);
        Assert.NotNull(taxiOutFrame.BlockEvent);
        Assert.Equal(BlockEventType.BlocksOff, taxiOutFrame.BlockEvent!.Type);
    }
}
