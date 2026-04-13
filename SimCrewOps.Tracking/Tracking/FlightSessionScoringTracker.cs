using SimCrewOps.Scoring.Models;
using SimCrewOps.Scoring.Scoring;
using SimCrewOps.Tracking.Models;

namespace SimCrewOps.Tracking.Tracking;

public sealed class FlightSessionScoringTracker
{
    private readonly FlightSessionProfile _profile;
    private readonly Queue<RecentSample> _recentSamples = new();

    private TelemetryFrame? _previousFrame;

    private bool _preflightBeaconSeen;

    private bool _taxiOutSeen;
    private bool _taxiOutTaxiLightsValid = true;
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
    private double _landingTouchdownGForce;
    private DateTimeOffset? _lastTouchdownAt;
    private DateTimeOffset? _airborneAfterTouchdownAt;

    private bool _taxiInSeen;
    private bool _taxiInLandingLightsOff = true;
    private bool _taxiInStrobesOff = true;
    private bool _taxiInTaxiLightsValid = true;
    private double _taxiInMaxGroundSpeed;
    private int _taxiInTurnSpeedEvents;
    private DateTimeOffset? _lastTaxiInTurnEventAt;

    private bool _arrivalSeen;
    private bool _arrivalParkingBrakeObserved;
    private bool _arrivalTaxiLightsOffBeforeParkingBrakeSet;
    private bool _arrivalParkingBrakeSetBeforeAllEnginesShutdown = true;
    private bool _arrivalAllEnginesOffByEndOfSession;

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
    }

    public void Ingest(TelemetryFrame frame)
    {
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
                BeaconOnBeforeTaxi = _preflightBeaconSeen,
            },
            TaxiOut = new TaxiMetrics
            {
                MaxGroundSpeedKnots = _taxiOutMaxGroundSpeed,
                ExcessiveTurnSpeedEvents = _taxiOutTurnSpeedEvents,
                TaxiLightsOn = _taxiOutSeen && _taxiOutTaxiLightsValid,
            },
            Takeoff = new TakeoffMetrics
            {
                BounceCount = _takeoffBounceCount,
                TailStrikeDetected = _takeoffTailStrikeDetected,
                MaxBankAngleDegrees = _takeoffMaxBank,
                MaxPitchAngleDegrees = _takeoffMaxPitch,
                MaxGForce = _takeoffMaxG,
                LandingLightsOnBeforeTakeoff = _takeoffSeen && _takeoffLandingLightsOnBeforeTakeoff,
                LandingLightsOffByFl180 = _takeoffSeen && _takeoffLandingLightsOffByFl180,
                StrobesOnFromTakeoffToLanding = _takeoffSeen && _takeoffStrobesOnFromTakeoffToLanding,
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
                LandingLightsOnByFl180 = _descentSeen && _descentLandingLightsOnByFl180,
            },
            Approach = new ApproachMetrics
            {
                GearDownBy1000Agl = _capturedApproach1000Agl && _gearDownBy1000Agl,
                FlapsHandleIndexAt500Agl = _approachFlapsAt500Agl,
                VerticalSpeedAt500AglFpm = _approachVsAt500Agl,
                BankAngleAt500AglDegrees = _approachBankAt500Agl,
                PitchAngleAt500AglDegrees = _approachPitchAt500Agl,
                GearDownAt500Agl = _capturedApproach500Agl && _approachGearDownAt500Agl,
            },
            Landing = new LandingMetrics
            {
                TouchdownZoneExcessDistanceFeet = _landingTouchdownZoneExcessDistanceFeet,
                TouchdownVerticalSpeedFpm = _landingTouchdownVerticalSpeedFpm,
                TouchdownGForce = _landingTouchdownGForce,
                BounceCount = _landingBounceCount,
            },
            TaxiIn = new TaxiInMetrics
            {
                LandingLightsOff = _taxiInSeen && _taxiInLandingLightsOff,
                StrobesOff = _taxiInSeen && _taxiInStrobesOff,
                MaxGroundSpeedKnots = _taxiInMaxGroundSpeed,
                ExcessiveTurnSpeedEvents = _taxiInTurnSpeedEvents,
                TaxiLightsOn = _taxiInSeen && _taxiInTaxiLightsValid,
            },
            Arrival = new ArrivalMetrics
            {
                TaxiLightsOffBeforeParkingBrakeSet = _arrivalSeen && _arrivalParkingBrakeObserved && _arrivalTaxiLightsOffBeforeParkingBrakeSet,
                ParkingBrakeSetBeforeAllEnginesShutdown = _arrivalSeen && _arrivalParkingBrakeObserved && _arrivalParkingBrakeSetBeforeAllEnginesShutdown,
                AllEnginesOffByEndOfSession = _arrivalSeen && _arrivalAllEnginesOffByEndOfSession,
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
        if (!_taxiOutSeen && frame.BeaconLightOn)
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

        if (!frame.TaxiLightsOn)
        {
            _taxiOutTaxiLightsValid = false;
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

            if (!frame.LandingLightsOn)
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

            if (!frame.OnGround && !frame.StrobesOn)
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
        _cruiseMaxAltitudeDeviation = Math.Max(_cruiseMaxAltitudeDeviation, Math.Abs(frame.IndicatedAltitudeFeet - _cruiseTargetAltitudeFeet.Value));

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

        if (_previousFrame is not null && _previousFrame.Phase == FlightPhase.Cruise)
        {
            var machDelta = Math.Abs(frame.Mach - _previousFrame.Mach);
            var iasDelta = Math.Abs(frame.IndicatedAirspeedKnots - _previousFrame.IndicatedAirspeedKnots);
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
                _cruiseSpeedInstabilityActive = false;
                _cruiseSpeedInstabilityStartedAt = null;
            }
        }
        else
        {
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

        if (frame.IndicatedAltitudeFeet <= 18000 && !frame.LandingLightsOn)
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
            _landingTouchdownVerticalSpeedFpm = Math.Max(_landingTouchdownVerticalSpeedFpm, CalculateTouchdownVerticalSpeed());
            _landingTouchdownGForce = Math.Max(_landingTouchdownGForce, CalculateTouchdownGForce());

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

        if (frame.LandingLightsOn)
        {
            _taxiInLandingLightsOff = false;
        }

        if (frame.StrobesOn)
        {
            _taxiInStrobesOff = false;
        }

        if (!frame.TaxiLightsOn)
        {
            _taxiInTaxiLightsValid = false;
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
                // This tracks the last known taxi-light state before the parking brake was set.
                // A same-frame brake+lights-off update does not prove the required order.
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

    private double CalculateTouchdownVerticalSpeed()
    {
        var touchdownSamples = _recentSamples
            .Where(sample => sample.AltitudeAglFeet <= 100)
            .ToList();

        if (touchdownSamples.Count == 0)
        {
            return 0;
        }

        var minVs = touchdownSamples.Min(sample => sample.VerticalSpeedFpm);
        return Math.Abs(minVs);
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
