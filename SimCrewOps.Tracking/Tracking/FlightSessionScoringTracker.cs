using SimCrewOps.Scoring.Models;
using SimCrewOps.Scoring.Scoring;
using SimCrewOps.Tracking.Models;

namespace SimCrewOps.Tracking.Tracking;

public sealed class FlightSessionScoringTracker
{
    private FlightSessionProfile _profile;
    private readonly Queue<RecentSample> _recentSamples = new();

    private TelemetryFrame? _previousFrame;

    private bool _preflightBeaconSeen;

    private bool _taxiOutSeen;
    // Set when ground speed first exceeds 6 kt (forward taxi under own power).
    // Distinct from _taxiOutSeen which fires on phase transition during pushback.
    private bool _forwardTaxiStarted;
    private bool _taxiOutTaxiLightsValid = true;
    private DateTimeOffset? _taxiOutLightsOffStart;
    private double _taxiOutMaxGroundSpeed;
    private int _taxiOutTurnSpeedEvents;
    private DateTimeOffset? _lastTaxiOutTurnEventAt;

    private bool _takeoffSeen;
    private bool _takeoffLandingLightsOnBeforeTakeoff = true;
    private bool _takeoffLandingLightsOffByFl180 = true;
    private bool _takeoffStrobesOnFromTakeoffToLanding = true;
    private double _takeoffMaxBank;
    private double _takeoffMaxPitch;
    private double _takeoffMaxG;
    private int _takeoffBounceCount;
    private bool _takeoffTailStrikeDetected;
    private DateTimeOffset? _lastTakeoffLiftoffAt;

    private double _climbMaxIasBelowFl100;
    private double _climbMaxBank;
    private double _climbMaxG;

    private double _cruiseMaxAltitudeDeviation;
    private double _cruiseMaxBank;
    private double _cruiseMaxG;
    private int _cruiseSpeedInstabilityEvents;
    private DateTimeOffset? _lastCruiseSpeedInstabilityAt;
    private DateTimeOffset? _cruiseSpeedInstabilityStartedAt;
    private bool _cruiseSpeedInstabilityActive;
    private double? _cruiseReferenceIasKnots;
    private double? _cruiseReferenceMach;
    private double? _cruiseTargetAltitudeFeet;
    private double? _pendingCruiseTargetAltitudeFeet;
    private DateTimeOffset? _pendingCruiseTargetStartedAt;
    private double? _newFlightLevelCaptureSeconds;

    private bool _descentSeen;
    private double _descentMaxIasBelowFl100;
    private double _descentMaxBank;
    private double _descentMaxPitch;
    private double _descentMaxG;
    private bool _descentLandingLightsOnByFl180 = true;

    private bool _capturedApproach1000Agl;
    private bool _gearDownBy1000Agl;
    private bool _capturedApproach500Agl;
    private int _approachFlapsAt500Agl;
    private double _approachVsAt500Agl;
    private double _approachBankAt500Agl;
    private double _approachPitchAt500Agl;
    private bool _approachGearDownAt500Agl;

    private int _landingBounceCount;
    private double _landingTouchdownZoneExcessDistanceFeet;
    private double _landingTouchdownVerticalSpeedFpm;
    private double _landingTouchdownBankAngleDegrees;
    private double _landingTouchdownIndicatedAirspeedKnots;
    private double _landingTouchdownPitchAngleDegrees;
    private double _landingTouchdownGForce;
    private DateTimeOffset? _lastTouchdownAt;
    private DateTimeOffset? _airborneAfterTouchdownAt;

    private bool _taxiInSeen;
    private bool _taxiInLandingLightsOff = true;
    private bool _taxiInStrobesOff = true;
    private bool _taxiInTaxiLightsValid = true;
    private DateTimeOffset? _taxiInLightsOffStart;
    private DateTimeOffset? _taxiInLandingLightsOnStart;   // 60 s sustained on → penalty (1 min after vacate)
    private DateTimeOffset? _taxiInStrobesOnStart;          // 60 s sustained on → penalty
    private double _taxiInMaxGroundSpeed;
    private int _taxiInTurnSpeedEvents;
    private DateTimeOffset? _lastTaxiInTurnEventAt;

    private bool _arrivalSeen;
    private bool _arrivalParkingBrakeObserved;
    private bool _arrivalTaxiLightsOffBeforeParkingBrakeSet;
    private bool _arrivalParkingBrakeSetBeforeAllEnginesShutdown = true;
    private bool _arrivalAllEnginesOffByEndOfSession;

    // After a session restore (tracker restart / reconnect) the first few frames from
    // SimConnect may carry stale boolean SimVar values (all false) before MSFS has
    // fully populated the aircraft state.  We skip "negative" watchdog checks for
    // this many frames to prevent false deductions on reconnect.
    private int _postRestoreGraceFrames;

    private bool _crashDetected;
    private int _overspeedEvents;
    private int _sustainedOverspeedEvents;
    private int _stallEvents;
    private int _gpwsEvents;
    private int _engineShutdownsInFlight;
    private bool _overspeedActive;
    private bool _overspeedSustainedCounted;
    private DateTimeOffset? _overspeedStartedAt;
    private bool _stallActive;
    private bool _gpwsActive;

    public FlightSessionScoringTracker(FlightSessionProfile? profile = null)
    {
        _profile = profile ?? new FlightSessionProfile();
        // Give fresh sessions the same startup grace period as restored ones.
        // The first few SimConnect frames can carry stale boolean SimVar values
        // (all false) before MSFS has fully populated the aircraft state.
        _postRestoreGraceFrames = 10;
    }

