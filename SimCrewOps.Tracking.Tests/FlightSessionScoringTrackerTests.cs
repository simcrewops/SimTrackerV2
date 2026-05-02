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
        Assert.True(input.Arrival.ParkingBrakeSetBeforeAllEnginesShutdown);
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
    public void TrackerMarksArrivalViolationWhenAllEnginesShutdownBeforeParkingBrake()
    {
        var tracker = new FlightSessionScoringTracker();
        var t0 = new DateTimeOffset(2026, 4, 12, 23, 45, 0, TimeSpan.Zero);

        tracker.Ingest(Frame(t0.AddSeconds(0), FlightPhase.TaxiIn, onGround: true, taxiLights: true, landingLights: false, strobes: false, groundSpeed: 8, heading: 180));
        tracker.Ingest(Frame(t0.AddSeconds(5), FlightPhase.TaxiIn, onGround: true, taxiLights: false, landingLights: false, strobes: false, groundSpeed: 2, heading: 180, engine1: false, engine2: false));
        tracker.Ingest(Frame(t0.AddSeconds(8), FlightPhase.Arrival, onGround: true, taxiLights: false, landingLights: false, strobes: false, parkingBrake: true, engine1: false, engine2: false));

        var input = tracker.BuildScoreInput();

        Assert.True(input.Arrival.TaxiLightsOffBeforeParkingBrakeSet);
        Assert.False(input.Arrival.ParkingBrakeSetBeforeAllEnginesShutdown);
        Assert.True(input.Arrival.AllEnginesOffByEndOfSession);
    }

    [Fact]
    public void TrackerRequiresTaxiLightsOffBeforeParkingBrake_NotSameFrame()
    {
        var tracker = new FlightSessionScoringTracker();
        var t0 = new DateTimeOffset(2026, 4, 13, 0, 0, 0, TimeSpan.Zero);

        tracker.Ingest(Frame(t0.AddSeconds(0), FlightPhase.TaxiIn, onGround: true, taxiLights: true, landingLights: false, strobes: false, groundSpeed: 5, heading: 270));
        tracker.Ingest(Frame(t0.AddSeconds(5), FlightPhase.Arrival, onGround: true, taxiLights: false, landingLights: false, strobes: false, parkingBrake: true, engine1: false, engine2: false));

        var input = tracker.BuildScoreInput();

        Assert.False(input.Arrival.TaxiLightsOffBeforeParkingBrakeSet);
        Assert.True(input.Arrival.ParkingBrakeSetBeforeAllEnginesShutdown);
        Assert.True(input.Arrival.AllEnginesOffByEndOfSession);
    }

    [Fact]
    public void TouchdownRateCandidates_NullBeforeTouchdown()
    {
        var tracker = new FlightSessionScoringTracker();
        var t0 = new DateTimeOffset(2026, 4, 12, 22, 0, 0, TimeSpan.Zero);

        tracker.Ingest(Frame(t0, FlightPhase.Preflight, onGround: true));
        tracker.Ingest(Frame(t0.AddSeconds(5), FlightPhase.Climb, onGround: false));

        Assert.Null(tracker.BuildScoreInput().TouchdownRateCandidates);
    }

    [Fact]
    public void TouchdownRateCandidates_CapturedAtFirstTouchdown()
    {
        var tracker = new FlightSessionScoringTracker();
        var t0 = new DateTimeOffset(2026, 4, 12, 22, 0, 0, TimeSpan.Zero);

        // Last-airborne frame (velocityWorldY: -3.0 ft/s = 180 fpm; vs: -210 fpm)
        tracker.Ingest(Frame(t0, FlightPhase.Approach, onGround: false,
            velocityWorldY: -3.0, touchdownNormal: -2.5, vs: -210));

        // Touchdown frame — OnGround flips true; velocityWorldY: -2.0 ft/s = 120 fpm
        tracker.Ingest(Frame(t0.AddSeconds(1), FlightPhase.Landing, onGround: true,
            velocityWorldY: -2.0, touchdownNormal: -1.8, vs: -180));

        // Sustaining frame: ≥500ms elapsed — debounce commits the first touchdown.
        tracker.Ingest(Frame(t0.AddSeconds(1.6), FlightPhase.Landing, onGround: true,
            velocityWorldY: -2.0, touchdownNormal: -1.8, vs: -180));

        var input = tracker.BuildScoreInput();
        Assert.NotNull(input.TouchdownRateCandidates);

        var c = input.TouchdownRateCandidates!;
        // TD-frame candidates
        Assert.Equal(120.0, c.FpmVelocityWorldY, precision: 1);    // 2.0 * 60
        Assert.Equal(108.0, c.FpmTouchdownNormal, precision: 1);    // 1.8 * 60
        Assert.Equal(180.0, c.FpmVerticalSpeed, precision: 1);      // abs(vs=-180)
        Assert.True(c.FinalSelected > 0, "FinalSelected must be positive");

        // Last-airborne candidates from the preceding airborne frame
        Assert.Equal(180.0, c.FpmVelocityWorldYLastAirborne, precision: 1);  // 3.0 * 60
        Assert.Equal(210.0, c.FpmVerticalSpeedLastAirborne, precision: 1);   // abs(vs=-210)

        // Source label must be set
        Assert.NotEmpty(c.SelectedSourceLabel);

        // Raw TouchdownNormal fps on TD frame (negative, descending)
        Assert.Equal(-1.8, c.RawTouchdownNormalVelocityFpsTouchdownFrame, precision: 5);
    }

    [Fact]
    public void TouchdownRateCandidates_LastAirborneFromPreviousFrame()
    {
        var tracker = new FlightSessionScoringTracker();
        var t0 = new DateTimeOffset(2026, 4, 12, 22, 0, 0, TimeSpan.Zero);

        tracker.Ingest(Frame(t0, FlightPhase.Approach, onGround: false,
            velocityWorldY: -3.5, vs: -220));
        tracker.Ingest(Frame(t0.AddSeconds(1), FlightPhase.Landing, onGround: true,
            velocityWorldY: -2.0, vs: -180));
        // Sustaining frame: ≥500ms elapsed — debounce commits.
        tracker.Ingest(Frame(t0.AddSeconds(1.6), FlightPhase.Landing, onGround: true,
            velocityWorldY: -2.0, vs: -180));

        var c = tracker.BuildScoreInput().TouchdownRateCandidates!;
        Assert.Equal(210.0, c.FpmVelocityWorldYLastAirborne, precision: 1);  // 3.5 * 60
        Assert.Equal(220.0, c.FpmVerticalSpeedLastAirborne, precision: 1);   // abs(-220)
    }

    [Fact]
    public void TouchdownRateCandidates_SelectedSourceLabelPrimary()
    {
        var tracker = new FlightSessionScoringTracker();
        var t0 = new DateTimeOffset(2026, 4, 12, 22, 0, 0, TimeSpan.Zero);

        tracker.Ingest(Frame(t0, FlightPhase.Approach, onGround: false));
        // TD frame with negative VelocityWorldY — should use primary path
        tracker.Ingest(Frame(t0.AddSeconds(1), FlightPhase.Landing, onGround: true,
            velocityWorldY: -2.0, vs: -180));
        // Sustaining frame: ≥500ms elapsed — debounce commits.
        tracker.Ingest(Frame(t0.AddSeconds(1.6), FlightPhase.Landing, onGround: true,
            velocityWorldY: -2.0, vs: -180));

        var c = tracker.BuildScoreInput().TouchdownRateCandidates!;
        Assert.Equal("VelocityWorldY (TD frame)", c.SelectedSourceLabel);
    }

    [Fact]
    public void TouchdownRateCandidates_SelectedSourceLabel_FallbackA()
    {
        var tracker = new FlightSessionScoringTracker();
        var t0 = new DateTimeOffset(2026, 4, 12, 22, 0, 0, TimeSpan.Zero);

        // Last-airborne has negative VelocityWorldY; TD frame reports 0
        tracker.Ingest(Frame(t0, FlightPhase.Approach, onGround: false,
            velocityWorldY: -2.0, vs: -180));
        tracker.Ingest(Frame(t0.AddSeconds(1), FlightPhase.Landing, onGround: true,
            velocityWorldY: 0.0, vs: -150));
        // Sustaining frame: ≥500ms elapsed — debounce commits.
        tracker.Ingest(Frame(t0.AddSeconds(1.6), FlightPhase.Landing, onGround: true,
            velocityWorldY: 0.0, vs: -150));

        var c = tracker.BuildScoreInput().TouchdownRateCandidates!;
        Assert.Equal("VelocityWorldY (last airborne)", c.SelectedSourceLabel);
    }

    [Fact]
    public void TouchdownRateCandidates_PostTouchdownNormalScan_CapturesFirstNonZero()
    {
        var tracker = new FlightSessionScoringTracker();
        var t0 = new DateTimeOffset(2026, 4, 12, 22, 0, 0, TimeSpan.Zero);

        tracker.Ingest(Frame(t0, FlightPhase.Approach, onGround: false,
            velocityWorldY: -2.0, vs: -150));

        // TD frame: TouchdownNormal is 0 — will open the 2-second scan window after commit.
        tracker.Ingest(Frame(t0.AddSeconds(1), FlightPhase.Landing, onGround: true,
            velocityWorldY: -2.0, touchdownNormal: 0.0, vs: -150));

        // Sustaining frame at 500ms: ≥500ms elapsed — debounce commits and scan window opens.
        tracker.Ingest(Frame(t0.AddSeconds(1.5), FlightPhase.Landing, onGround: true,
            velocityWorldY: 0.0, touchdownNormal: 0.0, vs: -120));

        // Post-commit frame within the 2-second window: sticky SimVar now has a value.
        tracker.Ingest(Frame(t0.AddSeconds(1.6), FlightPhase.Landing, onGround: true,
            velocityWorldY: 0.0, touchdownNormal: -2.1, vs: -100));

        var c = tracker.BuildScoreInput().TouchdownRateCandidates!;
        Assert.NotNull(c.RawTouchdownNormalVelocityFpsFirstNonZero);
        Assert.Equal(-2.1, c.RawTouchdownNormalVelocityFpsFirstNonZero!.Value, precision: 5);
    }

    [Fact]
    public void TouchdownRateCandidates_PostTouchdownNormalScan_SkippedWhenTdFrameNonZero()
    {
        var tracker = new FlightSessionScoringTracker();
        var t0 = new DateTimeOffset(2026, 4, 12, 22, 0, 0, TimeSpan.Zero);

        tracker.Ingest(Frame(t0, FlightPhase.Approach, onGround: false,
            velocityWorldY: -2.0, vs: -150));

        // TD frame already has non-zero TouchdownNormal — no scan needed.
        tracker.Ingest(Frame(t0.AddSeconds(1), FlightPhase.Landing, onGround: true,
            velocityWorldY: -2.0, touchdownNormal: -1.8, vs: -150));
        // Sustaining frame: ≥500ms elapsed — debounce commits.
        tracker.Ingest(Frame(t0.AddSeconds(1.6), FlightPhase.Landing, onGround: true,
            velocityWorldY: -2.0, touchdownNormal: -1.8, vs: -150));

        var c = tracker.BuildScoreInput().TouchdownRateCandidates!;
        Assert.Equal(-1.8, c.RawTouchdownNormalVelocityFpsTouchdownFrame, precision: 5);
        Assert.Null(c.RawTouchdownNormalVelocityFpsFirstNonZero);
    }

    [Fact]
    public void TouchdownRateCandidates_RestoredFromFlightScoreInput()
    {
        var tracker = new FlightSessionScoringTracker();
        var t0 = new DateTimeOffset(2026, 4, 12, 22, 0, 0, TimeSpan.Zero);

        tracker.Ingest(Frame(t0, FlightPhase.Approach, onGround: false,
            velocityWorldY: -3.0, touchdownNormal: -2.5, vs: -200));
        tracker.Ingest(Frame(t0.AddSeconds(1), FlightPhase.Landing, onGround: true,
            velocityWorldY: -2.5, touchdownNormal: -2.0, vs: -180));
        // Sustaining frame: ≥500ms elapsed — debounce commits before BuildScoreInput.
        tracker.Ingest(Frame(t0.AddSeconds(1.6), FlightPhase.Landing, onGround: true,
            velocityWorldY: -2.5, touchdownNormal: -2.0, vs: -180));

        var saved = tracker.BuildScoreInput();
        Assert.NotNull(saved.TouchdownRateCandidates);

        // Restore into a fresh tracker
        var tracker2 = new FlightSessionScoringTracker();
        tracker2.Restore(saved, FlightPhase.Landing, lastTelemetryFrame: null, wheelsOnUtc: t0.AddSeconds(1));
        var restored = tracker2.BuildScoreInput();

        Assert.NotNull(restored.TouchdownRateCandidates);
        var s = saved.TouchdownRateCandidates!;
        var r = restored.TouchdownRateCandidates!;

        Assert.Equal(s.FpmVelocityWorldY, r.FpmVelocityWorldY);
        Assert.Equal(s.FpmTouchdownNormal, r.FpmTouchdownNormal);
        Assert.Equal(s.FpmVerticalSpeed, r.FpmVerticalSpeed);
        Assert.Equal(s.FinalSelected, r.FinalSelected);

        // New fields survive round-trip
        Assert.Equal(s.FpmVelocityWorldYLastAirborne, r.FpmVelocityWorldYLastAirborne);
        Assert.Equal(s.FpmVerticalSpeedLastAirborne, r.FpmVerticalSpeedLastAirborne);
        Assert.Equal(s.SelectedSourceLabel, r.SelectedSourceLabel);
        Assert.Equal(s.RawTouchdownNormalVelocityFpsTouchdownFrame, r.RawTouchdownNormalVelocityFpsTouchdownFrame);
        Assert.Equal(s.RawTouchdownNormalVelocityFpsFirstNonZero, r.RawTouchdownNormalVelocityFpsFirstNonZero);
    }

    // ── Touchdown debounce tests ──────────────────────────────────────────────

    [Fact]
    public void TouchdownDebounce_FlickerIgnored_SustainedGroundIsCaptured()
    {
        // Simulates the observed bug: SIM_ON_GROUND flickers true ~5s before actual touchdown
        // during the ground-effect/flare phase. The pre-flare flicker shows 450 fpm (7.5 fps);
        // the real touchdown shows 240 fpm (4.0 fps). Without debounce SimCrewOps captured the flicker.
        var tracker = new FlightSessionScoringTracker();
        var t0 = new DateTimeOffset(2026, 4, 12, 22, 24, 45, TimeSpan.Zero);

        // Airborne approach
        tracker.Ingest(Frame(t0, FlightPhase.Approach, onGround: false, velocityWorldY: -8.0, vs: -480, agl: 30));

        // Premature SIM_ON_GROUND flicker (~18 ft AGL, pre-flare) — 100ms ground contact
        tracker.Ingest(Frame(t0.AddMilliseconds(100), FlightPhase.Landing, onGround: true,
            velocityWorldY: -7.5, vs: -450, agl: 18));  // 7.5 fps * 60 = 450 fpm

        // Back airborne after 130ms total (<500ms) — flicker, tentative resets
        tracker.Ingest(Frame(t0.AddMilliseconds(230), FlightPhase.Landing, onGround: false,
            velocityWorldY: -6.5, vs: -400, agl: 20));

        tracker.Ingest(Frame(t0.AddMilliseconds(350), FlightPhase.Approach, onGround: false,
            velocityWorldY: -4.5, vs: -270, agl: 60));  // agl >50: FallbackB won't use this

        // Real touchdown after flare (~2 ft AGL, reduced sink rate): 4.0 fps * 60 = 240 fpm
        tracker.Ingest(Frame(t0.AddSeconds(5), FlightPhase.Landing, onGround: true,
            velocityWorldY: -4.0, vs: -240, agl: 2));

        // Sustaining frames — debounce window
        tracker.Ingest(Frame(t0.AddSeconds(5.3), FlightPhase.Landing, onGround: true,
            velocityWorldY: -1.0, vs: -60, agl: 0));
        // ≥500ms elapsed from real TD frame → debounce commits
        tracker.Ingest(Frame(t0.AddSeconds(5.6), FlightPhase.Landing, onGround: true,
            vs: -30, agl: 0));

        var input = tracker.BuildScoreInput();
        // Should capture real TD at 240 fpm (4.0 fps * 60), not flicker at 450 fpm (7.5 fps * 60)
        Assert.Equal(240.0, input.Landing.TouchdownVerticalSpeedFpm, precision: 1);
    }

    [Fact]
    public void TouchdownDebounce_NoFlicker_NormalLandingCaptured()
    {
        var tracker = new FlightSessionScoringTracker();
        var t0 = new DateTimeOffset(2026, 4, 12, 22, 24, 45, TimeSpan.Zero);

        tracker.Ingest(Frame(t0, FlightPhase.Approach, onGround: false,
            velocityWorldY: -5.0, vs: -300, agl: 60));  // agl >50: FallbackB won't trigger
        // Clean touchdown — no flicker. 3.0 fps * 60 = 180 fpm.
        tracker.Ingest(Frame(t0.AddMilliseconds(100), FlightPhase.Landing, onGround: true,
            velocityWorldY: -3.0, vs: -180, agl: 2));
        // ≥500ms elapsed → debounce commits
        tracker.Ingest(Frame(t0.AddMilliseconds(700), FlightPhase.Landing, onGround: true,
            velocityWorldY: -1.0, vs: -60, agl: 0));

        var input = tracker.BuildScoreInput();
        Assert.Equal(180.0, input.Landing.TouchdownVerticalSpeedFpm, precision: 1);  // 3.0 fps * 60
    }

    [Fact]
    public void TouchdownDebounce_BounceCountedAfterConfirmedTouchdown()
    {
        var tracker = new FlightSessionScoringTracker();
        var t0 = new DateTimeOffset(2026, 4, 12, 22, 24, 45, TimeSpan.Zero);

        tracker.Ingest(Frame(t0, FlightPhase.Approach, onGround: false,
            velocityWorldY: -4.0, vs: -240, agl: 60));  // agl >50

        // Real touchdown. 3.5 fps * 60 = 210 fpm.
        tracker.Ingest(Frame(t0.AddMilliseconds(100), FlightPhase.Landing, onGround: true,
            velocityWorldY: -3.5, vs: -210, agl: 2));
        // ≥500ms elapsed — debounce commits first touchdown
        tracker.Ingest(Frame(t0.AddMilliseconds(700), FlightPhase.Landing, onGround: true,
            vs: -50, agl: 0));

        // Bounce: go airborne within 3s of confirmed touchdown
        tracker.Ingest(Frame(t0.AddSeconds(1.5), FlightPhase.Landing, onGround: false,
            vs: 200, agl: 5));
        // Return to ground
        tracker.Ingest(Frame(t0.AddSeconds(2), FlightPhase.Landing, onGround: true,
            vs: -80, agl: 0));
        tracker.Ingest(Frame(t0.AddSeconds(2.6), FlightPhase.Landing, onGround: true,
            vs: -20, agl: 0));

        var input = tracker.BuildScoreInput();
        Assert.Equal(1, input.Landing.BounceCount);
        // TD rate captured from first contact (3.5 fps * 60 = 210 fpm), not from bounce
        Assert.Equal(210.0, input.Landing.TouchdownVerticalSpeedFpm, precision: 1);
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
        double velocityWorldY = 0,
        double touchdownNormal = 0)
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
            VelocityWorldYFps = velocityWorldY,
            TouchdownNormalVelocityFps = touchdownNormal,
        };
    }

    // ── Cruise altitude auto-detect tests ────────────────────────────────────

    [Fact]
    public void CruiseAutoDetect_AcceptsAfter60sStable()
    {
        var tracker = new FlightSessionScoringTracker();
        var t0 = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

        // 61 airborne frames at 1-second intervals — FL340, VS = 50 fpm (< 200 threshold)
        for (var i = 0; i <= 60; i++)
            tracker.Ingest(Frame(t0.AddSeconds(i), FlightPhase.Cruise,
                onGround: false, altitude: 34000, agl: 32000, vs: 50));

        var input = tracker.BuildScoreInput();
        Assert.NotNull(input.Cruise.CruiseTargetAltitudeFt);
        Assert.Equal(34000.0, input.Cruise.CruiseTargetAltitudeFt!.Value);
    }

    [Fact]
    public void CruiseAutoDetect_ResetsOnAltDrift()
    {
        var tracker = new FlightSessionScoringTracker();
        var t0 = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

        // 30 frames stable at FL340 — timer starts but doesn't reach 60 s
        for (var i = 0; i < 30; i++)
            tracker.Ingest(Frame(t0.AddSeconds(i), FlightPhase.Cruise,
                onGround: false, altitude: 34000, agl: 32000, vs: 50));

        // Altitude drifts > 100 ft — timer resets; only 10 frames (10 s) elapse at new alt
        for (var i = 30; i < 40; i++)
            tracker.Ingest(Frame(t0.AddSeconds(i), FlightPhase.Cruise,
                onGround: false, altitude: 34800, agl: 32800, vs: 50));

        var input = tracker.BuildScoreInput();
        Assert.Null(input.Cruise.CruiseTargetAltitudeFt);
    }

    [Fact]
    public void CruiseAutoDetect_StepClimbUpdatesTarget()
    {
        var tracker = new FlightSessionScoringTracker();
        var t0 = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

        // 65 stable frames at FL280 — sets initial target to FL280
        for (var i = 0; i < 65; i++)
            tracker.Ingest(Frame(t0.AddSeconds(i), FlightPhase.Cruise,
                onGround: false, altitude: 28000, agl: 26000, vs: 30));

        // 10 frames climbing (VS > 200) — resets the level-off timer
        for (var i = 65; i < 75; i++)
            tracker.Ingest(Frame(t0.AddSeconds(i), FlightPhase.Cruise,
                onGround: false, altitude: 28000 + (i - 65) * 600, agl: 26000 + (i - 65) * 600, vs: 1200));

        // 65 stable frames at FL340 — updates target to FL340 after 60 s
        for (var i = 75; i < 140; i++)
            tracker.Ingest(Frame(t0.AddSeconds(i), FlightPhase.Cruise,
                onGround: false, altitude: 34000, agl: 32000, vs: 30));

        var input = tracker.BuildScoreInput();
        Assert.NotNull(input.Cruise.CruiseTargetAltitudeFt);
        Assert.Equal(34000.0, input.Cruise.CruiseTargetAltitudeFt!.Value);
    }
}
