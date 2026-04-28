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
    // v3 new taxi out metrics
    private double _taxiOutMaxTurnSpeed;
    private bool _taxiOutStrobeLightOn;

    private bool _takeoffSeen;
    private bool _takeoffLandingLightsOnBeforeTakeoff = true;
    private bool _takeoffLandingLightsOffByFl180 = true;
    private bool _takeoffStrobesOnFromTakeoffToLanding = true;
    private double _takeoffMaxBank;
    private double _takeoffMaxPitch;
    private double _takeoffMaxPitchAglFt;
    private double _takeoffMaxG;
    private double _takeoffGForceAtRotation;
    private int _takeoffBounceCount;
    private bool _takeoffTailStrikeDetected;
    private DateTimeOffset? _lastTakeoffLiftoffAt;
    // v3 new takeoff metrics
    private bool _takeoffPositiveRateBeforeGearUp = true;

    private double _climbMaxIasBelowFl100;
    private double _climbMaxBank;
    private double _climbMaxG;
    // v3 new climb metrics
    private double _climbMinG = double.MaxValue;
    private bool _climbLandingLightsOffAboveFL180 = true;

    private double _cruiseMaxAltitudeDeviation;
    private double _cruiseMaxBank;
    private double _cruiseMaxG;
    private int _cruiseSpeedInstabilityEvents;
    // v3 new cruise metrics
    private double _cruiseMinG = double.MaxValue;
    private double _cruiseMaxTurnBank;
    private double _cruiseMaxIasDeviation;
    private double _cruiseMaxMachDeviation;
    private DateTimeOffset? _lastCruiseSpeedInstabilityAt;
    private DateTimeOffset? _cruiseSpeedInstabilityStartedAt;
    private bool _cruiseSpeedInstabilityActive;
    private double? _cruiseReferenceIasKnots;
    private double? _cruiseReferenceMach;
    private double? _cruiseTargetAltitudeFeet;
    private double? _newFlightLevelCaptureSeconds;
    // Settling: target floats until VS has been ≤ 100 fpm for 30 consecutive seconds.
    private DateTimeOffset? _cruiseLowVsStartedAt;
    // Capture timing: set when VS first drops below 300 fpm after a step climb.
    private DateTimeOffset? _cruiseLevelingStartedAt;
    private bool _cruiseHadSignificantClimb;

    private bool _descentSeen;
    private double _descentMaxIasBelowFl100;
    private double _descentMaxBank;
    private double _descentMaxPitch;
    private double _descentMaxG;
    private bool _descentLandingLightsOnBy9900 = true;
    // v3 new descent metrics
    private double _descentMinG = double.MaxValue;
    private double _descentMaxDescentRateFpm;
    private double _descentMaxNoseDownPitch;
    private bool _descentLandingLightsOnBeforeFL180 = true;

    private bool _capturedApproach1000Agl;
    private bool _gearDownBy1000Agl;
    private bool _capturedApproach500Agl;
    private int _approachFlapsAt500Agl;
    private double _approachVsAt500Agl;
    private double _approachBankAt500Agl;
    private double _approachPitchAt500Agl;
    private bool _approachGearDownAt500Agl;
    // ILS approach quality
    private bool _approachIlsDetected;
    private double _approachMaxGlideslope;
    private double _approachMaxLocalizer;
    private double _approachGlideslopeSampleSum;
    private double _approachLocalizerSampleSum;
    private int _approachIlsSampleCount;
    // v3 new approach metrics
    private double _approachIasAt500Agl;
    private double? _approachGearDownAglFeet;
    private bool _approachFlapsConfiguredBy1000Agl;
    private double _approach1000To500MinIas = double.MaxValue;
    private double _approach1000To500MaxIas;
    private bool _capturedApproach3000Agl;
    private double _approachMaxBankAngle3000to500;
    // v3 stabilized approach (below 500 AGL)
    private bool _stabilizedApproachActive;
    private double _stabilizedMaxIasDev;
    private double _stabilizedMaxDescentRate;
    private bool _stabilizedConfigChanged;
    private double _stabilizedHeadingAt500Agl;
    private double _stabilizedMaxHeadingDev;
    private bool _stabilizedIlsAvailable;
    private double _stabilizedMaxGlideslope;
    private double _stabilizedPitchAtGate;
    private int _stabilizedFlapsAt500Agl;
    private bool _stabilizedGearAt500Agl;

    private int _landingBounceCount;
    private double _landingTouchdownZoneExcessDistanceFeet;
    private double _landingTouchdownVerticalSpeedFpm;
    private double _landingTouchdownBankAngleDegrees;
    private double _landingTouchdownIndicatedAirspeedKnots;
    private double _landingTouchdownPitchAngleDegrees;
    private double _landingTouchdownGForce;
    private double _landingTouchdownCenterlineDeviationFeet;
    private double _landingTouchdownCrabAngleDegrees;
    private double _landingTouchdownLatitude;
    private double _landingTouchdownLongitude;
    private double _landingTouchdownHeadingDegrees;
    private double _landingTouchdownAltitudeFeet;
    // Extended touchdown context
    private bool _landingAutopilotAtTouchdown;
    private bool _landingSpoilersAtTouchdown;
    private bool _landingReverseThrustUsed;
    private double _landingWindSpeedAtTouchdown;
    private double _landingWindDirectionAtTouchdown;
    private double _landingOatAtTouchdown;
    private double _landingHeadwindComponent;
    private double _landingCrosswindComponent;
    // v3 new landing metrics
    private bool _landingGearUpAtTouchdown;
    private double _landingMaxPitchDuringRollout;
    private readonly List<FlightEvent> _flightEvents = [];

    // ── Approach path (circular buffer, max 300 samples) ─────────────────────
    private const int ApproachPathMaxSamples = 300;
    private readonly Queue<ApproachSamplePoint> _approachPathBuffer = new();

    // ── Flight path (one point every 60 s, blocks-off → blocks-on) ───────────
    private readonly List<FlightPathPoint> _flightPath = [];

    private DateTimeOffset? _lastTouchdownAt;
    private DateTimeOffset? _airborneAfterTouchdownAt;

    private bool _taxiInSeen;
    private bool _taxiInLandingLightsOff = true;
    private bool _taxiInStrobesOff = true;
    private bool _taxiInTaxiLightsValid = true;
    private DateTimeOffset? _taxiInStartedAt;               // when taxi-in phase was first entered
    private DateTimeOffset? _taxiInLightsOffStart;
    private DateTimeOffset? _taxiInLandingLightsOnStart;   // 60 s sustained on → penalty (1 min after vacate)
    private DateTimeOffset? _taxiInStrobesOnStart;          // 60 s sustained on → penalty
    private double _taxiInMaxGroundSpeed;
    private int _taxiInTurnSpeedEvents;
    private DateTimeOffset? _lastTaxiInTurnEventAt;
    // v3 new taxi in metrics
    private double _taxiInMaxTurnSpeed;
    private bool _taxiInStrobeLightOn;
    private bool _taxiInSmoothDeceleration = true;
    private DateTimeOffset? _taxiInPrevSpeedSampleAt;
    private double _taxiInPrevGroundSpeed;

    private bool _arrivalSeen;
    private bool _arrivalParkingBrakeObserved;
    private bool _arrivalTaxiLightsOffBeforeParkingBrakeSet;
    private bool _arrivalAllEnginesOffBeforeParkingBrakeSet = true;
    private bool _arrivalAllEnginesOffByEndOfSession;
    private bool _arrivalEnginesOffAfterParkingBrake;
    private bool _arrivalAllEnginesOffSeen;
    private bool _arrivalBeaconOffAfterEngines;

    // ── Session-level metrics ──────────────────────────────────────────────
    private DateTimeOffset? _sessionEnginesStartedAt;
    private DateTimeOffset? _sessionWheelsOffAt;
    private DateTimeOffset? _sessionWheelsOnAt;
    private DateTimeOffset? _sessionEnginesOffAt;
    private double _sessionFuelAtDepartureLbs;
    private double _sessionFuelAtLandingLbs;
    private bool _sessionFuelDepartureRecorded;
    // GPS track — one point every GPS_TRACK_INTERVAL_SECONDS
    private DateTimeOffset _lastGpsTrackPointAt;
    private readonly List<GpsTrackPoint> _gpsTrack = [];
    private const double GpsTrackIntervalSeconds = 30;

    // After a session restore (tracker restart / reconnect) the first few frames from
    // SimConnect may carry stale boolean SimVar values (all false) before MSFS has
    // fully populated the aircraft state.  We skip "negative" watchdog checks for
    // this many frames to prevent false deductions on reconnect.
    private int _postRestoreGraceFrames;

    // v3 lights systems (whole-flight)
    private bool _lightsBeaconOnThroughout = true;
    private bool _lightsNavOnThroughout = true;
    private bool _lightsStrobesCorrect = true;
    private bool _lightsStrobeWindowOpen;
    private int _lightsLandingCompliantSamples;
    private int _lightsLandingTotalSamples;
    private DateTimeOffset? _lightsLastComplianceSampleAt;

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
        _takeoffMaxPitchAglFt = input.Takeoff.MaxPitchAglFeet;
        _takeoffMaxG = input.Takeoff.MaxGForce;
        _takeoffGForceAtRotation = input.Takeoff.GForceAtRotation;
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
        _newFlightLevelCaptureSeconds = input.Cruise.NewFlightLevelCaptureSeconds;
        // Settling state is not persisted — live frames will re-establish it.
        _cruiseLowVsStartedAt = null;
        _cruiseLevelingStartedAt = null;
        _cruiseHadSignificantClimb = false;

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
        _descentMaxNoseDownPitch = input.Descent.MaxNoseDownPitchDegrees;
        _descentMaxPitch = input.Descent.MaxPitchAngleDegrees;
        _descentMaxG = input.Descent.MaxGForce;
        _descentLandingLightsOnBy9900 = !_descentSeen || input.Descent.LandingLightsOnBy9900;

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
        _landingTouchdownCenterlineDeviationFeet = input.Landing.TouchdownCenterlineDeviationFeet;
        _landingTouchdownCrabAngleDegrees = input.Landing.TouchdownCrabAngleDegrees;
        _landingTouchdownLatitude = input.Landing.TouchdownLatitude;
        _landingTouchdownLongitude = input.Landing.TouchdownLongitude;
        _landingTouchdownHeadingDegrees = input.Landing.TouchdownHeadingDegrees;
        _landingTouchdownAltitudeFeet = input.Landing.TouchdownAltitudeFeet;
        _lastTouchdownAt = wheelsOnUtc;
        _airborneAfterTouchdownAt = null;

        _taxiInSeen = HasReachedPhase(currentPhase, FlightPhase.TaxiIn);
        _taxiInLandingLightsOff = !_taxiInSeen || input.TaxiIn.LandingLightsOff;
        _taxiInStrobesOff = !_taxiInSeen || input.TaxiIn.StrobesOff;
        _taxiInTaxiLightsValid = !_taxiInSeen || input.TaxiIn.TaxiLightsOn;
        _taxiInStartedAt = null; // grace window re-arms on reconnect
        _taxiInLightsOffStart = null;
        _taxiInLandingLightsOnStart = null;
        _taxiInStrobesOnStart = null;
        _taxiInMaxGroundSpeed = input.TaxiIn.MaxGroundSpeedKnots;
        _taxiInTurnSpeedEvents = input.TaxiIn.ExcessiveTurnSpeedEvents;
        _lastTaxiInTurnEventAt = null;

        _arrivalSeen = currentPhase == FlightPhase.Arrival;
        _arrivalParkingBrakeObserved = currentPhase == FlightPhase.Arrival;
        _arrivalTaxiLightsOffBeforeParkingBrakeSet = input.Arrival.TaxiLightsOffBeforeParkingBrakeSet;
        _arrivalAllEnginesOffBeforeParkingBrakeSet =
            !_arrivalSeen || input.Arrival.AllEnginesOffBeforeParkingBrakeSet;
        _arrivalAllEnginesOffByEndOfSession = input.Arrival.AllEnginesOffByEndOfSession;
        _arrivalEnginesOffAfterParkingBrake = input.Arrival.EnginesOffAfterParkingBrake;
        _arrivalAllEnginesOffSeen = _arrivalSeen && input.Arrival.EnginesOffAfterParkingBrake;
        _arrivalBeaconOffAfterEngines = input.Arrival.BeaconOffAfterEngines;

        // ── Session-level fields ──────────────────────────────────────────────
        _sessionEnginesStartedAt = input.Session.EnginesStartedAtUtc;
        _sessionWheelsOffAt      = input.Session.WheelsOffAtUtc;
        _sessionWheelsOnAt       = input.Session.WheelsOnAtUtc;
        _sessionEnginesOffAt     = input.Session.EnginesOffAtUtc;
        _sessionFuelAtDepartureLbs = input.Session.FuelAtDepartureLbs;
        _sessionFuelAtLandingLbs   = input.Session.FuelAtLandingLbs;
        // Departure fuel is recorded once the aircraft starts moving — flag as recorded
        // if we have a non-zero value (so the first live frame doesn't overwrite it).
        _sessionFuelDepartureRecorded = input.Session.FuelAtDepartureLbs > 0;
        // Restore GPS track from saved state; set interval timer to avoid duplicates.
        _gpsTrack.Clear();
        _gpsTrack.AddRange(input.GpsTrack);
        _lastGpsTrackPointAt = _gpsTrack.Count > 0
            ? _gpsTrack[^1].TimestampUtc
            : DateTimeOffset.MinValue;

        // Restore flight path from saved state.
        _flightPath.Clear();
        _flightPath.AddRange(input.FlightPath);

        // ── ILS approach quality ──────────────────────────────────────────────
        _approachIlsDetected   = input.Approach.IlsApproachDetected;
        _approachMaxGlideslope = input.Approach.MaxGlideslopeDeviationDots;
        _approachMaxLocalizer  = input.Approach.MaxLocalizerDeviationDots;
        // Sample sum / count can't be reconstructed from averages alone; reset to zero
        // so new live samples after reconnect accumulate correctly.
        _approachGlideslopeSampleSum = 0;
        _approachLocalizerSampleSum  = 0;
        _approachIlsSampleCount      = 0;

        // ── Extended touchdown context ────────────────────────────────────────
        _landingAutopilotAtTouchdown    = input.Landing.AutopilotEngagedAtTouchdown;
        _landingSpoilersAtTouchdown     = input.Landing.SpoilersDeployedAtTouchdown;
        _landingReverseThrustUsed       = input.Landing.ReverseThrustUsed;
        _landingWindSpeedAtTouchdown    = input.Landing.WindSpeedAtTouchdownKnots;
        _landingWindDirectionAtTouchdown = input.Landing.WindDirectionAtTouchdownDegrees;
        _landingOatAtTouchdown          = input.Landing.OatCelsiusAtTouchdown;
        _landingHeadwindComponent       = input.Landing.HeadwindComponentKnots;
        _landingCrosswindComponent      = input.Landing.CrosswindComponentKnots;

        // Approach path is not restored — it cannot be reconstructed from saved state.
        // The buffer starts empty and will accumulate fresh samples if the aircraft
        // re-enters approach range after a mid-flight reconnect.
        _approachPathBuffer.Clear();
        _flightEvents.Clear();

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
        UpdateSession(frame);
        UpdateLightsSystems(frame);
        RecordFlightEvents(frame);

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
                MaxTurnSpeedKnots = _taxiOutMaxTurnSpeed,
                StrobeLightOnDuringTaxi = _taxiOutStrobeLightOn,
            },
            Takeoff = new TakeoffMetrics
            {
                BounceCount = _takeoffBounceCount,
                TailStrikeDetected = _takeoffTailStrikeDetected,
                MaxBankAngleDegrees = _takeoffMaxBank,
                MaxPitchAngleDegrees = _takeoffMaxPitch,
                MaxPitchAglFeet = _takeoffMaxPitchAglFt,
                MaxGForce = _takeoffMaxG,
                // Pass if the phase hasn't been seen yet — only penalise once we enter it.
                LandingLightsOnBeforeTakeoff = !_takeoffSeen || _takeoffLandingLightsOnBeforeTakeoff,
                LandingLightsOffByFl180 = !_takeoffSeen || _takeoffLandingLightsOffByFl180,
                StrobesOnFromTakeoffToLanding = !_takeoffSeen || _takeoffStrobesOnFromTakeoffToLanding,
                PositiveRateBeforeGearUp = _takeoffPositiveRateBeforeGearUp,
                GForceAtRotation = _takeoffGForceAtRotation,
            },
            Climb = new ClimbMetrics
            {
                HeavyFourEngineAircraft = _profile.HeavyFourEngineAircraft,
                MaxIasBelowFl100Knots = _climbMaxIasBelowFl100,
                MaxBankAngleDegrees = _climbMaxBank,
                MaxGForce = _climbMaxG,
                MinGForce = _climbMinG == double.MaxValue ? 0 : _climbMinG,
                LandingLightsOffAboveFL180 = _climbLandingLightsOffAboveFL180,
            },
            Cruise = new CruiseMetrics
            {
                MaxAltitudeDeviationFeet = _cruiseMaxAltitudeDeviation,
                NewFlightLevelCaptureSeconds = _newFlightLevelCaptureSeconds,
                SpeedInstabilityEvents = _cruiseSpeedInstabilityEvents,
                MaxBankAngleDegrees = _cruiseMaxBank,
                MaxGForce = _cruiseMaxG,
                CruiseAltitudeFeet = _cruiseTargetAltitudeFeet,
                MachTarget = _cruiseReferenceMach,
                MaxMachDeviation = _cruiseMaxMachDeviation,
                IasTarget = _cruiseReferenceIasKnots,
                MaxIasDeviationKnots = _cruiseMaxIasDeviation,
                MaxTurnBankAngleDegrees = _cruiseMaxTurnBank,
                MinGForce = _cruiseMinG == double.MaxValue ? 0 : _cruiseMinG,
            },
            Descent = new DescentMetrics
            {
                MaxIasBelowFl100Knots = _descentMaxIasBelowFl100,
                MaxBankAngleDegrees = _descentMaxBank,
                MaxPitchAngleDegrees = _descentMaxPitch,
                MaxGForce = _descentMaxG,
                // Pass if descent hasn't been seen yet — only penalise once we enter it.
                LandingLightsOnBy9900 = !_descentSeen || _descentLandingLightsOnBy9900,
                MinGForce = _descentMinG == double.MaxValue ? 0 : _descentMinG,
                MaxDescentRateFpm = _descentMaxDescentRateFpm,
                LandingLightsOnBeforeFL180 = !_descentSeen || _descentLandingLightsOnBeforeFL180,
                MaxNoseDownPitchDegrees = _descentMaxNoseDownPitch,
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
                IlsApproachDetected = _approachIlsDetected,
                MaxGlideslopeDeviationDots = _approachMaxGlideslope,
                AvgGlideslopeDeviationDots = _approachIlsSampleCount > 0
                    ? _approachGlideslopeSampleSum / _approachIlsSampleCount : 0,
                MaxLocalizerDeviationDots = _approachMaxLocalizer,
                AvgLocalizerDeviationDots = _approachIlsSampleCount > 0
                    ? _approachLocalizerSampleSum / _approachIlsSampleCount : 0,
                ApproachSpeedKnots = _approachIasAt500Agl,
                MaxIasDeviationKnots = (_approach1000To500MaxIas > 0 && _approach1000To500MinIas != double.MaxValue)
                    ? _approach1000To500MaxIas - _approach1000To500MinIas : 0,
                GearDownAglFeet = _approachGearDownAglFeet,
                FlapsConfiguredBy1000Agl = _approachFlapsConfiguredBy1000Agl,
                MaxBankAngleDegrees3000to500 = _approachMaxBankAngle3000to500,
            },
            StabilizedApproach = new StabilizedApproachMetrics
            {
                ApproachSpeedKnots = _approachIasAt500Agl,
                MaxIasDeviationKnots = _stabilizedMaxIasDev,
                MaxDescentRateFpm = _stabilizedMaxDescentRate,
                ConfigChanged = _stabilizedConfigChanged,
                MaxHeadingDeviationDegrees = _stabilizedMaxHeadingDev,
                IlsAvailable = _stabilizedIlsAvailable,
                MaxGlideslopeDeviationDots = _stabilizedMaxGlideslope,
                PitchAtGateDegrees = _stabilizedPitchAtGate,
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
                TouchdownCenterlineDeviationFeet = _landingTouchdownCenterlineDeviationFeet,
                TouchdownCrabAngleDegrees = _landingTouchdownCrabAngleDegrees,
                TouchdownLatitude = _landingTouchdownLatitude,
                TouchdownLongitude = _landingTouchdownLongitude,
                TouchdownHeadingDegrees = _landingTouchdownHeadingDegrees,
                AutopilotEngagedAtTouchdown = _landingAutopilotAtTouchdown,
                SpoilersDeployedAtTouchdown = _landingSpoilersAtTouchdown,
                ReverseThrustUsed = _landingReverseThrustUsed,
                WindSpeedAtTouchdownKnots = _landingWindSpeedAtTouchdown,
                WindDirectionAtTouchdownDegrees = _landingWindDirectionAtTouchdown,
                HeadwindComponentKnots = _landingHeadwindComponent,
                CrosswindComponentKnots = _landingCrosswindComponent,
                OatCelsiusAtTouchdown = _landingOatAtTouchdown,
                GearUpAtTouchdown = _landingGearUpAtTouchdown,
                MaxPitchDuringRolloutDegrees = _landingMaxPitchDuringRollout,
                TouchdownAltitudeFeet = _landingTouchdownAltitudeFeet,
            },
            TaxiIn = new TaxiInMetrics
            {
                // Pass if the phase hasn't been seen yet — only penalise once we enter it.
                LandingLightsOff = !_taxiInSeen || _taxiInLandingLightsOff,
                StrobesOff = !_taxiInSeen || _taxiInStrobesOff,
                MaxGroundSpeedKnots = _taxiInMaxGroundSpeed,
                ExcessiveTurnSpeedEvents = _taxiInTurnSpeedEvents,
                TaxiLightsOn = !_taxiInSeen || _taxiInTaxiLightsValid,
                MaxTurnSpeedKnots = _taxiInMaxTurnSpeed,
                StrobeLightOnDuringTaxi = _taxiInStrobeLightOn,
                SmoothDeceleration = _taxiInSmoothDeceleration,
            },
            Arrival = new ArrivalMetrics
            {
                // Pass if the phase hasn't been seen yet — only penalise once we enter it.
                TaxiLightsOffBeforeParkingBrakeSet = !_arrivalSeen || (_arrivalParkingBrakeObserved && _arrivalTaxiLightsOffBeforeParkingBrakeSet),
                AllEnginesOffBeforeParkingBrakeSet = !_arrivalSeen || (_arrivalParkingBrakeObserved && _arrivalAllEnginesOffBeforeParkingBrakeSet),
                AllEnginesOffByEndOfSession = !_arrivalSeen || _arrivalAllEnginesOffByEndOfSession,
                EnginesOffAfterParkingBrake = !_arrivalSeen || (_arrivalParkingBrakeObserved && _arrivalEnginesOffAfterParkingBrake),
                BeaconOffAfterEngines       = !_arrivalSeen || _arrivalBeaconOffAfterEngines,
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
            LightsSystems = new LightsSystemsMetrics
            {
                BeaconOnThroughoutFlight = _lightsBeaconOnThroughout,
                NavLightsOnThroughoutFlight = _lightsNavOnThroughout,
                StrobesCorrect = _lightsStrobesCorrect,
                LandingLightsCompliance = _lightsLandingTotalSamples > 0
                    ? (double)_lightsLandingCompliantSamples / _lightsLandingTotalSamples
                    : 1.0,
            },
            Session = new SessionMetrics
            {
                EnginesStartedAtUtc = _sessionEnginesStartedAt,
                WheelsOffAtUtc = _sessionWheelsOffAt,
                WheelsOnAtUtc = _sessionWheelsOnAt,
                EnginesOffAtUtc = _sessionEnginesOffAt,
                FuelAtDepartureLbs = _sessionFuelAtDepartureLbs,
                FuelAtLandingLbs = _sessionFuelAtLandingLbs,
            },
            GpsTrack = _gpsTrack,
            ApproachPath = _approachPathBuffer.Count > 0
                ? _approachPathBuffer.ToList()
                : [],
            FlightPath = _flightPath.Count > 0
                ? _flightPath.ToList()
                : [],
            FlightEvents = _flightEvents.Count > 0 ? _flightEvents.ToList() : [],
        };
    }

    public ScoreResult CalculateScore(ScoringEngine? engine = null, ScoringWeights? weights = null)
    {
        engine ??= new ScoringEngine();
        return engine.Calculate(BuildScoreInput(), weights);
    }

    /// <summary>
    /// Appends one approach telemetry sample to the rolling buffer.
    /// If the buffer is at capacity (300 samples) the oldest entry is dropped first.
    /// Called by RuntimeCoordinator every ~2 s during Descent/Approach when the
    /// aircraft is within 15 nm of the arrival airport, and once at touchdown.
    /// </summary>
    public void RecordApproachSample(double distanceToThresholdNm, double altitudeFeet,
                                     double indicatedAirspeedKnots, double verticalSpeedFpm,
                                     double latitude, double longitude, DateTimeOffset timestampUtc)
    {
        if (_approachPathBuffer.Count >= ApproachPathMaxSamples)
            _approachPathBuffer.Dequeue();

        _approachPathBuffer.Enqueue(new ApproachSamplePoint
        {
            DistanceToThresholdNm  = distanceToThresholdNm,
            AltitudeFeet           = altitudeFeet,
            IndicatedAirspeedKnots = indicatedAirspeedKnots,
            VerticalSpeedFpm       = verticalSpeedFpm,
            Latitude               = latitude,
            Longitude              = longitude,
            TimestampUtc           = timestampUtc,
        });
    }

    /// <summary>
    /// Clears the approach path buffer.  Called by RuntimeCoordinator when a crash
    /// is detected or the flight is cancelled so no partial data is uploaded.
    /// </summary>
    public void DiscardApproachPath() => _approachPathBuffer.Clear();

    /// <summary>
    /// Appends one flight-path point sampled at 60-second intervals during blocks-off → blocks-on.
    /// Called by RuntimeCoordinator. No capacity cap — at 60 s intervals a 12-hour flight
    /// produces at most ~720 points.
    /// </summary>
    public void RecordFlightPathPoint(TelemetryFrame frame)
    {
        _flightPath.Add(new FlightPathPoint
        {
            TimestampUtc  = frame.TimestampUtc,
            Latitude      = frame.Latitude,
            Longitude     = frame.Longitude,
            AltitudeFeet  = frame.AltitudeFeet,
        });
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

        // v3: track max turn speed (GS when heading changes significantly)
        if (_previousFrame is not null && _previousFrame.Phase == FlightPhase.TaxiOut)
        {
            var headingDelta = NormalizeHeadingDelta(frame.HeadingTrueDegrees - _previousFrame.HeadingTrueDegrees);
            var dtSeconds = (frame.TimestampUtc - _previousFrame.TimestampUtc).TotalSeconds;
            if (dtSeconds > 0 && Math.Abs(headingDelta) / dtSeconds > 5.0)
            {
                _taxiOutMaxTurnSpeed = Math.Max(_taxiOutMaxTurnSpeed, frame.GroundSpeedKnots);
            }
        }

        // v3: latch strobes on during taxi out
        if (frame.StrobesOn)
            _taxiOutStrobeLightOn = true;
    }

    private void UpdateTakeoff(TelemetryFrame frame)
    {
        // Accumulate max pitch while WOW is true, from blocks-off (TaxiOut phase seen)
        // until wheels-off (WOW goes false).  Gated to AGL < 20 ft to avoid capturing
        // normal climb-out pitch if the WOW SimVar lags a few frames after liftoff.
        // Runs regardless of the exact phase so the entire takeoff roll is included,
        // not just the frames where the phase has already transitioned to Takeoff.
        if (_taxiOutSeen && frame.OnGround && frame.AltitudeAglFeet < 20.0)
        {
            var absPitch = Math.Abs(frame.PitchAngleDegrees);
            if (absPitch > _takeoffMaxPitch)
            {
                _takeoffMaxPitch      = absPitch;
                _takeoffMaxPitchAglFt = frame.AltitudeAglFeet;
            }
        }

        if (frame.Phase == FlightPhase.Takeoff)
        {
            _takeoffSeen = true;
            _takeoffMaxBank = Math.Max(_takeoffMaxBank, Math.Abs(frame.BankAngleDegrees));
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
            _takeoffGForceAtRotation = frame.GForce;
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

        // v3: detect gear retraction after liftoff to check positive-rate-before-gear-up
        if (_previousFrame.GearDown && !frame.GearDown &&
            (frame.Phase == FlightPhase.Takeoff || frame.Phase == FlightPhase.Climb) &&
            !frame.OnGround)
        {
            _takeoffPositiveRateBeforeGearUp =
                frame.VerticalSpeedFpm > 100 || _previousFrame.VerticalSpeedFpm > 100;
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

        // v3 new metrics
        _climbMinG = Math.Min(_climbMinG, frame.GForce);
        if (frame.IndicatedAltitudeFeet >= 18000 && frame.LandingLightsOn)
            _climbLandingLightsOffAboveFL180 = false;
    }

    private void UpdateCruise(TelemetryFrame frame)
    {
        if (frame.Phase != FlightPhase.Cruise)
        {
            return;
        }

        _cruiseMaxBank = Math.Max(_cruiseMaxBank, Math.Abs(frame.BankAngleDegrees));
        _cruiseMaxG = Math.Max(_cruiseMaxG, frame.GForce);

        // v3 new metrics
        _cruiseMinG = Math.Min(_cruiseMinG, frame.GForce);

        var absVs = Math.Abs(frame.VerticalSpeedFpm);
        var currentAltitude = Math.Round(frame.IndicatedAltitudeFeet / 100.0) * 100.0;

        // ── Altitude target settling ──────────────────────────────────────────
        // The cruise altitude target "floats" to follow the aircraft until VS has
        // been ≤ 100 fpm for 30 consecutive seconds.  Re-arms on every climb or
        // descent so both the initial level-off and all step climbs are excluded
        // from altitude deviation tracking.
        if (absVs > 100)
        {
            // Aircraft is climbing or descending — reset settling timer and keep
            // the target following.
            _cruiseLowVsStartedAt = null;
            _cruiseTargetAltitudeFeet = currentAltitude;

            if (absVs > 300)
            {
                // Significant VS: a step climb/descent is in progress.
                // Reset the leveling timer until VS drops back below 300 fpm.
                _cruiseHadSignificantClimb = true;
                _cruiseLevelingStartedAt = null;
            }
            else if (_cruiseHadSignificantClimb)
            {
                // VS is 100–300 fpm: leveling off after a step climb.
                // Start the capture timer on the first such frame.
                _cruiseLevelingStartedAt ??= frame.TimestampUtc;
            }
        }
        else
        {
            // VS ≤ 100 fpm — accumulate consecutive seconds of level flight.
            _cruiseLowVsStartedAt ??= frame.TimestampUtc;

            // If we came from a step climb, start the capture timer now.
            if (_cruiseHadSignificantClimb)
                _cruiseLevelingStartedAt ??= frame.TimestampUtc;

            var settledSeconds = (frame.TimestampUtc - _cruiseLowVsStartedAt.Value).TotalSeconds;

            if (settledSeconds < 30)
            {
                // Still within the settling window — keep target following.
                _cruiseTargetAltitudeFeet = currentAltitude;
            }
            else
            {
                // Fully settled: lock the target and start tracking deviations.
                _cruiseTargetAltitudeFeet ??= currentAltitude;

                // Record step-climb capture time (time from leveling-off start to
                // fully settled), so the scoring engine can penalise slow captures.
                if (_cruiseHadSignificantClimb && _cruiseLevelingStartedAt is not null)
                {
                    var captureSeconds = (frame.TimestampUtc - _cruiseLevelingStartedAt.Value).TotalSeconds;
                    _newFlightLevelCaptureSeconds = _newFlightLevelCaptureSeconds is null
                        ? captureSeconds
                        : Math.Max(_newFlightLevelCaptureSeconds.Value, captureSeconds);
                }

                _cruiseHadSignificantClimb = false;
                _cruiseLevelingStartedAt = null;

                // |VS| ≤ 100 fpm mask — reaching this line requires absVs ≤ 100 (outer
                // else-branch), so only genuinely settled ticks contribute to the deviation.
                // Any tick where |VS| > 100 fpm resets the settling timer above and never
                // reaches here, satisfying the spec requirement to skip those ticks entirely.
                var deviation = Math.Abs(frame.IndicatedAltitudeFeet - _cruiseTargetAltitudeFeet.Value);
                _cruiseMaxAltitudeDeviation = Math.Max(_cruiseMaxAltitudeDeviation, deviation);
            }
        }

        // ── Speed instability ─────────────────────────────────────────────────
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

        // v3: track max deviations
        _cruiseMaxMachDeviation = Math.Max(_cruiseMaxMachDeviation, machDelta);
        _cruiseMaxIasDeviation = Math.Max(_cruiseMaxIasDeviation, iasDelta);

        // v3: track turn bank (bank when heading is changing)
        if (_previousFrame is not null)
        {
            var headingDelta = NormalizeHeadingDelta(frame.HeadingTrueDegrees - _previousFrame.HeadingTrueDegrees);
            var dtSeconds = (frame.TimestampUtc - _previousFrame.TimestampUtc).TotalSeconds;
            if (dtSeconds > 0 && Math.Abs(headingDelta) / dtSeconds > 1.0)
            {
                _cruiseMaxTurnBank = Math.Max(_cruiseMaxTurnBank, Math.Abs(frame.BankAngleDegrees));
            }
        }

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

        // Check at 9,900 ft rather than FL180 — pilots have until FL100 to turn lights on.
        if (_postRestoreGraceFrames <= 0 && frame.IndicatedAltitudeFeet <= 9900 && !frame.LandingLightsOn)
        {
            _descentLandingLightsOnBy9900 = false;
        }

        _descentMaxBank = Math.Max(_descentMaxBank, Math.Abs(frame.BankAngleDegrees));
        _descentMaxPitch = Math.Max(_descentMaxPitch, Math.Abs(frame.PitchAngleDegrees));
        _descentMaxG = Math.Max(_descentMaxG, frame.GForce);

        // v3 new metrics
        _descentMinG = Math.Min(_descentMinG, frame.GForce);
        _descentMaxDescentRateFpm = Math.Max(_descentMaxDescentRateFpm, Math.Abs(frame.VerticalSpeedFpm));
        if (frame.PitchAngleDegrees < 0)
            _descentMaxNoseDownPitch = Math.Max(_descentMaxNoseDownPitch, Math.Abs(frame.PitchAngleDegrees));
        if (_postRestoreGraceFrames <= 0 && frame.IndicatedAltitudeFeet <= 18000 && !frame.LandingLightsOn)
            _descentLandingLightsOnBeforeFL180 = false;
    }

    private void UpdateApproach(TelemetryFrame frame)
    {
        if (frame.Phase != FlightPhase.Approach)
        {
            // Run stabilized approach tracking also during Landing phase (below 500 AGL)
            if (frame.Phase == FlightPhase.Landing)
                UpdateStabilizedApproach(frame);
            return;
        }

        // v3: track bank from 3000 AGL down to 500 AGL
        if (!_capturedApproach3000Agl && frame.AltitudeAglFeet <= 3000)
            _capturedApproach3000Agl = true;

        if (_capturedApproach3000Agl && frame.AltitudeAglFeet >= 500)
            _approachMaxBankAngle3000to500 = Math.Max(_approachMaxBankAngle3000to500, Math.Abs(frame.BankAngleDegrees));

        // v3: gear-down transition — record actual AGL at which gear was extended
        if (_previousFrame is not null && !_previousFrame.GearDown && frame.GearDown)
            _approachGearDownAglFeet = frame.AltitudeAglFeet;

        if (_previousFrame is not null)
        {
            if (!_capturedApproach1000Agl &&
                _previousFrame.AltitudeAglFeet > 1000 &&
                frame.AltitudeAglFeet <= 1000)
            {
                _capturedApproach1000Agl = true;
                _gearDownBy1000Agl = frame.GearDown;
                // v3: record flaps configured at 1000 AGL
                _approachFlapsConfiguredBy1000Agl = frame.FlapsHandleIndex > 0;
                // v3: start tracking IAS for 1000-500 AGL window
                _approach1000To500MinIas = frame.IndicatedAirspeedKnots;
                _approach1000To500MaxIas = frame.IndicatedAirspeedKnots;
            }

            // v3: accumulate IAS range in the 1000-500 AGL window
            if (_capturedApproach1000Agl && !_capturedApproach500Agl && frame.AltitudeAglFeet > 500)
            {
                _approach1000To500MinIas = Math.Min(_approach1000To500MinIas, frame.IndicatedAirspeedKnots);
                _approach1000To500MaxIas = Math.Max(_approach1000To500MaxIas, frame.IndicatedAirspeedKnots);
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
                // v3: record approach speed and stabilized approach reference data at 500 AGL
                _approachIasAt500Agl = frame.IndicatedAirspeedKnots;
                _stabilizedHeadingAt500Agl = frame.HeadingTrueDegrees;
                _stabilizedFlapsAt500Agl = frame.FlapsHandleIndex;
                _stabilizedGearAt500Agl = frame.GearDown;
                _stabilizedApproachActive = true;
                _stabilizedPitchAtGate = frame.PitchAngleDegrees;
            }
        }

        // ILS quality — accumulate glideslope and localizer deviation while a valid
        // signal is present, from 1000 ft AGL downwards.
        if (frame.Nav1IlsSignalValid && frame.AltitudeAglFeet <= 1000)
        {
            _approachIlsDetected = true;
            var gs = Math.Abs(frame.Nav1GlideslopeErrorDots);
            var loc = Math.Abs(frame.Nav1LocalizerErrorDots);
            _approachMaxGlideslope = Math.Max(_approachMaxGlideslope, gs);
            _approachMaxLocalizer  = Math.Max(_approachMaxLocalizer,  loc);
            _approachGlideslopeSampleSum += gs;
            _approachLocalizerSampleSum  += loc;
            _approachIlsSampleCount++;
        }

        // v3: stabilized approach tracking below 500 AGL
        if (frame.AltitudeAglFeet <= 500)
            UpdateStabilizedApproach(frame);
    }

    private void UpdateStabilizedApproach(TelemetryFrame frame)
    {
        if (!_stabilizedApproachActive)
            return;

        // IAS deviation from approach speed captured at 500 AGL
        if (_approachIasAt500Agl > 0)
            _stabilizedMaxIasDev = Math.Max(_stabilizedMaxIasDev,
                Math.Abs(frame.IndicatedAirspeedKnots - _approachIasAt500Agl));

        _stabilizedMaxDescentRate = Math.Max(_stabilizedMaxDescentRate, Math.Abs(frame.VerticalSpeedFpm));

        // Config change detection: gear or flaps changed below 500 AGL
        if (_stabilizedGearAt500Agl != frame.GearDown || _stabilizedFlapsAt500Agl != frame.FlapsHandleIndex)
            _stabilizedConfigChanged = true;

        // Heading deviation from heading at 500 AGL
        var headingDev = Math.Abs(NormalizeHeadingDelta(frame.HeadingTrueDegrees - _stabilizedHeadingAt500Agl));
        _stabilizedMaxHeadingDev = Math.Max(_stabilizedMaxHeadingDev, headingDev);

        // ILS tracking
        if (frame.Nav1IlsSignalValid)
        {
            _stabilizedIlsAvailable = true;
            _stabilizedMaxGlideslope = Math.Max(_stabilizedMaxGlideslope, Math.Abs(frame.Nav1GlideslopeErrorDots));
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
            _landingTouchdownVerticalSpeedFpm = Math.Min(_landingTouchdownVerticalSpeedFpm, CalculateTouchdownVerticalSpeed(frame));
            _landingTouchdownGForce = Math.Max(_landingTouchdownGForce, CalculateTouchdownGForce());
            if (_landingTouchdownIndicatedAirspeedKnots == 0)
            {
                // First touchdown — record all point-in-time context.
                _landingTouchdownBankAngleDegrees = Math.Abs(frame.BankAngleDegrees);
                _landingTouchdownIndicatedAirspeedKnots = frame.IndicatedAirspeedKnots;
                _landingTouchdownPitchAngleDegrees = frame.PitchAngleDegrees;
                _landingTouchdownLatitude = frame.Latitude;
                _landingTouchdownLongitude = frame.Longitude;
                _landingTouchdownHeadingDegrees = frame.HeadingTrueDegrees;
                _landingTouchdownAltitudeFeet = frame.AltitudeFeet;
                _landingAutopilotAtTouchdown = frame.AutopilotEngaged;
                _landingSpoilersAtTouchdown = frame.SpoilersArmed || frame.SpoilerHandlePosition > 0.1;
                _landingWindSpeedAtTouchdown = frame.WindSpeedKnots;
                _landingWindDirectionAtTouchdown = frame.WindDirectionDegrees;
                _landingOatAtTouchdown = frame.OutsideAirTempCelsius;
                _sessionFuelAtLandingLbs = frame.FuelTotalLbs;
                _sessionWheelsOnAt = frame.TimestampUtc;
                // v3: gear-up at touchdown
                _landingGearUpAtTouchdown = !frame.GearDown;
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

        // Reverse thrust — detect sustained N1 above idle while on the ground after landing.
        // A spoiler handle > 0.5 during rollout also confirms speedbrake deployment.
        if (frame.OnGround && _lastTouchdownAt is not null && frame.GroundSpeedKnots > 10)
        {
            var maxN1 = Math.Max(
                Math.Max(frame.Engine1N1Pct, frame.Engine2N1Pct),
                Math.Max(frame.Engine3N1Pct, frame.Engine4N1Pct));
            if (maxN1 > 20)
                _landingReverseThrustUsed = true;
        }

        // Track max pitch during the high-speed landing rollout: from WOW-on until
        // ground speed drops below 30 kts.  Stopping at 30 kts avoids capturing the
        // low-pitch taxi-in phase and keeps this metric comparable to the takeoff
        // maxPitchWhileWowDeg which ends at WOW-off.
        if (frame.OnGround && _lastTouchdownAt is not null && frame.GroundSpeedKnots >= 30.0)
            _landingMaxPitchDuringRollout = Math.Max(_landingMaxPitchDuringRollout, Math.Abs(frame.PitchAngleDegrees));
    }

    private void UpdateTaxiIn(TelemetryFrame frame)
    {
        if (frame.Phase != FlightPhase.TaxiIn)
        {
            return;
        }

        _taxiInSeen = true;
        _taxiInStartedAt ??= frame.TimestampUtc;
        _taxiInMaxGroundSpeed = Math.Max(_taxiInMaxGroundSpeed, frame.GroundSpeedKnots);

        // Landing lights — 60-second grace window after runway vacate before penalty locks in.
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

        // Strobes — same 60-second window.
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

        // Taxi lights — 60-second grace window after runway vacate, then require lights on
        // above 8 kts.  Below 8 kts (slowing to gate) lights off is correct procedure.
        // 3-second debounce: an accidental toggle doesn't cause a permanent penalty.
        var taxiInElapsed = frame.TimestampUtc - _taxiInStartedAt!.Value;
        if (taxiInElapsed >= TimeSpan.FromSeconds(60) &&
            frame.GroundSpeedKnots >= 8.0 &&
            _postRestoreGraceFrames <= 0)
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

        // v3: track max turn speed for taxi in
        if (_previousFrame is not null && _previousFrame.Phase == FlightPhase.TaxiIn)
        {
            var headingDelta = NormalizeHeadingDelta(frame.HeadingTrueDegrees - _previousFrame.HeadingTrueDegrees);
            var dtSeconds = (frame.TimestampUtc - _previousFrame.TimestampUtc).TotalSeconds;
            if (dtSeconds > 0 && Math.Abs(headingDelta) / dtSeconds > 5.0)
            {
                _taxiInMaxTurnSpeed = Math.Max(_taxiInMaxTurnSpeed, frame.GroundSpeedKnots);
            }
        }

        // v3: latch strobes on during taxi in
        if (frame.StrobesOn)
            _taxiInStrobeLightOn = true;

        // v3: deceleration smoothness — check if decel rate exceeds 3 kts/s
        if (_taxiInPrevSpeedSampleAt is not null)
        {
            var dtSeconds = (frame.TimestampUtc - _taxiInPrevSpeedSampleAt.Value).TotalSeconds;
            if (dtSeconds > 0)
            {
                var decelRate = (_taxiInPrevGroundSpeed - frame.GroundSpeedKnots) / dtSeconds;
                if (decelRate > 3.0)
                    _taxiInSmoothDeceleration = false;
            }
        }
        _taxiInPrevSpeedSampleAt = frame.TimestampUtc;
        _taxiInPrevGroundSpeed = frame.GroundSpeedKnots;
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
            if (frame.ParkingBrakeSet)
            {
                // SOPs require engines off BEFORE setting the parking brake.
                // Penalise if any engine is still running when the brake is applied.
                if (AnyEngineRunning(frame))
                {
                    _arrivalAllEnginesOffBeforeParkingBrakeSet = false;
                }
                _arrivalParkingBrakeObserved = true;
            }
        }

        // Taxi lights: must be turned off after parking brake is set (shutdown checklist).
        // Correct flow: taxi with lights on → set brake → turn lights off → passes.
        // We only update after brake is observed so keeping lights on during taxi never
        // fires a penalty early; the penalty clears the moment lights go off post-brake.
        if (_arrivalParkingBrakeObserved && !frame.TaxiLightsOn)
        {
            _arrivalTaxiLightsOffBeforeParkingBrakeSet = true;
        }

        _arrivalAllEnginesOffByEndOfSession = !AnyEngineRunning(frame);

        // EnginesOffAfterParkingBrake: all engines went off after parking brake was set.
        if (_arrivalParkingBrakeObserved && !AnyEngineRunning(frame))
            _arrivalEnginesOffAfterParkingBrake = true;

        // BeaconOffAfterEngines: beacon turned off after all engines shut down.
        // _sessionEnginesStartedAt is set by UpdateSession in a previous frame so it
        // correctly reflects whether engines had been running earlier in the session.
        if (_sessionEnginesStartedAt is not null && !AnyEngineRunning(frame))
            _arrivalAllEnginesOffSeen = true;
        if (_arrivalAllEnginesOffSeen && !frame.BeaconLightOn)
            _arrivalBeaconOffAfterEngines = true;
    }

    private void UpdateSession(TelemetryFrame frame)
    {
        // Engine start time — first frame where any engine is running.
        if (_sessionEnginesStartedAt is null && AnyEngineRunning(frame))
            _sessionEnginesStartedAt = frame.TimestampUtc;

        // Wheels-off — first liftoff.
        if (_sessionWheelsOffAt is null && _previousFrame is { OnGround: true } && !frame.OnGround)
            _sessionWheelsOffAt = frame.TimestampUtc;

        // Engines-off time — first frame where all engines stop (airborne or on ground).
        if (_sessionEnginesOffAt is null && _sessionEnginesStartedAt is not null && !AnyEngineRunning(frame))
            _sessionEnginesOffAt = frame.TimestampUtc;

        // Departure fuel — snapshot when the aircraft first starts moving forward under own power.
        if (!_sessionFuelDepartureRecorded && _forwardTaxiStarted)
        {
            _sessionFuelAtDepartureLbs = frame.FuelTotalLbs;
            _sessionFuelDepartureRecorded = true;
        }

        // GPS track — one point every 30 seconds (or on phase changes).
        var phaseChanged = _previousFrame is not null && _previousFrame.Phase != frame.Phase;
        var intervalElapsed = (frame.TimestampUtc - _lastGpsTrackPointAt).TotalSeconds >= GpsTrackIntervalSeconds;
        if (intervalElapsed || phaseChanged)
        {
            _gpsTrack.Add(new GpsTrackPoint
            {
                TimestampUtc = frame.TimestampUtc,
                Latitude = frame.Latitude,
                Longitude = frame.Longitude,
                AltitudeFeet = frame.AltitudeFeet,
                GroundSpeedKnots = frame.GroundSpeedKnots,
                Phase = frame.Phase,
            });
            _lastGpsTrackPointAt = frame.TimestampUtc;
        }
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

    private void RecordFlightEvents(TelemetryFrame frame)
    {
        if (_previousFrame is null) return;

        void Emit(string type, int? engineIndex = null) =>
            _flightEvents.Add(new FlightEvent
            {
                Type         = type,
                EngineIndex  = engineIndex,
                Latitude     = frame.Latitude,
                Longitude    = frame.Longitude,
                AltitudeFeet = frame.AltitudeFeet,
                TimestampUtc = frame.TimestampUtc,
            });

        // Engines (4-engine support — index is 1-based)
        if (!_previousFrame.Engine1Running && frame.Engine1Running)  Emit("engine_on",  1);
        if ( _previousFrame.Engine1Running && !frame.Engine1Running) Emit("engine_off", 1);
        if (!_previousFrame.Engine2Running && frame.Engine2Running)  Emit("engine_on",  2);
        if ( _previousFrame.Engine2Running && !frame.Engine2Running) Emit("engine_off", 2);
        if (!_previousFrame.Engine3Running && frame.Engine3Running)  Emit("engine_on",  3);
        if ( _previousFrame.Engine3Running && !frame.Engine3Running) Emit("engine_off", 3);
        if (!_previousFrame.Engine4Running && frame.Engine4Running)  Emit("engine_on",  4);
        if ( _previousFrame.Engine4Running && !frame.Engine4Running) Emit("engine_off", 4);

        // Parking brake
        var prevBrake = _previousFrame.ParkingBrakeSet;
        var curBrake  = frame.ParkingBrakeSet;
        if (!prevBrake && curBrake)  Emit("parking_brake_set");
        if ( prevBrake && !curBrake) Emit("parking_brake_released");

        // Lights
        var prevLanding = _previousFrame.LandingLightsOn;
        var curLanding  = frame.LandingLightsOn;
        if (!prevLanding && curLanding)  Emit("landing_lights_on");
        if ( prevLanding && !curLanding) Emit("landing_lights_off");

        var prevStrobes = _previousFrame.StrobesOn;
        var curStrobes  = frame.StrobesOn;
        if (!prevStrobes && curStrobes)  Emit("strobes_on");
        if ( prevStrobes && !curStrobes) Emit("strobes_off");

        var prevTaxi = _previousFrame.TaxiLightsOn;
        var curTaxi  = frame.TaxiLightsOn;
        if (!prevTaxi && curTaxi)  Emit("taxi_lights_on");
        if ( prevTaxi && !curTaxi) Emit("taxi_lights_off");

        var prevBeacon = _previousFrame.BeaconLightOn;
        var curBeacon  = frame.BeaconLightOn;
        if (!prevBeacon && curBeacon)  Emit("beacon_on");
        if ( prevBeacon && !curBeacon) Emit("beacon_off");
    }

    private void AddRecentSample(TelemetryFrame frame)
    {
        _recentSamples.Enqueue(new RecentSample(frame.TimestampUtc, frame.VerticalSpeedFpm, frame.VelocityWorldYFps, frame.GForce, frame.AltitudeAglFeet));

        while (_recentSamples.Count > 0 && frame.TimestampUtc - _recentSamples.Peek().TimestampUtc > TimeSpan.FromSeconds(2))
        {
            _recentSamples.Dequeue();
        }
    }

    private double CalculateTouchdownVerticalSpeed(TelemetryFrame touchdownFrame)
    {
        // Primary: VELOCITY WORLD Y (physics-engine vertical velocity, no barometric lag,
        // continues updating through WOW transition).  Multiply fps → fpm, preserve sign
        // (negative = descending, matching aviation convention and Volanta/smartCARS).
        // Collect all negative candidates across the touchdown frame, the last airborne
        // frame, and recent sub-30-ft samples; return the Max (least-negative = minimum
        // absolute value) to avoid the single-frame impact spike that can read 2-3× the
        // true sink rate.  Caller stores as-is — webapp takes abs for display.
        var velocityWorldCandidates = new List<double>();

        if (touchdownFrame.VelocityWorldYFps < 0)
            velocityWorldCandidates.Add(touchdownFrame.VelocityWorldYFps * 60.0);

        if (_previousFrame is not null && !_previousFrame.OnGround && _previousFrame.VelocityWorldYFps < 0)
            velocityWorldCandidates.Add(_previousFrame.VelocityWorldYFps * 60.0);

        foreach (var s in _recentSamples)
        {
            if (s.VelocityWorldYFps < 0 && s.AltitudeAglFeet <= 30)
                velocityWorldCandidates.Add(s.VelocityWorldYFps * 60.0);
        }

        if (velocityWorldCandidates.Count > 0)
            return velocityWorldCandidates.Max(); // Max of negatives = least-negative = min abs

        // Fallback: barometric VERTICAL SPEED (legacy — used only when VelocityWorldY
        // is unavailable, e.g. older aircraft profiles that don't expose the SimVar).
        if (touchdownFrame.VerticalSpeedFpm < 0)
            return touchdownFrame.VerticalSpeedFpm;

        if (_previousFrame is not null && !_previousFrame.OnGround && _previousFrame.VerticalSpeedFpm < 0)
            return _previousFrame.VerticalSpeedFpm;

        // Fallback: AGL rate-of-change across the flare window (stable average sink rate).
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
                // AGL decreasing → (first - last) is positive; negate for sign convention.
                var aglSinkFpm = (first.AltitudeAglFeet - last.AltitudeAglFeet) / dtSeconds * 60.0;
                if (aglSinkFpm > 0) return -aglSinkFpm;
            }
        }

        var subHundredSamples = _recentSamples
            .Where(static s => s.AltitudeAglFeet is > 0 and <= 100)
            .ToList();

        // Max of negative baro VS values = least-negative = min abs value.
        return subHundredSamples.Count > 0
            ? subHundredSamples.Max(static s => s.VerticalSpeedFpm)
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

    private void UpdateLightsSystems(TelemetryFrame frame)
    {
        if (_postRestoreGraceFrames > 0)
            return;

        var anyEngineOn = AnyEngineRunning(frame);

        // Beacon must be on whenever any engine is running.
        if (anyEngineOn && !frame.BeaconLightOn)
            _lightsBeaconOnThroughout = false;

        // Nav lights proxy (TaxiLightsOn) — must be on when any engine is running.
        if (anyEngineOn && !frame.TaxiLightsOn)
            _lightsNavOnThroughout = false;

        // Strobe window: open when airborne during flight phases.
        var airborneFlightPhase = frame.Phase is FlightPhase.Takeoff or FlightPhase.Climb or
            FlightPhase.Cruise or FlightPhase.Descent or FlightPhase.Approach or FlightPhase.Landing
            && !frame.OnGround;
        _lightsStrobeWindowOpen = airborneFlightPhase;

        if (_lightsStrobeWindowOpen && !frame.StrobesOn)
            _lightsStrobesCorrect = false;
        if (!_lightsStrobeWindowOpen && frame.StrobesOn &&
            frame.Phase is FlightPhase.TaxiOut or FlightPhase.TaxiIn or FlightPhase.Arrival)
            _lightsStrobesCorrect = false;

        // Landing lights compliance — sample every 30 s.
        // Correct: lights on when below FL180, lights off when at or above FL180.
        if (_lightsLastComplianceSampleAt is null ||
            (frame.TimestampUtc - _lightsLastComplianceSampleAt.Value).TotalSeconds >= 30)
        {
            if (frame.Phase is FlightPhase.Climb or FlightPhase.Cruise or
                FlightPhase.Descent or FlightPhase.Approach)
            {
                _lightsLandingTotalSamples++;
                var belowFl180 = frame.IndicatedAltitudeFeet < 18000;
                if ((belowFl180 && frame.LandingLightsOn) || (!belowFl180 && !frame.LandingLightsOn))
                    _lightsLandingCompliantSamples++;
                _lightsLastComplianceSampleAt = frame.TimestampUtc;
            }
        }
    }

    private sealed record RecentSample(DateTimeOffset TimestampUtc, double VerticalSpeedFpm, double VelocityWorldYFps, double GForce, double AltitudeAglFeet);
}