    public void Restore(
        FlightScoreInput input,
        FlightPhase currentPhase,
        TelemetryFrame? lastTelemetryFrame,
        FlightSessionProfile? profile = null,
        DateTimeOffset? wheelsOffUtc = null,
        DateTimeOffset? wheelsOnUtc = null)
    {
        ArgumentNullException.ThrowIfNull(input);

        _profile = profile ?? new FlightSessionProfile
        {
            HeavyFourEngineAircraft = input.Climb.HeavyFourEngineAircraft,
        };

        _recentSamples.Clear();
        _previousFrame = lastTelemetryFrame;

        _preflightBeaconSeen = input.Preflight.BeaconOnBeforeTaxi;

        _taxiOutSeen = HasReachedPhase(currentPhase, FlightPhase.TaxiOut);
        _forwardTaxiStarted = _taxiOutSeen;
        _taxiOutTaxiLightsValid = !_taxiOutSeen || input.TaxiOut.TaxiLightsOn;
        _taxiOutLightsOffStart = null;
        _taxiOutMaxGroundSpeed = input.TaxiOut.MaxGroundSpeedKnots;
        _taxiOutTurnSpeedEvents = input.TaxiOut.ExcessiveTurnSpeedEvents;
        _lastTaxiOutTurnEventAt = null;

        _takeoffSeen = HasReachedPhase(currentPhase, FlightPhase.Takeoff);
        _takeoffLandingLightsOnBeforeTakeoff = !_takeoffSeen || input.Takeoff.LandingLightsOnBeforeTakeoff;
        _takeoffLandingLightsOffByFl180 = !_takeoffSeen || input.Takeoff.LandingLightsOffByFl180;
        _takeoffStrobesOnFromTakeoffToLanding = !_takeoffSeen || input.Takeoff.StrobesOnFromTakeoffToLanding;
        _takeoffMaxBank = input.Takeoff.MaxBankAngleDegrees;
        _takeoffMaxPitch = input.Takeoff.MaxPitchAngleDegrees;
        _takeoffMaxG = input.Takeoff.MaxGForce;
        _takeoffBounceCount = input.Takeoff.BounceCount;
        _takeoffTailStrikeDetected = input.Takeoff.TailStrikeDetected;
        _lastTakeoffLiftoffAt = wheelsOffUtc;

        _climbMaxIasBelowFl100 = input.Climb.MaxIasBelowFl100Knots;
        _climbMaxBank = input.Climb.MaxBankAngleDegrees;
        _climbMaxG = input.Climb.MaxGForce;

        _cruiseMaxAltitudeDeviation = input.Cruise.MaxAltitudeDeviationFeet;
        _cruiseMaxBank = input.Cruise.MaxBankAngleDegrees;
        _cruiseMaxG = input.Cruise.MaxGForce;
        _cruiseSpeedInstabilityEvents = input.Cruise.SpeedInstabilityEvents;
        _lastCruiseSpeedInstabilityAt = null;
        _cruiseSpeedInstabilityStartedAt = null;
        _cruiseSpeedInstabilityActive = false;
        _pendingCruiseTargetAltitudeFeet = null;
        _pendingCruiseTargetStartedAt = null;
        _newFlightLevelCaptureSeconds = input.Cruise.NewFlightLevelCaptureSeconds;

        if (HasReachedPhase(currentPhase, FlightPhase.Cruise) && lastTelemetryFrame is not null)
        {
            _cruiseReferenceIasKnots = lastTelemetryFrame.IndicatedAirspeedKnots;
            _cruiseReferenceMach = lastTelemetryFrame.Mach;
            _cruiseTargetAltitudeFeet = Math.Round(lastTelemetryFrame.IndicatedAltitudeFeet / 100.0) * 100.0;
        }
        else
        {
            _cruiseReferenceIasKnots = null;
            _cruiseReferenceMach = null;
            _cruiseTargetAltitudeFeet = null;
        }

        _descentSeen = HasReachedPhase(currentPhase, FlightPhase.Descent);
        _descentMaxIasBelowFl100 = input.Descent.MaxIasBelowFl100Knots;
        _descentMaxBank = input.Descent.MaxBankAngleDegrees;
        _descentMaxPitch = input.Descent.MaxPitchAngleDegrees;
        _descentMaxG = input.Descent.MaxGForce;
        _descentLandingLightsOnByFl180 = !_descentSeen || input.Descent.LandingLightsOnByFl180;

        _capturedApproach1000Agl = input.Approach.GearDownBy1000Agl;
        _gearDownBy1000Agl = input.Approach.GearDownBy1000Agl;
        _capturedApproach500Agl = input.Approach.GearDownAt500Agl || input.Approach.FlapsHandleIndexAt500Agl > 0;
        _approachFlapsAt500Agl = input.Approach.FlapsHandleIndexAt500Agl;
        _approachVsAt500Agl = input.Approach.VerticalSpeedAt500AglFpm;
        _approachBankAt500Agl = input.Approach.BankAngleAt500AglDegrees;
        _approachPitchAt500Agl = input.Approach.PitchAngleAt500AglDegrees;
        _approachGearDownAt500Agl = input.Approach.GearDownAt500Agl;

        _landingBounceCount = input.Landing.BounceCount;
        _landingTouchdownZoneExcessDistanceFeet = input.Landing.TouchdownZoneExcessDistanceFeet;
        _landingTouchdownVerticalSpeedFpm = input.Landing.TouchdownVerticalSpeedFpm;
        _landingTouchdownBankAngleDegrees = input.Landing.TouchdownBankAngleDegrees;
        _landingTouchdownIndicatedAirspeedKnots = input.Landing.TouchdownIndicatedAirspeedKnots;
        _landingTouchdownPitchAngleDegrees = input.Landing.TouchdownPitchAngleDegrees;
        _landingTouchdownGForce = input.Landing.TouchdownGForce;
        _lastTouchdownAt = wheelsOnUtc;
        _airborneAfterTouchdownAt = null;

        _taxiInSeen = HasReachedPhase(currentPhase, FlightPhase.TaxiIn);
        _taxiInLandingLightsOff = !_taxiInSeen || input.TaxiIn.LandingLightsOff;
        _taxiInStrobesOff = !_taxiInSeen || input.TaxiIn.StrobesOff;
        _taxiInTaxiLightsValid = !_taxiInSeen || input.TaxiIn.TaxiLightsOn;
        _taxiInLightsOffStart = null;
        _taxiInLandingLightsOnStart = null;
        _taxiInStrobesOnStart = null;
        _taxiInMaxGroundSpeed = input.TaxiIn.MaxGroundSpeedKnots;
        _taxiInTurnSpeedEvents = input.TaxiIn.ExcessiveTurnSpeedEvents;
        _lastTaxiInTurnEventAt = null;

        _arrivalSeen = currentPhase == FlightPhase.Arrival;
        _arrivalParkingBrakeObserved = currentPhase == FlightPhase.Arrival;
        _arrivalTaxiLightsOffBeforeParkingBrakeSet = input.Arrival.TaxiLightsOffBeforeParkingBrakeSet;
        _arrivalParkingBrakeSetBeforeAllEnginesShutdown =
            !_arrivalSeen || input.Arrival.ParkingBrakeSetBeforeAllEnginesShutdown;
        _arrivalAllEnginesOffByEndOfSession = input.Arrival.AllEnginesOffByEndOfSession;

        // Give the first several frames after reconnect a chance to reflect the real
        // aircraft state before any "lights off" watchdog checks can trigger.
        _postRestoreGraceFrames = 10;

        _crashDetected = input.Safety.CrashDetected;
        _overspeedEvents = input.Safety.OverspeedEvents;
        _sustainedOverspeedEvents = input.Safety.SustainedOverspeedEvents;
        _stallEvents = input.Safety.StallEvents;
        _gpwsEvents = input.Safety.GpwsEvents;
        _engineShutdownsInFlight = input.Safety.EngineShutdownsInFlight;
        _overspeedActive = false;
        _overspeedSustainedCounted = false;
        _overspeedStartedAt = null;
        _stallActive = false;
        _gpwsActive = false;
    }

