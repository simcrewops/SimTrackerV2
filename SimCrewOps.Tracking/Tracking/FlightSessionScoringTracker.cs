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
    private double _taxiOutMaxTurnSpeed;
    private bool _taxiOutNavLightsValid = true;
    private bool _taxiOutStrobesOff = true;
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
    private int _takeoffFlapsAtLiftoff;
    private double _takeoffInitialClimbFpm;
    private bool _takeoffPositiveRateBeforeGearUp;
    private double _takeoffMaxPitchWhileWow;
    private double _takeoffMaxPitchWhileWowAglFt;
    private double _takeoffGForceAtRotation;

    private double _climbMaxIasBelowFl100;
    private double _climbMaxBank;
    private double _climbMaxG;
    private double _climbMinG = double.MaxValue;
    private double _climbVsSum;
    private int _climbVsCount;
    private int _climbStableFrames;
    private DateTimeOffset? _climbFL100CrossingAt;

    private double _cruiseMaxAltitudeDeviation;
    private double _cruiseLevelMaxBank;
    private double _cruiseTurnMaxBank;
    private double _cruiseMaxG;
    private double _cruiseMinG = double.MaxValue;
    private double _cruiseMaxMachDev;
    private int _cruiseSpeedInstabilityEvents;
    private double _cruiseMaxSpeedDeviationKts;
    private DateTimeOffset? _lastCruiseSpeedInstabilityAt;
    private DateTimeOffset? _cruiseSpeedInstabilityStartedAt;
    private bool _cruiseSpeedInstabilityActive;
    private double? _cruiseReferenceIasKnots;
    private double? _cruiseReferenceMach;
    private double? _cruiseTargetAltitudeFeet;
    private double? _newFlightLevelCaptureSeconds;
    // Cruise altitude auto-detect: 60s stable level-off detection (airborne, any phase).
    private DateTimeOffset? _levelOffStartUtc;
    private double? _levelOffAltFt;

    private bool _descentSeen;
    private double _descentMaxIasBelowFl100;
    private double _descentMaxBank;
    private double _descentMaxPitch;
    private double _descentMaxG;
    private double _descentMinG = double.MaxValue;
    private double _descentMaxRate;
    private double _descentMaxNoseDown;
    private bool _descentLandingLightsOnByFl180 = true;
    private double _descentVsSum;
    private int _descentVsCount;
    private double? _descentSpeedAtFL100Kts;

    private bool _capturedApproach1000Agl;
    private bool _gearDownAtGate;     // gear state (down = true) at the 1000 AGL gate crossing
    private double? _gearDownAglFt;   // AGL at which gear transitioned UP→DOWN during approach
    private double _approachSpeed1000Agl;
    private bool _flapsConfiguredBy1000Agl;
    private double _approachMaxIasDev;
    private double _approachMaxBank;
    private bool _capturedApproach500Agl;
    private int _approachFlapsAt500Agl;
    private double _approachVsAt500Agl;
    private double _approachBankAt500Agl;
    private double _approachPitchAt500Agl;
    private bool _approachGearDownAt500Agl;
    // Stabilized approach (<500 AGL window)
    private bool _inStabApproach;
    private double _stabApproachSpeed500Agl;
    private double _stabMaxIasDev;
    private double _stabMaxDescentRate;
    private bool _stabConfigChanged;
    private bool _stabGearAt500;
    private int _stabFlapsAt500;
    private double _stabRunwayHeadingRef;
    private double _stabMaxHdgDev;
    private bool _stabIlsAvailable;   // remains false until SimConnect ILS SimVars are wired
    private double _stabMaxGsDev;     // remains 0 until ILS wired
    private double _stabPitchAtGate;
    // Per-frame gear state tracking for gear-transition detection
    private bool _prevGearDown;

    private int _landingBounceCount;
    private double _landingTouchdownZoneExcessDistanceFeet;
    private double _landingTouchdownVerticalSpeedFpm;
    private double _landingTouchdownBankAngleDegrees;
    private double _landingTouchdownIndicatedAirspeedKnots;
    private double _landingTouchdownPitchAngleDegrees;
    private double _landingTouchdownGForce;
    private double _landingMaxPitchWhileWowDegrees;
    private double? _landingTouchdownLat;
    private double? _landingTouchdownLon;
    private double? _landingTouchdownHeadingMagneticDeg;
    private double? _landingTouchdownAltFt;
    private double? _landingTouchdownWindSpeedKnots;
    private double? _landingTouchdownWindDirectionDegrees;
    private bool _landingGearUpAtTouchdown;
    private bool _capturedFirstTouchdown;
    private DateTimeOffset? _lastTouchdownAt;
    private DateTimeOffset? _airborneAfterTouchdownAt;

    // A/B instrumentation: touchdown FPM candidates latched at first touchdown
    private double _abFpmVelocityWorldY;
    private double _abFpmTouchdownNormal;
    private double _abFpmVerticalSpeed;
    private double _abFinalSelected;
    private double _abFpmVelocityWorldYLastAirborne;
    private double _abFpmVerticalSpeedLastAirborne;
    private string _abSelectedSourceLabel = string.Empty;
    private double _abRawTouchdownNormalFpsTdFrame;
    private double? _abRawTouchdownNormalFpsFirstNonZero;
    private bool _abTouchdownNormalSearchActive;
    private DateTimeOffset? _abTouchdownNormalSearchExpiry;

    // Debounce: first-touchdown tentative state.
    // SIM_ON_GROUND can flicker true during ground-effect/flare before actual wheel contact.
    // We require ≥500ms of sustained ground before committing the first touchdown.
    // Known limitation: a genuine hard landing that bounces back airborne within 500ms will
    // be misclassified as a flicker (TD recorded at the softer second contact). Real bounces
    // occur >1s after touchdown in practice; a future AGL discriminator could resolve it.
    private TelemetryFrame? _tentativeTouchdownFrame;
    private TelemetryFrame? _tentativeTouchdownPreviousFrame;
    private double _tentativeTdFpm;
    private string _tentativeTdLabel = string.Empty;

    private DateTimeOffset? _sessionStartUtc;
    private DateTimeOffset? _lastFlightPathSampleAt;
    private double? _lastFlightPathAltFt;
    private readonly List<FlightPathPoint> _flightPath = [];
    private readonly List<ApproachPathPoint> _approachPath = [];

    private bool _taxiInSeen;
    private bool _taxiInLandingLightsOff = true;
    private bool _taxiInStrobesOff = true;
    private bool _taxiInNavLightsValid = true;
    private bool _taxiInTaxiLightsValid = true;
    private DateTimeOffset? _taxiInLightsOffStart;
    private DateTimeOffset? _taxiInLandingLightsOnStart;   // 60 s sustained on → penalty (1 min after vacate)
    private DateTimeOffset? _taxiInStrobesOnStart;          // 60 s sustained on → penalty
    private double _taxiInMaxGroundSpeed;
    private double _taxiInMaxTurnSpeed;
    private bool _taxiInSmoothDecel = true;
    private double? _taxiInPrevGroundSpeed;
    private int _taxiInTurnSpeedEvents;
    private DateTimeOffset? _lastTaxiInTurnEventAt;

    private bool _arrivalSeen;
    private bool _arrivalParkingBrakeObserved;
    private bool _arrivalTaxiLightsOffBeforeParkingBrakeSet;
    private bool _taxiLightsWentOffBeforeBrake;
    private bool _arrivalParkingBrakeSetBeforeAllEnginesShutdown = true;
    private bool _arrivalAllEnginesOffByEndOfSession;
    private bool _arrivalBeaconOffAfterEngines;
    private bool _arrivalEnginesOffObserved;
    // LightsSystems accumulators
    private bool _beaconOnAirborneThroughout = true;
    private bool _navLightsOnThroughout = true;
    private bool _strobesCorrect = true;
    private bool _strobesShouldBeOn;
    private int _landingLightsCompliantTicks;
    private int _landingLightsTotalTicks;

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
        _taxiOutMaxTurnSpeed = input.TaxiOut.MaxTurnSpeedKnots;
        _taxiOutNavLightsValid = true;
        _taxiOutStrobesOff = !_taxiOutSeen || input.TaxiOut.StrobesOff;
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
        _takeoffFlapsAtLiftoff = input.Takeoff.FlapsHandleIndexAtLiftoff;
        _takeoffInitialClimbFpm = input.Takeoff.InitialClimbFpm;
        _takeoffPositiveRateBeforeGearUp = input.Takeoff.PositiveRateBeforeGearUp;
        _takeoffMaxPitchWhileWow = input.Takeoff.MaxPitchWhileWowDegrees;
        _takeoffMaxPitchWhileWowAglFt = input.Takeoff.MaxPitchAglFt;
        _takeoffGForceAtRotation = input.Takeoff.GForceAtRotation;

        _climbMaxIasBelowFl100 = input.Climb.MaxIasBelowFl100Knots;
        _climbMaxBank = input.Climb.MaxBankAngleDegrees;
        _climbMaxG = input.Climb.MaxGForce;
        _climbMinG = HasReachedPhase(currentPhase, FlightPhase.Climb) ? input.Climb.MinGForce : double.MaxValue;
        // Restore avg climb as a single synthetic sample; exact accumulators can't be persisted.
        _climbVsSum = input.Climb.AvgClimbFpm;
        _climbVsCount = input.Climb.AvgClimbFpm != 0 ? 1 : 0;
        _climbStableFrames = input.Climb.VsStabilityScore > 0 ? 1 : 0;
        _climbFL100CrossingAt = null;

        _cruiseMaxAltitudeDeviation = input.Cruise.MaxAltitudeDeviationFeet;
        _cruiseLevelMaxBank = input.Cruise.LevelMaxBankDegrees;
        _cruiseTurnMaxBank = input.Cruise.TurnMaxBankDegrees;
        _cruiseMaxG = input.Cruise.MaxGForce;
        _cruiseMinG = HasReachedPhase(currentPhase, FlightPhase.Cruise) ? input.Cruise.MinGForce : double.MaxValue;
        _cruiseMaxMachDev = input.Cruise.MaxMachDeviation;
        _cruiseSpeedInstabilityEvents = input.Cruise.SpeedInstabilityEvents;
        _cruiseMaxSpeedDeviationKts = input.Cruise.MaxSpeedDeviationKts;
        _lastCruiseSpeedInstabilityAt = null;
        _cruiseSpeedInstabilityStartedAt = null;
        _cruiseSpeedInstabilityActive = false;
        _newFlightLevelCaptureSeconds = input.Cruise.NewFlightLevelCaptureSeconds;
        _levelOffStartUtc = null;
        _levelOffAltFt = null;

        if (HasReachedPhase(currentPhase, FlightPhase.Cruise) && lastTelemetryFrame is not null)
        {
            _cruiseReferenceIasKnots = lastTelemetryFrame.IndicatedAirspeedKnots;
            _cruiseReferenceMach = lastTelemetryFrame.Mach;
            _cruiseTargetAltitudeFeet = input.Cruise.CruiseTargetAltitudeFt
                ?? Math.Round(lastTelemetryFrame.IndicatedAltitudeFeet / 100.0) * 100.0;
        }
        else
        {
            _cruiseReferenceIasKnots = null;
            _cruiseReferenceMach = null;
            _cruiseTargetAltitudeFeet = input.Cruise.CruiseTargetAltitudeFt;
        }

        _descentSeen = HasReachedPhase(currentPhase, FlightPhase.Descent);
        _descentMaxIasBelowFl100 = input.Descent.MaxIasBelowFl100Knots;
        _descentMaxBank = input.Descent.MaxBankAngleDegrees;
        _descentMaxPitch = input.Descent.MaxPitchAngleDegrees;
        _descentMaxG = input.Descent.MaxGForce;
        _descentMinG = HasReachedPhase(currentPhase, FlightPhase.Descent) ? input.Descent.MinGForce : double.MaxValue;
        _descentMaxRate = input.Descent.MaxDescentRateFpm;
        _descentMaxNoseDown = input.Descent.MaxNoseDownPitchDeg;
        _descentLandingLightsOnByFl180 = !_descentSeen || input.Descent.LandingLightsOnByFl180;
        // Restore avg descent as a single synthetic sample.
        _descentVsSum = input.Descent.AvgDescentFpm;
        _descentVsCount = input.Descent.AvgDescentFpm != 0 ? 1 : 0;
        _descentSpeedAtFL100Kts = input.Descent.SpeedAtFL100Kts;

        _approachSpeed1000Agl = input.Approach.ApproachSpeedKts;
        _capturedApproach1000Agl = input.Approach.ApproachSpeedKts > 0 || HasReachedPhase(currentPhase, FlightPhase.Landing);
        _gearDownAtGate = input.Approach.GearDownBy1000Agl;
        _gearDownAglFt = input.Approach.GearDownAglFt;
        _flapsConfiguredBy1000Agl = input.Approach.FlapsConfiguredBy1000Agl;
        _approachMaxIasDev = input.Approach.MaxIasDeviationKts;
        _approachMaxBank = input.Approach.MaxBankDegrees;
        _capturedApproach500Agl = input.Approach.GearDownAt500Agl || input.Approach.FlapsHandleIndexAt500Agl > 0;
        _approachFlapsAt500Agl = input.Approach.FlapsHandleIndexAt500Agl;
        _approachVsAt500Agl = input.Approach.VerticalSpeedAt500AglFpm;
        _approachBankAt500Agl = input.Approach.BankAngleAt500AglDegrees;
        _approachPitchAt500Agl = input.Approach.PitchAngleAt500AglDegrees;
        _approachGearDownAt500Agl = input.Approach.GearDownAt500Agl;

        // Stab approach — restore from persisted metrics; _inStabApproach always starts false on restore.
        _inStabApproach = false;
        _stabApproachSpeed500Agl = input.StabilizedApproach.ApproachSpeedKts;
        _stabMaxIasDev = input.StabilizedApproach.MaxIasDeviationKts;
        _stabMaxDescentRate = input.StabilizedApproach.MaxDescentRateFpm;
        _stabConfigChanged = input.StabilizedApproach.ConfigChanged;
        _stabMaxHdgDev = input.StabilizedApproach.MaxHeadingDeviationDeg;
        _stabIlsAvailable = input.StabilizedApproach.IlsAvailable;
        _stabMaxGsDev = input.StabilizedApproach.MaxGlideslopeDevDots;
        _stabPitchAtGate = input.StabilizedApproach.PitchAtGateDeg;

        _landingBounceCount = input.Landing.BounceCount;
        _landingTouchdownZoneExcessDistanceFeet = input.Landing.TouchdownZoneExcessDistanceFeet;
        _landingTouchdownVerticalSpeedFpm = input.Landing.TouchdownVerticalSpeedFpm;
        _landingTouchdownBankAngleDegrees = input.Landing.TouchdownBankAngleDegrees;
        _landingTouchdownIndicatedAirspeedKnots = input.Landing.TouchdownIndicatedAirspeedKnots;
        _landingTouchdownPitchAngleDegrees = input.Landing.TouchdownPitchAngleDegrees;
        _landingTouchdownGForce = input.Landing.TouchdownGForce;
        _lastTouchdownAt = wheelsOnUtc;
        _airborneAfterTouchdownAt = null;
        _landingMaxPitchWhileWowDegrees = input.Landing.MaxPitchWhileWowDegrees;
        _landingGearUpAtTouchdown = input.Landing.GearUpAtTouchdown;
        _capturedFirstTouchdown = input.LandingAnalysis.TouchdownLat.HasValue;
        _landingTouchdownLat = input.LandingAnalysis.TouchdownLat;
        _landingTouchdownLon = input.LandingAnalysis.TouchdownLon;
        _landingTouchdownHeadingMagneticDeg = input.LandingAnalysis.TouchdownHeadingMagneticDeg;
        _landingTouchdownAltFt = input.LandingAnalysis.TouchdownAltFt;
        _landingTouchdownWindSpeedKnots = input.LandingAnalysis.WindSpeedKnots;
        _landingTouchdownWindDirectionDegrees = input.LandingAnalysis.WindDirectionDegrees;

        _abFpmVelocityWorldY = input.TouchdownRateCandidates?.FpmVelocityWorldY ?? 0;
        _abFpmTouchdownNormal = input.TouchdownRateCandidates?.FpmTouchdownNormal ?? 0;
        _abFpmVerticalSpeed   = input.TouchdownRateCandidates?.FpmVerticalSpeed ?? 0;
        _abFinalSelected      = input.TouchdownRateCandidates?.FinalSelected ?? 0;
        _abFpmVelocityWorldYLastAirborne     = input.TouchdownRateCandidates?.FpmVelocityWorldYLastAirborne ?? 0;
        _abFpmVerticalSpeedLastAirborne      = input.TouchdownRateCandidates?.FpmVerticalSpeedLastAirborne ?? 0;
        _abSelectedSourceLabel               = input.TouchdownRateCandidates?.SelectedSourceLabel ?? string.Empty;
        _abRawTouchdownNormalFpsTdFrame      = input.TouchdownRateCandidates?.RawTouchdownNormalVelocityFpsTouchdownFrame ?? 0;
        _abRawTouchdownNormalFpsFirstNonZero = input.TouchdownRateCandidates?.RawTouchdownNormalVelocityFpsFirstNonZero;

        _flightPath.Clear();
        _flightPath.AddRange(input.FlightPath);
        _approachPath.Clear();
        _approachPath.AddRange(input.ApproachPath);

        _taxiInSeen = HasReachedPhase(currentPhase, FlightPhase.TaxiIn);
        _taxiInLandingLightsOff = !_taxiInSeen || input.TaxiIn.LandingLightsOff;
        _taxiInStrobesOff = !_taxiInSeen || input.TaxiIn.StrobesOff;
        _taxiInTaxiLightsValid = !_taxiInSeen || input.TaxiIn.TaxiLightsOn;
        _taxiInNavLightsValid = true;
        _taxiInLightsOffStart = null;
        _taxiInLandingLightsOnStart = null;
        _taxiInStrobesOnStart = null;
        _taxiInMaxGroundSpeed = input.TaxiIn.MaxGroundSpeedKnots;
        _taxiInMaxTurnSpeed = input.TaxiIn.MaxTurnSpeedKnots;
        _taxiInSmoothDecel = !_taxiInSeen || input.TaxiIn.SmoothDeceleration;
        _taxiInPrevGroundSpeed = null;
        _taxiInTurnSpeedEvents = input.TaxiIn.ExcessiveTurnSpeedEvents;
        _lastTaxiInTurnEventAt = null;

        _arrivalSeen = currentPhase == FlightPhase.Arrival;
        _arrivalParkingBrakeObserved = currentPhase == FlightPhase.Arrival;
        _arrivalTaxiLightsOffBeforeParkingBrakeSet = input.Arrival.TaxiLightsOffBeforeParkingBrakeSet;
        _arrivalParkingBrakeSetBeforeAllEnginesShutdown =
            !_arrivalSeen || input.Arrival.ParkingBrakeSetBeforeAllEnginesShutdown;
        _arrivalAllEnginesOffByEndOfSession = input.Arrival.AllEnginesOffByEndOfSession;
        _arrivalBeaconOffAfterEngines = input.Arrival.BeaconOffAfterEngines;
        _arrivalEnginesOffObserved = false;

        _beaconOnAirborneThroughout = input.LightsSystems.BeaconOnThroughoutFlight;
        _navLightsOnThroughout = input.LightsSystems.NavLightsOnThroughoutFlight;
        _strobesCorrect = input.LightsSystems.StrobesCorrect;
        // Tick counts can't be persisted exactly — reset to 0 and re-accumulate from reconnect.
        _landingLightsCompliantTicks = 0;
        _landingLightsTotalTicks = 0;

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

        _sessionStartUtc ??= frame.TimestampUtc;

        AddRecentSample(frame);
        UpdateFlightPath(frame);
        UpdateApproachPath(frame);
        UpdatePreflight(frame);
        UpdateTaxiOut(frame);
        UpdateTakeoff(frame);
        UpdateClimb(frame);
        UpdateLevelOff(frame);
        UpdateCruise(frame);
        UpdateDescent(frame);
        UpdateApproach(frame);
        UpdateStabilizedApproach(frame);
        UpdateLanding(frame);
        UpdateMaxPitchWhileWow(frame);
        UpdateTaxiIn(frame);
        UpdateArrivalLifecycle(frame);
        UpdateArrival(frame);
        UpdateLightsSystems(frame);
        UpdateSafety(frame);

        _previousFrame = frame;
        _prevGearDown = frame.GearDown;
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
                MaxGroundSpeedKnots      = _taxiOutMaxGroundSpeed,
                MaxTurnSpeedKnots        = _taxiOutMaxTurnSpeed,
                ExcessiveTurnSpeedEvents = _taxiOutTurnSpeedEvents,
                // Pass if the phase hasn't been seen yet — only penalise once we enter it.
                TaxiLightsOn = !_taxiOutSeen || _taxiOutTaxiLightsValid,
                NavLightsOn  = !_taxiOutSeen || _taxiOutNavLightsValid,
                StrobesOff   = !_taxiOutSeen || _taxiOutStrobesOff,
            },
            Takeoff = new TakeoffMetrics
            {
                BounceCount              = _takeoffBounceCount,
                TailStrikeDetected       = _takeoffTailStrikeDetected,
                MaxBankAngleDegrees      = _takeoffMaxBank,
                MaxPitchAngleDegrees     = _takeoffMaxPitch,
                MaxGForce                = _takeoffMaxG,
                MaxPitchWhileWowDegrees  = _takeoffMaxPitchWhileWow,
                MaxPitchAglFt            = _takeoffMaxPitchWhileWowAglFt,
                GForceAtRotation         = _takeoffGForceAtRotation,
                PositiveRateBeforeGearUp = _takeoffPositiveRateBeforeGearUp,
                // Pass if the phase hasn't been seen yet — only penalise once we enter it.
                LandingLightsOnBeforeTakeoff  = !_takeoffSeen || _takeoffLandingLightsOnBeforeTakeoff,
                LandingLightsOffByFl180       = !_takeoffSeen || _takeoffLandingLightsOffByFl180,
                StrobesOnFromTakeoffToLanding = !_takeoffSeen || _takeoffStrobesOnFromTakeoffToLanding,
                FlapsHandleIndexAtLiftoff     = _takeoffFlapsAtLiftoff,
                InitialClimbFpm              = _takeoffInitialClimbFpm,
            },
            Climb = new ClimbMetrics
            {
                HeavyFourEngineAircraft = _profile.HeavyFourEngineAircraft,
                MaxIasBelowFl100Knots   = _climbMaxIasBelowFl100,
                MaxBankAngleDegrees     = _climbMaxBank,
                MaxGForce               = _climbMaxG,
                MinGForce               = _climbMinG < double.MaxValue ? _climbMinG : 1.0,
                AvgClimbFpm             = _climbVsCount > 0 ? _climbVsSum / _climbVsCount : 0,
                TimeToFL100Minutes      = (_climbFL100CrossingAt.HasValue && _lastTakeoffLiftoffAt.HasValue)
                    ? (_climbFL100CrossingAt.Value - _lastTakeoffLiftoffAt.Value).TotalMinutes
                    : null,
                VsStabilityScore = _climbVsCount > 0 ? (double)_climbStableFrames / _climbVsCount : 0,
            },
            Cruise = new CruiseMetrics
            {
                CruiseTargetAltitudeFt       = _cruiseTargetAltitudeFeet,
                MaxAltitudeDeviationFeet      = _cruiseMaxAltitudeDeviation,
                NewFlightLevelCaptureSeconds  = _newFlightLevelCaptureSeconds,
                MachTarget                   = _cruiseReferenceMach,
                MaxMachDeviation             = _cruiseMaxMachDev,
                IasTarget                    = _cruiseReferenceIasKnots,
                MaxSpeedDeviationKts         = _cruiseMaxSpeedDeviationKts,
                SpeedInstabilityEvents       = _cruiseSpeedInstabilityEvents,
                LevelMaxBankDegrees          = _cruiseLevelMaxBank,
                TurnMaxBankDegrees           = _cruiseTurnMaxBank,
                MaxGForce                    = _cruiseMaxG,
                MinGForce                    = _cruiseMinG < double.MaxValue ? _cruiseMinG : 1.0,
            },
            Descent = new DescentMetrics
            {
                MaxIasBelowFl100Knots = _descentMaxIasBelowFl100,
                MaxBankAngleDegrees   = _descentMaxBank,
                MaxPitchAngleDegrees  = _descentMaxPitch,
                MaxGForce             = _descentMaxG,
                MinGForce             = _descentMinG < double.MaxValue ? _descentMinG : 1.0,
                MaxDescentRateFpm     = _descentMaxRate,
                MaxNoseDownPitchDeg   = _descentMaxNoseDown,
                // Pass if descent hasn't been seen yet — only penalise once we enter it.
                LandingLightsOnByFl180 = !_descentSeen || _descentLandingLightsOnByFl180,
                AvgDescentFpm          = _descentVsCount > 0 ? _descentVsSum / _descentVsCount : 0,
                SpeedAtFL100Kts        = _descentSpeedAtFL100Kts,
            },
            Approach = new ApproachMetrics
            {
                // Pass gear/flaps checks if the capture snapshot hasn't fired yet —
                // only penalise once the aircraft actually passes through those altitudes.
                GearDownBy1000Agl        = !_capturedApproach1000Agl || _gearDownAtGate,
                GearDownAglFt            = _gearDownAglFt,
                ApproachSpeedKts         = _approachSpeed1000Agl,
                MaxIasDeviationKts       = _approachMaxIasDev,
                FlapsConfiguredBy1000Agl = _capturedApproach1000Agl && _flapsConfiguredBy1000Agl,
                MaxBankDegrees           = _approachMaxBank,
                // Default flaps to 2 (passing) until the 500ft snapshot is captured.
                FlapsHandleIndexAt500Agl  = _capturedApproach500Agl ? _approachFlapsAt500Agl : 2,
                VerticalSpeedAt500AglFpm  = _approachVsAt500Agl,
                BankAngleAt500AglDegrees  = _approachBankAt500Agl,
                PitchAngleAt500AglDegrees = _approachPitchAt500Agl,
                GearDownAt500Agl          = !_capturedApproach500Agl || _approachGearDownAt500Agl,
            },
            StabilizedApproach = new StabilizedApproachMetrics
            {
                ApproachSpeedKts      = _stabApproachSpeed500Agl,
                MaxIasDeviationKts    = _stabMaxIasDev,
                MaxDescentRateFpm     = _stabMaxDescentRate,
                ConfigChanged         = _stabConfigChanged,
                MaxHeadingDeviationDeg = _stabMaxHdgDev,
                IlsAvailable          = _stabIlsAvailable,
                MaxGlideslopeDevDots  = _stabMaxGsDev,
                PitchAtGateDeg        = _stabPitchAtGate,
            },
            Landing = new LandingMetrics
            {
                TouchdownZoneExcessDistanceFeet = _landingTouchdownZoneExcessDistanceFeet,
                TouchdownVerticalSpeedFpm       = _landingTouchdownVerticalSpeedFpm,
                TouchdownBankAngleDegrees       = _landingTouchdownBankAngleDegrees,
                TouchdownIndicatedAirspeedKnots = _landingTouchdownIndicatedAirspeedKnots,
                TouchdownPitchAngleDegrees      = _landingTouchdownPitchAngleDegrees,
                MaxPitchWhileWowDegrees         = _landingMaxPitchWhileWowDegrees,
                TouchdownGForce                 = _landingTouchdownGForce,
                BounceCount                     = _landingBounceCount,
                GearUpAtTouchdown               = _landingGearUpAtTouchdown,
            },
            LandingAnalysis = new LandingAnalysisData
            {
                TouchdownLat                = _landingTouchdownLat,
                TouchdownLon                = _landingTouchdownLon,
                TouchdownHeadingMagneticDeg = _landingTouchdownHeadingMagneticDeg,
                TouchdownAltFt              = _landingTouchdownAltFt,
                TouchdownIAS                = _capturedFirstTouchdown ? _landingTouchdownIndicatedAirspeedKnots : null,
                WindSpeedKnots              = _landingTouchdownWindSpeedKnots,
                WindDirectionDegrees        = _landingTouchdownWindDirectionDegrees,
            },
            FlightPath   = _flightPath.ToArray(),
            ApproachPath = _approachPath.ToArray(),
            TouchdownRateCandidates = _capturedFirstTouchdown
                ? new TouchdownRateCandidates
                {
                    FpmVelocityWorldY  = _abFpmVelocityWorldY,
                    FpmTouchdownNormal = _abFpmTouchdownNormal,
                    FpmVerticalSpeed   = _abFpmVerticalSpeed,
                    FinalSelected      = _abFinalSelected,
                    FpmVelocityWorldYLastAirborne               = _abFpmVelocityWorldYLastAirborne,
                    FpmVerticalSpeedLastAirborne                = _abFpmVerticalSpeedLastAirborne,
                    SelectedSourceLabel                         = _abSelectedSourceLabel,
                    RawTouchdownNormalVelocityFpsTouchdownFrame = _abRawTouchdownNormalFpsTdFrame,
                    RawTouchdownNormalVelocityFpsFirstNonZero   = _abRawTouchdownNormalFpsFirstNonZero,
                }
                : null,
            TaxiIn = new TaxiInMetrics
            {
                // Pass if the phase hasn't been seen yet — only penalise once we enter it.
                LandingLightsOff     = !_taxiInSeen || _taxiInLandingLightsOff,
                StrobesOff           = !_taxiInSeen || _taxiInStrobesOff,
                MaxGroundSpeedKnots  = _taxiInMaxGroundSpeed,
                MaxTurnSpeedKnots    = _taxiInMaxTurnSpeed,
                ExcessiveTurnSpeedEvents = _taxiInTurnSpeedEvents,
                TaxiLightsOn         = !_taxiInSeen || _taxiInTaxiLightsValid,
                NavLightsOn          = !_taxiInSeen || _taxiInNavLightsValid,
                SmoothDeceleration   = !_taxiInSeen || _taxiInSmoothDecel,
            },
            Arrival = new ArrivalMetrics
            {
                ArrivalReached = _arrivalSeen,
                // Pass if the phase hasn't been seen yet — only penalise once we enter it.
                TaxiLightsOffBeforeParkingBrakeSet      = !_arrivalSeen || (_arrivalParkingBrakeObserved && _arrivalTaxiLightsOffBeforeParkingBrakeSet),
                ParkingBrakeSetBeforeAllEnginesShutdown = !_arrivalSeen || (_arrivalParkingBrakeObserved && _arrivalParkingBrakeSetBeforeAllEnginesShutdown),
                AllEnginesOffByEndOfSession             = !_arrivalSeen || _arrivalAllEnginesOffByEndOfSession,
                BeaconOffAfterEngines                   = _arrivalBeaconOffAfterEngines,
            },
            LightsSystems = new LightsSystemsMetrics
            {
                BeaconOnThroughoutFlight    = _beaconOnAirborneThroughout,
                NavLightsOnThroughoutFlight = _navLightsOnThroughout,
                StrobesCorrect              = _strobesCorrect,
                LandingLightsCompliance     = _landingLightsTotalTicks > 0
                    ? (double)_landingLightsCompliantTicks / _landingLightsTotalTicks
                    : 1.0,
            },
            Safety = new SafetyMetrics
            {
                CrashDetected           = _crashDetected,
                OverspeedEvents         = _overspeedEvents,
                SustainedOverspeedEvents = _sustainedOverspeedEvents,
                StallEvents             = _stallEvents,
                GpwsEvents              = _gpwsEvents,
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

        // Enforce taxi lights, nav lights, and strobe checks once forward taxi is underway.
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

            // Strobes must be OFF during taxi-out (before takeoff roll).
            if (frame.StrobesOn)
                _taxiOutStrobesOff = false;

            // Turn speed: max GS during high-heading-rate ticks.
            if (_previousFrame is not null && _previousFrame.Phase == FlightPhase.TaxiOut
                && HeadingRateDegPerSec(frame, _previousFrame) >= 5.0)
            {
                _taxiOutMaxTurnSpeed = Math.Max(_taxiOutMaxTurnSpeed, frame.GroundSpeedKnots);
            }
        }

        CountTurnSpeedEvent(frame, ref _taxiOutTurnSpeedEvents, ref _lastTaxiOutTurnEventAt);
    }

    private void UpdateTakeoff(TelemetryFrame frame)
    {
        if (frame.Phase == FlightPhase.Takeoff)
        {
            _takeoffSeen = true;
            // MaxBankAngleDegrees tracks bank below 1000 AGL (takeoff roll/rotation only).
            if (frame.AltitudeAglFeet < 1000)
                _takeoffMaxBank = Math.Max(_takeoffMaxBank, Math.Abs(frame.BankAngleDegrees));
            _takeoffMaxPitch = Math.Max(_takeoffMaxPitch, Math.Abs(frame.PitchAngleDegrees));
            _takeoffMaxG = Math.Max(_takeoffMaxG, frame.GForce);

            // Track max pitch while WOW=true for tail-strike detection at rotation.
            if (frame.OnGround && frame.PitchAngleDegrees > _takeoffMaxPitchWhileWow)
            {
                _takeoffMaxPitchWhileWow = frame.PitchAngleDegrees;
                _takeoffMaxPitchWhileWowAglFt = frame.AltitudeAglFeet;
            }

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
            _takeoffFlapsAtLiftoff = frame.FlapsHandleIndex;
            // Capture G at the exact WOW→airborne transition (rotation G).
            _takeoffGForceAtRotation = frame.GForce;
        }

        // Positive rate before gear up: check at gear-down→gear-up transition while airborne.
        if (!_takeoffPositiveRateBeforeGearUp
            && _prevGearDown && !frame.GearDown && !frame.OnGround
            && frame.Phase is FlightPhase.Takeoff or FlightPhase.Climb)
        {
            _takeoffPositiveRateBeforeGearUp = frame.VerticalSpeedFpm > 0;
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

        if (_previousFrame is null || _previousFrame.Phase != FlightPhase.Climb)
        {
            _takeoffInitialClimbFpm = frame.VerticalSpeedFpm;
        }

        if (frame.IndicatedAltitudeFeet < 10000)
        {
            _climbMaxIasBelowFl100 = Math.Max(_climbMaxIasBelowFl100, frame.IndicatedAirspeedKnots);
        }

        if (_previousFrame is not null &&
            _previousFrame.IndicatedAltitudeFeet < 10000 &&
            frame.IndicatedAltitudeFeet >= 10000 &&
            _climbFL100CrossingAt is null)
        {
            _climbFL100CrossingAt = frame.TimestampUtc;
        }

        _climbVsSum += frame.VerticalSpeedFpm;
        _climbVsCount++;
        if (frame.VerticalSpeedFpm >= 300)
            _climbStableFrames++;

        _climbMaxBank = Math.Max(_climbMaxBank, Math.Abs(frame.BankAngleDegrees));
        _climbMaxG = Math.Max(_climbMaxG, frame.GForce);
        _climbMinG = Math.Min(_climbMinG, frame.GForce);
    }

    private void UpdateLevelOff(TelemetryFrame frame)
    {
        // Detect 60-second stable level-off (|VS| < 200 fpm, altitude drift < 100 ft) on any
        // airborne tick regardless of phase. Sets _cruiseTargetAltitudeFeet when stable.
        if (frame.OnGround)
        {
            _levelOffStartUtc = null;
            _levelOffAltFt = null;
            return;
        }

        if (Math.Abs(frame.VerticalSpeedFpm) < 200)
        {
            if (_levelOffStartUtc is null ||
                (_levelOffAltFt.HasValue && Math.Abs(frame.IndicatedAltitudeFeet - _levelOffAltFt.Value) > 100))
            {
                _levelOffStartUtc = frame.TimestampUtc;
                _levelOffAltFt = frame.IndicatedAltitudeFeet;
            }
            else if (frame.TimestampUtc - _levelOffStartUtc.Value >= TimeSpan.FromSeconds(60))
            {
                _cruiseTargetAltitudeFeet = _levelOffAltFt;
            }
        }
        else
        {
            _levelOffStartUtc = null;
            _levelOffAltFt = null;
        }
    }

    private void UpdateCruise(TelemetryFrame frame)
    {
        if (frame.Phase != FlightPhase.Cruise)
        {
            return;
        }

        _cruiseMaxG = Math.Max(_cruiseMaxG, frame.GForce);
        _cruiseMinG = Math.Min(_cruiseMinG, frame.GForce);

        // Bank split: level cruise (heading rate < 5°/s) vs turning cruise (≥ 5°/s).
        var headingRate = _previousFrame is not null ? HeadingRateDegPerSec(frame, _previousFrame) : 0;
        if (headingRate >= 5.0)
            _cruiseTurnMaxBank = Math.Max(_cruiseTurnMaxBank, Math.Abs(frame.BankAngleDegrees));
        else
            _cruiseLevelMaxBank = Math.Max(_cruiseLevelMaxBank, Math.Abs(frame.BankAngleDegrees));

        // Accumulate altitude deviation only while in genuinely level cruise (target known, |VS| < 100 fpm).
        if (_cruiseTargetAltitudeFeet.HasValue && Math.Abs(frame.VerticalSpeedFpm) < 100)
        {
            var deviation = Math.Abs(frame.IndicatedAltitudeFeet - _cruiseTargetAltitudeFeet.Value);
            _cruiseMaxAltitudeDeviation = Math.Max(_cruiseMaxAltitudeDeviation, deviation);
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

        // Mach deviation from cruise reference.
        _cruiseMaxMachDev = Math.Max(_cruiseMaxMachDev, Math.Abs(frame.Mach - _cruiseReferenceMach.Value));

        var machDelta = Math.Abs(frame.Mach - _cruiseReferenceMach.Value);
        var iasDelta = Math.Abs(frame.IndicatedAirspeedKnots - _cruiseReferenceIasKnots.Value);
        _cruiseMaxSpeedDeviationKts = Math.Max(_cruiseMaxSpeedDeviationKts, iasDelta);
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

        _descentVsSum += frame.VerticalSpeedFpm;
        _descentVsCount++;

        if (frame.IndicatedAltitudeFeet < 10000)
        {
            _descentMaxIasBelowFl100 = Math.Max(_descentMaxIasBelowFl100, frame.IndicatedAirspeedKnots);
        }

        if (_previousFrame is not null &&
            _previousFrame.IndicatedAltitudeFeet > 10000 &&
            frame.IndicatedAltitudeFeet <= 10000 &&
            _descentSpeedAtFL100Kts is null)
        {
            _descentSpeedAtFL100Kts = frame.IndicatedAirspeedKnots;
        }

        if (_postRestoreGraceFrames <= 0 && frame.IndicatedAltitudeFeet <= 18000 && !frame.LandingLightsOn)
        {
            _descentLandingLightsOnByFl180 = false;
        }

        _descentMaxBank = Math.Max(_descentMaxBank, Math.Abs(frame.BankAngleDegrees));
        _descentMaxPitch = Math.Max(_descentMaxPitch, Math.Abs(frame.PitchAngleDegrees));
        _descentMaxG = Math.Max(_descentMaxG, frame.GForce);
        _descentMinG = Math.Min(_descentMinG, frame.GForce);
        _descentMaxRate = Math.Max(_descentMaxRate, Math.Abs(frame.VerticalSpeedFpm));
        if (frame.PitchAngleDegrees < 0)
            _descentMaxNoseDown = Math.Max(_descentMaxNoseDown, Math.Abs(frame.PitchAngleDegrees));
    }

    private void UpdateApproach(TelemetryFrame frame)
    {
        if (frame.Phase != FlightPhase.Approach)
        {
            return;
        }

        _approachMaxBank = Math.Max(_approachMaxBank, Math.Abs(frame.BankAngleDegrees));

        // Gear DOWN transition: record AGL at which gear was first extended.
        if (!_prevGearDown && frame.GearDown && !frame.OnGround)
        {
            _gearDownAglFt ??= frame.AltitudeAglFeet;
        }

        if (_previousFrame is not null)
        {
            if (!_capturedApproach1000Agl &&
                _previousFrame.AltitudeAglFeet > 1000 &&
                frame.AltitudeAglFeet <= 1000)
            {
                _capturedApproach1000Agl = true;
                _approachSpeed1000Agl = frame.IndicatedAirspeedKnots;
                _flapsConfiguredBy1000Agl = frame.FlapsHandleIndex > 0;
                _gearDownAtGate = frame.GearDown;
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

        // Track IAS deviation from the 1000 AGL reference speed.
        if (_capturedApproach1000Agl && _approachSpeed1000Agl > 0)
        {
            _approachMaxIasDev = Math.Max(_approachMaxIasDev,
                Math.Abs(frame.IndicatedAirspeedKnots - _approachSpeed1000Agl));
        }
    }

    private void UpdateStabilizedApproach(TelemetryFrame frame)
    {
        if (frame.Phase != FlightPhase.Approach || frame.AltitudeAglFeet >= 500)
        {
            return;
        }

        // First entry into <500 AGL: capture gate snapshot.
        if (!_inStabApproach)
        {
            _inStabApproach = true;
            _stabPitchAtGate = frame.PitchAngleDegrees;
            _stabApproachSpeed500Agl = frame.IndicatedAirspeedKnots;
            _stabRunwayHeadingRef = frame.HeadingMagneticDegrees;
            _stabGearAt500 = frame.GearDown;
            _stabFlapsAt500 = frame.FlapsHandleIndex;
        }

        // Accumulate maxima throughout the stabilized window.
        _stabMaxIasDev = Math.Max(_stabMaxIasDev,
            Math.Abs(frame.IndicatedAirspeedKnots - _stabApproachSpeed500Agl));
        _stabMaxDescentRate = Math.Max(_stabMaxDescentRate, Math.Abs(frame.VerticalSpeedFpm));

        var hdgDelta = Math.Abs(frame.HeadingMagneticDegrees - _stabRunwayHeadingRef);
        if (hdgDelta > 180) hdgDelta = 360 - hdgDelta;
        _stabMaxHdgDev = Math.Max(_stabMaxHdgDev, hdgDelta);

        // Config change: gear or flaps different from gate values.
        if (frame.GearDown != _stabGearAt500 || frame.FlapsHandleIndex != _stabFlapsAt500)
            _stabConfigChanged = true;
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

        // First-touchdown: set tentative candidate, defer commit until ≥500ms of sustained ground.
        if (!_previousFrame.OnGround && frame.OnGround && !_capturedFirstTouchdown)
        {
            _tentativeTouchdownFrame = frame;
            _tentativeTouchdownPreviousFrame = _previousFrame;
            (_tentativeTdFpm, _tentativeTdLabel) = CalculateTouchdownVerticalSpeed(frame);
        }

        // After first touchdown confirmed: handle subsequent WOW transitions for bounce counting.
        if (!_previousFrame.OnGround && frame.OnGround && _capturedFirstTouchdown)
        {
            _lastTouchdownAt = frame.TimestampUtc;
            var (tdFpm, _) = CalculateTouchdownVerticalSpeed(frame);
            _landingTouchdownVerticalSpeedFpm = Math.Max(_landingTouchdownVerticalSpeedFpm, tdFpm);
            _landingTouchdownGForce = Math.Max(_landingTouchdownGForce, CalculateTouchdownGForce());
            if (_landingTouchdownIndicatedAirspeedKnots == 0)
            {
                _landingTouchdownBankAngleDegrees       = Math.Abs(frame.BankAngleDegrees);
                _landingTouchdownIndicatedAirspeedKnots = frame.IndicatedAirspeedKnots;
                _landingTouchdownPitchAngleDegrees      = frame.PitchAngleDegrees;
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

        // Debounce resolution: confirm or discard the tentative first touchdown.
        if (_tentativeTouchdownFrame != null)
        {
            var elapsed = (frame.TimestampUtc - _tentativeTouchdownFrame.TimestampUtc).TotalMilliseconds;
            if (elapsed >= 500)
            {
                // ≥500ms of sustained ground (or going airborne after ≥500ms ground contact) =
                // confirmed real touchdown. Commit using the first-contact frame's data.
                CommitFirstTouchdown();
            }
            else if (!frame.OnGround)
            {
                // Back airborne within 500ms = SIM_ON_GROUND flicker. Reset and wait for the
                // next transition. Note: a genuine hard landing that bounces in <500ms would
                // also be reset here (TD recorded at the softer second contact). Acceptable
                // since real bounces occur >1s post-touchdown in practice.
                _tentativeTouchdownFrame = null;
                _tentativeTouchdownPreviousFrame = null;
            }
        }

        // Post-touchdown scan: capture first non-zero PLANE TOUCHDOWN NORMAL VELOCITY within 2 s.
        // The sticky SimVar can take a frame or two to update after wheel contact.
        if (_abTouchdownNormalSearchActive)
        {
            if (frame.OnGround && frame.TouchdownNormalVelocityFps != 0
                && frame.TimestampUtc <= _abTouchdownNormalSearchExpiry)
            {
                _abRawTouchdownNormalFpsFirstNonZero = frame.TouchdownNormalVelocityFps;
                _abTouchdownNormalSearchActive = false;
            }
            else if (frame.TimestampUtc > _abTouchdownNormalSearchExpiry)
            {
                _abTouchdownNormalSearchActive = false;
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

    private void UpdateFlightPath(TelemetryFrame frame)
    {
        var tMin = _sessionStartUtc.HasValue
            ? (frame.TimestampUtc - _sessionStartUtc.Value).TotalMinutes
            : 0;

        if (_flightPath.Count == 0)
        {
            _flightPath.Add(new FlightPathPoint { Lat = frame.Latitude, Lon = frame.Longitude, AltFt = frame.AltitudeFeet, TMin = tMin });
            _lastFlightPathSampleAt = frame.TimestampUtc;
            _lastFlightPathAltFt = frame.AltitudeFeet;
            return;
        }

        var timeSinceLast = _lastFlightPathSampleAt.HasValue
            ? frame.TimestampUtc - _lastFlightPathSampleAt.Value
            : TimeSpan.MaxValue;

        var altDelta = _lastFlightPathAltFt.HasValue
            ? Math.Abs(frame.AltitudeFeet - _lastFlightPathAltFt.Value)
            : double.MaxValue;

        if (timeSinceLast >= TimeSpan.FromSeconds(15) || altDelta >= 500)
        {
            _flightPath.Add(new FlightPathPoint { Lat = frame.Latitude, Lon = frame.Longitude, AltFt = frame.AltitudeFeet, TMin = tMin });
            _lastFlightPathSampleAt = frame.TimestampUtc;
            _lastFlightPathAltFt = frame.AltitudeFeet;
        }
    }

    private void UpdateApproachPath(TelemetryFrame frame)
    {
        if (frame.Phase is not (FlightPhase.Approach or FlightPhase.Landing))
            return;
        if (frame.AltitudeAglFeet >= 10000)
            return;

        _approachPath.Add(new ApproachPathPoint
        {
            Lat = frame.Latitude,
            Lon = frame.Longitude,
            AltFt = frame.AltitudeFeet,
            IasKts = frame.IndicatedAirspeedKnots,
            VsFpm = frame.VerticalSpeedFpm,
        });
    }

    private void UpdateMaxPitchWhileWow(TelemetryFrame frame)
    {
        if (!_capturedFirstTouchdown || !frame.OnGround)
            return;
        if (frame.Phase is not (FlightPhase.Landing or FlightPhase.TaxiIn or FlightPhase.Arrival))
            return;

        _landingMaxPitchWhileWowDegrees = Math.Max(_landingMaxPitchWhileWowDegrees, Math.Abs(frame.PitchAngleDegrees));
    }

    private void UpdateTaxiIn(TelemetryFrame frame)
    {
        if (frame.Phase != FlightPhase.TaxiIn)
        {
            return;
        }

        _taxiInSeen = true;
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

        // Only require taxi lights while actively taxiing — below 8 kts (slowing to gate)
        // lights off is correct procedure, ~1 min before parking brake.
        // 3-second debounce: an accidental toggle doesn't cause a permanent penalty.
        if (frame.GroundSpeedKnots >= 8.0 && _postRestoreGraceFrames <= 0)
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

        // Turn speed: max GS during high-heading-rate ticks.
        if (_previousFrame is not null && _previousFrame.Phase == FlightPhase.TaxiIn
            && HeadingRateDegPerSec(frame, _previousFrame) >= 5.0)
        {
            _taxiInMaxTurnSpeed = Math.Max(_taxiInMaxTurnSpeed, frame.GroundSpeedKnots);
        }

        // Smooth deceleration: speed drop > 8 kt in a single frame indicates hard braking.
        // Heuristic — webapp doesn't currently score this field; tune threshold when needed.
        if (_taxiInPrevGroundSpeed.HasValue && _postRestoreGraceFrames <= 0)
        {
            if (_taxiInPrevGroundSpeed.Value - frame.GroundSpeedKnots > 8.0)
                _taxiInSmoothDecel = false;
        }
        _taxiInPrevGroundSpeed = frame.GroundSpeedKnots;

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

        // Beacon off after engines: engines must shut down first, then beacon off.
        if (!_arrivalEnginesOffObserved && !AnyEngineRunning(frame))
        {
            _arrivalEnginesOffObserved = true;
        }
        if (_arrivalEnginesOffObserved && !frame.BeaconLightOn)
        {
            _arrivalBeaconOffAfterEngines = true;
        }

        if (!_arrivalParkingBrakeObserved)
        {
            if (!frame.ParkingBrakeSet)
            {
                // Accumulate whether lights went off before the brake frame arrives.
                if (!frame.TaxiLightsOn)
                {
                    _taxiLightsWentOffBeforeBrake = true;
                }

                if (!AnyEngineRunning(frame))
                {
                    _arrivalParkingBrakeSetBeforeAllEnginesShutdown = false;
                }
            }
            else
            {
                _arrivalParkingBrakeObserved = true;
                // Lights count only if they went off in an earlier frame, not simultaneously.
                _arrivalTaxiLightsOffBeforeParkingBrakeSet = _taxiLightsWentOffBeforeBrake;
            }
        }

        _arrivalAllEnginesOffByEndOfSession = !AnyEngineRunning(frame);
    }

    private void UpdateLightsSystems(TelemetryFrame frame)
    {
        if (_postRestoreGraceFrames > 0) return;

        // Beacon: must be on throughout the airborne portion (after takeoff phase begins).
        if (_takeoffSeen && !frame.OnGround && !frame.BeaconLightOn)
            _beaconOnAirborneThroughout = false;

        // Strobes: must be on while airborne (from takeoff roll through landing rollout).
        var strobesShouldBeOn = !frame.OnGround && _takeoffSeen && !_taxiInSeen;
        if (strobesShouldBeOn && !frame.StrobesOn)
            _strobesCorrect = false;

        // Landing lights compliance: count ticks in Approach and Landing phases.
        if (frame.Phase is FlightPhase.Approach or FlightPhase.Landing)
        {
            _landingLightsTotalTicks++;
            if (frame.LandingLightsOn)
                _landingLightsCompliantTicks++;
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

    private void AddRecentSample(TelemetryFrame frame)
    {
        _recentSamples.Enqueue(new RecentSample(frame.TimestampUtc, frame.VerticalSpeedFpm, frame.GForce, frame.AltitudeAglFeet));

        while (_recentSamples.Count > 0 && frame.TimestampUtc - _recentSamples.Peek().TimestampUtc > TimeSpan.FromSeconds(2))
        {
            _recentSamples.Dequeue();
        }
    }

    private (double Fpm, string Label) CalculateTouchdownVerticalSpeed(TelemetryFrame touchdownFrame)
    {
        // Primary: VELOCITY WORLD Y at the moment of wheel contact.
        //
        // This SimVar is driven by the physics engine and has no barometric lag —
        // it reads the true instantaneous sink rate at the exact frame OnGround flips.
        // The VERTICAL SPEED SimVar (barometric) lags real aircraft motion by 1–2 s,
        // so during a flare where the pilot arrests from 350 → 195 fpm the baro VS
        // still reads the pre-flare rate at touchdown. VELOCITY WORLD Y does not have
        // this problem and matches what Volanta and other pro trackers report.
        if (touchdownFrame.VelocityWorldYFps < 0)
            return (Math.Abs(touchdownFrame.VelocityWorldYFps) * 60.0, "VelocityWorldY (TD frame)");

        // Fallback A: VelocityWorldY on the last airborne frame (slightly earlier reading).
        // Catches the case where the sim briefly reports 0 or positive on the touchdown frame.
        if (_previousFrame is not null && !_previousFrame.OnGround && _previousFrame.VelocityWorldYFps < 0)
            return (Math.Abs(_previousFrame.VelocityWorldYFps) * 60.0, "VelocityWorldY (last airborne)");

        // Fallback B: barometric VS on the last airborne frame (sampled just before contact).
        // The frame immediately before OnGround is free of gear-compression artefacts.
        if (_previousFrame is not null
            && !_previousFrame.OnGround
            && _previousFrame.AltitudeAglFeet <= 50
            && _previousFrame.VerticalSpeedFpm < 0)
        {
            return (Math.Abs(_previousFrame.VerticalSpeedFpm), "VerticalSpeed (last airborne)");
        }

        // Fallback C: barometric VS on the touchdown frame itself.
        if (touchdownFrame.VerticalSpeedFpm < 0)
            return (Math.Abs(touchdownFrame.VerticalSpeedFpm), "VerticalSpeed (TD frame)");

        // Fallback D: AGL rate-of-change across the full flare window in the 2-second rolling
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
                if (aglSinkFpm > 0) return (aglSinkFpm, "AGL rate-of-change");
            }
        }

        // Last resort: lowest barometric VS from sub-100-ft samples.
        var subHundredSamples = _recentSamples
            .Where(static s => s.AltitudeAglFeet is > 0 and <= 100)
            .ToList();

        if (subHundredSamples.Count > 0)
            return (Math.Abs(subHundredSamples.Min(static s => s.VerticalSpeedFpm)), "BaroVS min (sub-100ft)");

        return (0, "zero");
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

    private void CommitFirstTouchdown()
    {
        var tdFrame = _tentativeTouchdownFrame!;
        _lastTouchdownAt = tdFrame.TimestampUtc;
        _landingTouchdownVerticalSpeedFpm = Math.Max(_landingTouchdownVerticalSpeedFpm, _tentativeTdFpm);
        _landingTouchdownGForce = Math.Max(_landingTouchdownGForce, CalculateTouchdownGForce());
        if (_landingTouchdownIndicatedAirspeedKnots == 0)
        {
            _landingTouchdownBankAngleDegrees       = Math.Abs(tdFrame.BankAngleDegrees);
            _landingTouchdownIndicatedAirspeedKnots = tdFrame.IndicatedAirspeedKnots;
            _landingTouchdownPitchAngleDegrees      = tdFrame.PitchAngleDegrees;
        }
        _capturedFirstTouchdown               = true;
        _landingTouchdownLat                  = tdFrame.Latitude;
        _landingTouchdownLon                  = tdFrame.Longitude;
        _landingTouchdownHeadingMagneticDeg   = tdFrame.HeadingMagneticDegrees;
        _landingTouchdownAltFt                = tdFrame.AltitudeFeet;
        _landingTouchdownWindSpeedKnots       = tdFrame.WindSpeedKnots;
        _landingTouchdownWindDirectionDegrees = tdFrame.WindDirectionDegrees;
        _landingGearUpAtTouchdown             = !tdFrame.GearDown;
        _landingMaxPitchWhileWowDegrees       = Math.Abs(tdFrame.PitchAngleDegrees);

        // A/B instrumentation: all values from the first-contact frame.
        _abFpmVelocityWorldY  = tdFrame.VelocityWorldYFps < 0
            ? Math.Abs(tdFrame.VelocityWorldYFps) * 60.0 : 0;
        _abFpmTouchdownNormal = tdFrame.TouchdownNormalVelocityFps < 0
            ? Math.Abs(tdFrame.TouchdownNormalVelocityFps) * 60.0 : 0;
        _abFpmVerticalSpeed   = tdFrame.VerticalSpeedFpm < 0
            ? Math.Abs(tdFrame.VerticalSpeedFpm) : 0;
        _abFinalSelected      = _landingTouchdownVerticalSpeedFpm;

        // Last-airborne candidates from the frame immediately before first contact.
        if (_tentativeTouchdownPreviousFrame is not null && !_tentativeTouchdownPreviousFrame.OnGround)
        {
            _abFpmVelocityWorldYLastAirborne = _tentativeTouchdownPreviousFrame.VelocityWorldYFps < 0
                ? Math.Abs(_tentativeTouchdownPreviousFrame.VelocityWorldYFps) * 60.0 : 0;
            _abFpmVerticalSpeedLastAirborne  = _tentativeTouchdownPreviousFrame.VerticalSpeedFpm < 0
                ? Math.Abs(_tentativeTouchdownPreviousFrame.VerticalSpeedFpm) : 0;
        }
        _abSelectedSourceLabel          = _tentativeTdLabel;
        _abRawTouchdownNormalFpsTdFrame = tdFrame.TouchdownNormalVelocityFps;

        // If TD-frame TouchdownNormal is zero, open a 2-second scan for first non-zero.
        // The sticky SimVar can take a frame or two to update after wheel contact.
        if (tdFrame.TouchdownNormalVelocityFps == 0)
        {
            _abTouchdownNormalSearchActive = true;
            _abTouchdownNormalSearchExpiry = tdFrame.TimestampUtc + TimeSpan.FromSeconds(2);
        }
        if (tdFrame.TouchdownZoneExcessDistanceFeet is not null)
        {
            _landingTouchdownZoneExcessDistanceFeet = tdFrame.TouchdownZoneExcessDistanceFeet.Value;
        }

        _tentativeTouchdownFrame         = null;
        _tentativeTouchdownPreviousFrame = null;
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

    private static double HeadingRateDegPerSec(TelemetryFrame frame, TelemetryFrame prev)
    {
        var dt = (frame.TimestampUtc - prev.TimestampUtc).TotalSeconds;
        if (dt <= 0) return 0;
        var delta = Math.Abs(frame.HeadingMagneticDegrees - prev.HeadingMagneticDegrees);
        if (delta > 180) delta = 360 - delta;
        return delta / dt;
    }

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