    public void Ingest(TelemetryFrame frame)
    {
        if (_postRestoreGraceFrames > 0) _postRestoreGraceFrames--;

        AddRecentSample(frame);
        UpdatePreflight(frame);
        UpdateTaxiOut(frame);
        UpdateTakeoff(frame);
        UpdateClimb(frame);
        UpdateCruise(frame);
        UpdateDescent(frame);
        UpdateApproach(frame);
        UpdateLanding(frame);
        UpdateTaxiIn(frame);
        UpdateArrivalLifecycle(frame);
        UpdateArrival(frame);
        UpdateSafety(frame);

        _previousFrame = frame;
    }

    public FlightScoreInput BuildScoreInput()
    {
        return new FlightScoreInput
        {
            Preflight = new PreflightMetrics
            {
                // Pass until forward taxi begins — only penalise once the window has closed.
                BeaconOnBeforeTaxi = !_forwardTaxiStarted || _preflightBeaconSeen,
            },
            TaxiOut = new TaxiMetrics
            {
                MaxGroundSpeedKnots = _taxiOutMaxGroundSpeed,
                ExcessiveTurnSpeedEvents = _taxiOutTurnSpeedEvents,
                // Pass if the phase hasn't been seen yet — only penalise once we enter it.
                TaxiLightsOn = !_taxiOutSeen || _taxiOutTaxiLightsValid,
            },
            Takeoff = new TakeoffMetrics
            {
                BounceCount = _takeoffBounceCount,
                TailStrikeDetected = _takeoffTailStrikeDetected,
                MaxBankAngleDegrees = _takeoffMaxBank,
                MaxPitchAngleDegrees = _takeoffMaxPitch,
                MaxGForce = _takeoffMaxG,
                // Pass if the phase hasn't been seen yet — only penalise once we enter it.
                LandingLightsOnBeforeTakeoff = !_takeoffSeen || _takeoffLandingLightsOnBeforeTakeoff,
                LandingLightsOffByFl180 = !_takeoffSeen || _takeoffLandingLightsOffByFl180,
                StrobesOnFromTakeoffToLanding = !_takeoffSeen || _takeoffStrobesOnFromTakeoffToLanding,
            },
            Climb = new ClimbMetrics
            {
                HeavyFourEngineAircraft = _profile.HeavyFourEngineAircraft,
                MaxIasBelowFl100Knots = _climbMaxIasBelowFl100,
                MaxBankAngleDegrees = _climbMaxBank,
                MaxGForce = _climbMaxG,
            },
            Cruise = new CruiseMetrics
            {
                MaxAltitudeDeviationFeet = _cruiseMaxAltitudeDeviation,
                NewFlightLevelCaptureSeconds = _newFlightLevelCaptureSeconds,
                SpeedInstabilityEvents = _cruiseSpeedInstabilityEvents,
                MaxBankAngleDegrees = _cruiseMaxBank,
                MaxGForce = _cruiseMaxG,
            },
            Descent = new DescentMetrics
            {
                MaxIasBelowFl100Knots = _descentMaxIasBelowFl100,
                MaxBankAngleDegrees = _descentMaxBank,
                MaxPitchAngleDegrees = _descentMaxPitch,
                MaxGForce = _descentMaxG,
                // Pass if descent hasn't been seen yet — only penalise once we enter it.
                LandingLightsOnByFl180 = !_descentSeen || _descentLandingLightsOnByFl180,
            },
            Approach = new ApproachMetrics
            {
                // Pass gear/flaps checks if the capture snapshot hasn't fired yet —
                // only penalise once the aircraft actually passes through those altitudes.
                GearDownBy1000Agl = !_capturedApproach1000Agl || _gearDownBy1000Agl,
                // Default flaps to 2 (passing) until the 500ft snapshot is captured.
                FlapsHandleIndexAt500Agl = _capturedApproach500Agl ? _approachFlapsAt500Agl : 2,
                VerticalSpeedAt500AglFpm = _approachVsAt500Agl,
                BankAngleAt500AglDegrees = _approachBankAt500Agl,
                PitchAngleAt500AglDegrees = _approachPitchAt500Agl,
                GearDownAt500Agl = !_capturedApproach500Agl || _approachGearDownAt500Agl,
            },
            Landing = new LandingMetrics
            {
                TouchdownZoneExcessDistanceFeet = _landingTouchdownZoneExcessDistanceFeet,
                TouchdownVerticalSpeedFpm = _landingTouchdownVerticalSpeedFpm,
                TouchdownBankAngleDegrees = _landingTouchdownBankAngleDegrees,
                TouchdownIndicatedAirspeedKnots = _landingTouchdownIndicatedAirspeedKnots,
                TouchdownPitchAngleDegrees = _landingTouchdownPitchAngleDegrees,
                TouchdownGForce = _landingTouchdownGForce,
                BounceCount = _landingBounceCount,
            },
            TaxiIn = new TaxiInMetrics
            {
                // Pass if the phase hasn't been seen yet — only penalise once we enter it.
                LandingLightsOff = !_taxiInSeen || _taxiInLandingLightsOff,
                StrobesOff = !_taxiInSeen || _taxiInStrobesOff,
                MaxGroundSpeedKnots = _taxiInMaxGroundSpeed,
                ExcessiveTurnSpeedEvents = _taxiInTurnSpeedEvents,
                TaxiLightsOn = !_taxiInSeen || _taxiInTaxiLightsValid,
            },
            Arrival = new ArrivalMetrics
            {
                // Pass if the phase hasn't been seen yet — only penalise once we enter it.
                TaxiLightsOffBeforeParkingBrakeSet = !_arrivalSeen || (_arrivalParkingBrakeObserved && _arrivalTaxiLightsOffBeforeParkingBrakeSet),
                ParkingBrakeSetBeforeAllEnginesShutdown = !_arrivalSeen || (_arrivalParkingBrakeObserved && _arrivalParkingBrakeSetBeforeAllEnginesShutdown),
                AllEnginesOffByEndOfSession = !_arrivalSeen || _arrivalAllEnginesOffByEndOfSession,
            },
            Safety = new SafetyMetrics
            {
                CrashDetected = _crashDetected,
                OverspeedEvents = _overspeedEvents,
                SustainedOverspeedEvents = _sustainedOverspeedEvents,
                StallEvents = _stallEvents,
                GpwsEvents = _gpwsEvents,
                EngineShutdownsInFlight = _engineShutdownsInFlight,
            },
        };
    }

    public ScoreResult CalculateScore(ScoringEngine? engine = null, ScoringWeights? weights = null)
    {
        engine ??= new ScoringEngine();
        return engine.Calculate(BuildScoreInput(), weights);
    }

    private void UpdatePreflight(TelemetryFrame frame)
    {
        // Beacon must be on before forward taxi starts (GS ≥ 6 kt, own-power movement).
        // Pushback counts as preflight — the beacon should already be on during pushback.
        // Keep the window open while within the startup grace period to avoid
        // stale SimVar false-values permanently closing the window on first-frame reconnect.
        if ((!_forwardTaxiStarted || _postRestoreGraceFrames > 0) && frame.BeaconLightOn)
        {
            _preflightBeaconSeen = true;
        }
    }

    private void UpdateTaxiOut(TelemetryFrame frame)
    {
        if (frame.Phase != FlightPhase.TaxiOut)
        {
            return;
        }

        _taxiOutSeen = true;
        _taxiOutMaxGroundSpeed = Math.Max(_taxiOutMaxGroundSpeed, frame.GroundSpeedKnots);

        // Forward taxi = aircraft moving under its own power at ≥ 6 kt.
        // Pushback by tug is typically 1–3 kt; 6 kt is safely above any realistic pushback speed.
        // The beacon window closes at this point (pilot should have had beacon on since pushback).
        if (!_forwardTaxiStarted && frame.GroundSpeedKnots >= 6.0)
        {
            _forwardTaxiStarted = true;
        }

        // Enforce taxi lights throughout the taxi roll (once forward taxi is underway).
        // Require 3 consecutive seconds of lights-off before recording a deduction so that
        // an accidental switch toggle doesn't cause a permanent penalty.
        if (_forwardTaxiStarted && _postRestoreGraceFrames <= 0)
        {
            if (!frame.TaxiLightsOn)
            {
                _taxiOutLightsOffStart ??= frame.TimestampUtc;
                if (frame.TimestampUtc - _taxiOutLightsOffStart.Value >= TimeSpan.FromSeconds(3))
                    _taxiOutTaxiLightsValid = false;
            }
            else
            {
                _taxiOutLightsOffStart = null;
            }
        }

        CountTurnSpeedEvent(frame, ref _taxiOutTurnSpeedEvents, ref _lastTaxiOutTurnEventAt);
    }

    private void UpdateTakeoff(TelemetryFrame frame)
    {
        if (frame.Phase == FlightPhase.Takeoff)
        {
            _takeoffSeen = true;
            _takeoffMaxBank = Math.Max(_takeoffMaxBank, Math.Abs(frame.BankAngleDegrees));
            _takeoffMaxPitch = Math.Max(_takeoffMaxPitch, Math.Abs(frame.PitchAngleDegrees));
            _takeoffMaxG = Math.Max(_takeoffMaxG, frame.GForce);

            if (_postRestoreGraceFrames <= 0 && !frame.LandingLightsOn)
            {
                _takeoffLandingLightsOnBeforeTakeoff = false;
            }

            if (frame.OnGround && frame.IndicatedAirspeedKnots >= 40 && frame.PitchAngleDegrees >= 10)
            {
                _takeoffTailStrikeDetected = true;
            }
        }

        if (frame.Phase is FlightPhase.Takeoff or FlightPhase.Climb or FlightPhase.Cruise or FlightPhase.Descent or FlightPhase.Approach or FlightPhase.Landing)
        {
            if (frame.IndicatedAltitudeFeet >= 18000 && frame.LandingLightsOn)
            {
                _takeoffLandingLightsOffByFl180 = false;
            }

            if (_postRestoreGraceFrames <= 0 && !frame.OnGround && !frame.StrobesOn)
            {
                _takeoffStrobesOnFromTakeoffToLanding = false;
            }
        }

        if (_previousFrame is null)
        {
            return;
        }

        if (_previousFrame.Phase == FlightPhase.Takeoff && _previousFrame.OnGround && !frame.OnGround)
        {
            _lastTakeoffLiftoffAt = frame.TimestampUtc;
        }

        if (_previousFrame.Phase == FlightPhase.Takeoff &&
            !_previousFrame.OnGround &&
            frame.OnGround &&
            _lastTakeoffLiftoffAt is not null &&
            frame.TimestampUtc - _lastTakeoffLiftoffAt <= TimeSpan.FromSeconds(5) &&
            frame.AltitudeAglFeet < 100)
        {
            _takeoffBounceCount++;
        }
    }

    private void UpdateClimb(TelemetryFrame frame)
    {
        if (frame.Phase != FlightPhase.Climb)
        {
            return;
        }

        if (frame.IndicatedAltitudeFeet < 10000)
        {
            _climbMaxIasBelowFl100 = Math.Max(_climbMaxIasBelowFl100, frame.IndicatedAirspeedKnots);
        }

        _climbMaxBank = Math.Max(_climbMaxBank, Math.Abs(frame.BankAngleDegrees));
        _climbMaxG = Math.Max(_climbMaxG, frame.GForce);
    }

    private void UpdateCruise(TelemetryFrame frame)
    {
        if (frame.Phase != FlightPhase.Cruise)
        {
            return;
        }

        _cruiseMaxBank = Math.Max(_cruiseMaxBank, Math.Abs(frame.BankAngleDegrees));
        _cruiseMaxG = Math.Max(_cruiseMaxG, frame.GForce);

        var currentAltitude = Math.Round(frame.IndicatedAltitudeFeet / 100.0) * 100.0;
        _cruiseTargetAltitudeFeet ??= currentAltitude;

        // Only accumulate altitude deviation while the aircraft is in level flight.
        // During a step climb (or descent to a lower cruise level) the VS will be well
        // above 300 fpm; counting that deviation would penalise an intentional level
        // change.  The target-altitude adoption logic below handles updating the
        // reference once the new level is captured.
        if (Math.Abs(frame.VerticalSpeedFpm) < 300)
        {
            _cruiseMaxAltitudeDeviation = Math.Max(_cruiseMaxAltitudeDeviation, Math.Abs(frame.IndicatedAltitudeFeet - _cruiseTargetAltitudeFeet.Value));
        }

        if (Math.Abs(frame.IndicatedAltitudeFeet - _cruiseTargetAltitudeFeet.Value) > 300)
        {
            if (_pendingCruiseTargetAltitudeFeet is null ||
                Math.Abs(frame.IndicatedAltitudeFeet - _pendingCruiseTargetAltitudeFeet.Value) > 100)
            {
                _pendingCruiseTargetAltitudeFeet = currentAltitude;
                _pendingCruiseTargetStartedAt = frame.TimestampUtc;
            }
            else if (_pendingCruiseTargetStartedAt is not null &&
                     frame.TimestampUtc - _pendingCruiseTargetStartedAt >= TimeSpan.FromSeconds(60))
            {
                _newFlightLevelCaptureSeconds = (_newFlightLevelCaptureSeconds is null)
                    ? (frame.TimestampUtc - _pendingCruiseTargetStartedAt.Value).TotalSeconds
                    : Math.Max(_newFlightLevelCaptureSeconds.Value, (frame.TimestampUtc - _pendingCruiseTargetStartedAt.Value).TotalSeconds);

                _cruiseTargetAltitudeFeet = _pendingCruiseTargetAltitudeFeet;
                _pendingCruiseTargetAltitudeFeet = null;
                _pendingCruiseTargetStartedAt = null;
            }
        }
        else
        {
            _pendingCruiseTargetAltitudeFeet = null;
            _pendingCruiseTargetStartedAt = null;
        }

        if (_previousFrame is null || _previousFrame.Phase != FlightPhase.Cruise)
        {
            _cruiseReferenceIasKnots = frame.IndicatedAirspeedKnots;
            _cruiseReferenceMach = frame.Mach;
            _cruiseSpeedInstabilityActive = false;
            _cruiseSpeedInstabilityStartedAt = null;
            return;
        }

        _cruiseReferenceIasKnots ??= _previousFrame.IndicatedAirspeedKnots;
        _cruiseReferenceMach ??= _previousFrame.Mach;

        var machDelta = Math.Abs(frame.Mach - _cruiseReferenceMach.Value);
        var iasDelta = Math.Abs(frame.IndicatedAirspeedKnots - _cruiseReferenceIasKnots.Value);
        var unstable = machDelta > 0.03 || iasDelta > 15;

        if (unstable)
        {
            if (!_cruiseSpeedInstabilityActive)
            {
                _cruiseSpeedInstabilityActive = true;
                _cruiseSpeedInstabilityStartedAt = frame.TimestampUtc;
            }
            else if (_cruiseSpeedInstabilityStartedAt is not null &&
                     frame.TimestampUtc - _cruiseSpeedInstabilityStartedAt >= TimeSpan.FromSeconds(5) &&
                     (_lastCruiseSpeedInstabilityAt is null || frame.TimestampUtc - _lastCruiseSpeedInstabilityAt >= TimeSpan.FromSeconds(10)))
            {
                _cruiseSpeedInstabilityEvents++;
                _lastCruiseSpeedInstabilityAt = frame.TimestampUtc;
            }
        }
        else
        {
            _cruiseReferenceIasKnots = frame.IndicatedAirspeedKnots;
            _cruiseReferenceMach = frame.Mach;
            _cruiseSpeedInstabilityActive = false;
            _cruiseSpeedInstabilityStartedAt = null;
        }
    }

    private void UpdateDescent(TelemetryFrame frame)
    {
        if (frame.Phase != FlightPhase.Descent)
        {
            return;
        }

        _descentSeen = true;

        if (frame.IndicatedAltitudeFeet < 10000)
        {
            _descentMaxIasBelowFl100 = Math.Max(_descentMaxIasBelowFl100, frame.IndicatedAirspeedKnots);
        }

        if (_postRestoreGraceFrames <= 0 && frame.IndicatedAltitudeFeet <= 18000 && !frame.LandingLightsOn)
        {
            _descentLandingLightsOnByFl180 = false;
        }

        _descentMaxBank = Math.Max(_descentMaxBank, Math.Abs(frame.BankAngleDegrees));
        _descentMaxPitch = Math.Max(_descentMaxPitch, Math.Abs(frame.PitchAngleDegrees));
        _descentMaxG = Math.Max(_descentMaxG, frame.GForce);
    }

    private void UpdateApproach(TelemetryFrame frame)
    {
        if (frame.Phase != FlightPhase.Approach)
        {
            return;
        }

        if (_previousFrame is not null)
        {
            if (!_capturedApproach1000Agl &&
                _previousFrame.AltitudeAglFeet > 1000 &&
                frame.AltitudeAglFeet <= 1000)
            {
                _capturedApproach1000Agl = true;
                _gearDownBy1000Agl = frame.GearDown;
            }

            if (!_capturedApproach500Agl &&
                _previousFrame.AltitudeAglFeet > 500 &&
                frame.AltitudeAglFeet <= 500)
            {
                _capturedApproach500Agl = true;
                _approachFlapsAt500Agl = frame.FlapsHandleIndex;
                _approachVsAt500Agl = Math.Abs(frame.VerticalSpeedFpm);
                _approachBankAt500Agl = Math.Abs(frame.BankAngleDegrees);
                _approachPitchAt500Agl = Math.Abs(frame.PitchAngleDegrees);
                _approachGearDownAt500Agl = frame.GearDown;
            }
        }
    }

    private void UpdateLanding(TelemetryFrame frame)
    {
        if (_previousFrame is null)
        {
            if (frame.TouchdownZoneExcessDistanceFeet is not null)
            {
                _landingTouchdownZoneExcessDistanceFeet = frame.TouchdownZoneExcessDistanceFeet.Value;
            }

            return;
        }

        if (!_previousFrame.OnGround && frame.OnGround)
        {
            _lastTouchdownAt = frame.TimestampUtc;
            _landingTouchdownVerticalSpeedFpm = Math.Max(_landingTouchdownVerticalSpeedFpm, CalculateTouchdownVerticalSpeed(frame));
            _landingTouchdownGForce = Math.Max(_landingTouchdownGForce, CalculateTouchdownGForce());
            if (_landingTouchdownIndicatedAirspeedKnots == 0)
            {
                _landingTouchdownBankAngleDegrees = Math.Abs(frame.BankAngleDegrees);
                _landingTouchdownIndicatedAirspeedKnots = frame.IndicatedAirspeedKnots;
                _landingTouchdownPitchAngleDegrees = frame.PitchAngleDegrees;
            }

            if (_airborneAfterTouchdownAt is not null &&
                frame.TimestampUtc - _airborneAfterTouchdownAt <= TimeSpan.FromSeconds(3))
            {
                _landingBounceCount++;
            }

            if (frame.TouchdownZoneExcessDistanceFeet is not null)
            {
                _landingTouchdownZoneExcessDistanceFeet = frame.TouchdownZoneExcessDistanceFeet.Value;
            }
        }

        if (_previousFrame.OnGround &&
            !frame.OnGround &&
            _lastTouchdownAt is not null &&
            frame.TimestampUtc - _lastTouchdownAt <= TimeSpan.FromSeconds(3))
        {
            _airborneAfterTouchdownAt = frame.TimestampUtc;
        }

        if (frame.TouchdownZoneExcessDistanceFeet is not null)
        {
            _landingTouchdownZoneExcessDistanceFeet = Math.Max(_landingTouchdownZoneExcessDistanceFeet, frame.TouchdownZoneExcessDistanceFeet.Value);
        }
    }

    private void UpdateTaxiIn(TelemetryFrame frame)
    {
        if (frame.Phase != FlightPhase.TaxiIn)
        {
            return;
        }

        _taxiInSeen = true;
        _taxiInMaxGroundSpeed = Math.Max(_taxiInMaxGroundSpeed, frame.GroundSpeedKnots);

        // Landing lights — 20-second sustained-on required before penalty locks in.
        // Gives the pilot time to complete the after-landing checklist after runway vacate.
        if (_postRestoreGraceFrames <= 0)
        {
            if (frame.LandingLightsOn)
            {
                _taxiInLandingLightsOnStart ??= frame.TimestampUtc;
                if (frame.TimestampUtc - _taxiInLandingLightsOnStart.Value >= TimeSpan.FromSeconds(60))
                    _taxiInLandingLightsOff = false;
            }
            else
            {
                _taxiInLandingLightsOnStart = null;
            }
        }

        // Strobes — same 20-second window.
        if (_postRestoreGraceFrames <= 0)
        {
            if (frame.StrobesOn)
            {
                _taxiInStrobesOnStart ??= frame.TimestampUtc;
                if (frame.TimestampUtc - _taxiInStrobesOnStart.Value >= TimeSpan.FromSeconds(60))
                    _taxiInStrobesOff = false;
            }
            else
            {
                _taxiInStrobesOnStart = null;
            }
        }

        // 3-second debounce: an accidental light toggle doesn't cause a permanent penalty.
        if (_postRestoreGraceFrames <= 0)
        {
            if (!frame.TaxiLightsOn)
            {
                _taxiInLightsOffStart ??= frame.TimestampUtc;
                if (frame.TimestampUtc - _taxiInLightsOffStart.Value >= TimeSpan.FromSeconds(3))
                    _taxiInTaxiLightsValid = false;
            }
            else
            {
                _taxiInLightsOffStart = null;
            }
        }

        CountTurnSpeedEvent(frame, ref _taxiInTurnSpeedEvents, ref _lastTaxiInTurnEventAt);
    }

    private void UpdateArrival(TelemetryFrame frame)
    {
        if (frame.Phase == FlightPhase.Arrival)
        {
            _arrivalSeen = true;
        }
    }

    private void UpdateArrivalLifecycle(TelemetryFrame frame)
    {
        if (frame.Phase is not FlightPhase.TaxiIn and not FlightPhase.Arrival)
        {
            return;
        }

        if (frame.ParkingBrakeSet)
        {
            _arrivalSeen = true;
        }

        if (!_arrivalParkingBrakeObserved)
        {
            if (!frame.ParkingBrakeSet)
            {
                // Track whether taxi lights are off on the approach to the gate.
                // Lights should be extinguished while pulling up, before the brake is set.
                _arrivalTaxiLightsOffBeforeParkingBrakeSet = !frame.TaxiLightsOn;

                if (!AnyEngineRunning(frame))
                {
                    _arrivalParkingBrakeSetBeforeAllEnginesShutdown = false;
                }
            }
            else
            {
                _arrivalParkingBrakeObserved = true;
            }
        }

        _arrivalAllEnginesOffByEndOfSession = !AnyEngineRunning(frame);
    }

    private void UpdateSafety(TelemetryFrame frame)
    {
        _crashDetected |= frame.CrashDetected;

        if (frame.OverspeedWarning)
        {
            if (!_overspeedActive)
            {
                _overspeedEvents++;
                _overspeedActive = true;
                _overspeedStartedAt = frame.TimestampUtc;
                _overspeedSustainedCounted = false;
            }
            else if (!_overspeedSustainedCounted &&
                     _overspeedStartedAt is not null &&
                     frame.TimestampUtc - _overspeedStartedAt >= TimeSpan.FromSeconds(30))
            {
                _sustainedOverspeedEvents++;
                _overspeedSustainedCounted = true;
            }
        }
        else
        {
            _overspeedActive = false;
            _overspeedStartedAt = null;
            _overspeedSustainedCounted = false;
        }

        if (frame.StallWarning && !_stallActive)
        {
            _stallEvents++;
        }

        if (frame.GpwsAlert && !_gpwsActive)
        {
            _gpwsEvents++;
        }

        _stallActive = frame.StallWarning;
        _gpwsActive = frame.GpwsAlert;

        if (_previousFrame is not null && !frame.OnGround && IsAirbornePhase(frame.Phase))
        {
            CountEngineShutdown(_previousFrame.Engine1Running, frame.Engine1Running);
            CountEngineShutdown(_previousFrame.Engine2Running, frame.Engine2Running);
            CountEngineShutdown(_previousFrame.Engine3Running, frame.Engine3Running);
            CountEngineShutdown(_previousFrame.Engine4Running, frame.Engine4Running);
        }
    }

    private void CountEngineShutdown(bool previousRunning, bool currentRunning)
    {
        if (previousRunning && !currentRunning)
        {
            _engineShutdownsInFlight++;
        }
    }

    private static bool AnyEngineRunning(TelemetryFrame frame) =>
        frame.Engine1Running ||
        frame.Engine2Running ||
        frame.Engine3Running ||
        frame.Engine4Running;

    private void AddRecentSample(TelemetryFrame frame)
    {
        _recentSamples.Enqueue(new RecentSample(frame.TimestampUtc, frame.VerticalSpeedFpm, frame.GForce, frame.AltitudeAglFeet));

        while (_recentSamples.Count > 0 && frame.TimestampUtc - _recentSamples.Peek().TimestampUtc > TimeSpan.FromSeconds(2))
        {
            _recentSamples.Dequeue();
        }
    }

    private double CalculateTouchdownVerticalSpeed(TelemetryFrame touchdownFrame)
    {
        // Primary: barometric VS on the last airborne frame, sampled just before wheel contact.
        //
        // The VERTICAL SPEED SimVar reports the instantaneous rate of altitude change.  On the
        // first on-ground frame the gear physically compresses and the SimVar spikes (sometimes
        // 2–3× the actual sink rate).  The frame immediately before OnGround flips is free of
        // this artefact and gives the true approach/flare sink rate.
        //
        // The adjacent-frame AGL-delta method (previous primary) amplifies badly at high frame
        // rates: a 2 ft AGL drop in 0.125 s (8 Hz) = 960 fpm even for a smooth landing, which
        // consistently reads heavier than the cockpit indication or other trackers.
        if (_previousFrame is not null
            && !_previousFrame.OnGround
            && _previousFrame.AltitudeAglFeet <= 50   // must be in the flare/approach, not cruise
            && _previousFrame.VerticalSpeedFpm < 0)   // must be descending
        {
            return Math.Abs(_previousFrame.VerticalSpeedFpm);
        }

        // Fallback A: barometric VS on the touchdown frame itself.
        // Used when the previous frame is missing or the AGL guard above isn't met.
        if (touchdownFrame.VerticalSpeedFpm < 0)
            return Math.Abs(touchdownFrame.VerticalSpeedFpm);

        // Fallback B: AGL rate-of-change across the full flare window in the 2-second rolling
        // buffer.  Using a wider time span (first → last sample in 0.5–30 ft range) smooths
        // out per-frame noise and gives a stable average sink rate.
        var flareFrames = _recentSamples
            .Where(static s => s.AltitudeAglFeet >= 0.5 && s.AltitudeAglFeet <= 30)
            .OrderBy(static s => s.TimestampUtc)
            .ToList();

        if (flareFrames.Count >= 2)
        {
            var first = flareFrames[0];
            var last  = flareFrames[^1];
            var dtSeconds = (last.TimestampUtc - first.TimestampUtc).TotalSeconds;
            if (dtSeconds >= 0.1)
            {
                var aglSinkFpm = (first.AltitudeAglFeet - last.AltitudeAglFeet) / dtSeconds * 60.0;
                if (aglSinkFpm > 0) return aglSinkFpm;
            }
        }

        // Last resort: lowest barometric VS from sub-100-ft samples.
        var subHundredSamples = _recentSamples
            .Where(static s => s.AltitudeAglFeet is > 0 and <= 100)
            .ToList();

        return subHundredSamples.Count > 0
            ? Math.Abs(subHundredSamples.Min(static s => s.VerticalSpeedFpm))
            : 0;
    }

    private double CalculateTouchdownGForce()
    {
        var touchdownSamples = _recentSamples
            .Where(sample => sample.AltitudeAglFeet <= 100)
            .ToList();

        if (touchdownSamples.Count == 0)
        {
            return 0;
        }

        return touchdownSamples.Max(sample => sample.GForce);
    }

    private void CountTurnSpeedEvent(
        TelemetryFrame frame,
        ref int eventCount,
        ref DateTimeOffset? lastEventAt)
    {
        if (_previousFrame is null || _previousFrame.Phase != frame.Phase)
        {
            return;
        }

        var headingDelta = NormalizeHeadingDelta(frame.HeadingTrueDegrees - _previousFrame.HeadingTrueDegrees);
        if (Math.Abs(headingDelta) < 45 || frame.GroundSpeedKnots <= 15)
        {
            return;
        }

        if (lastEventAt is not null && frame.TimestampUtc - lastEventAt < TimeSpan.FromSeconds(3))
        {
            return;
        }

        eventCount++;
        lastEventAt = frame.TimestampUtc;
    }

    private static bool IsAirbornePhase(FlightPhase phase) =>
        phase is FlightPhase.Takeoff or FlightPhase.Climb or FlightPhase.Cruise or FlightPhase.Descent or FlightPhase.Approach or FlightPhase.Landing;

    private static bool HasReachedPhase(FlightPhase currentPhase, FlightPhase requiredPhase) =>
        currentPhase >= requiredPhase;

    private static double NormalizeHeadingDelta(double degrees)
    {
        var normalized = degrees % 360;
        if (normalized > 180)
        {
            normalized -= 360;
        }
        else if (normalized < -180)
        {
            normalized += 360;
        }

        return normalized;
    }

    private sealed record RecentSample(DateTimeOffset TimestampUtc, double VerticalSpeedFpm, double GForce, double AltitudeAglFeet);
}
