using System.Diagnostics;
using SimCrewOps.PhaseEngine.Models;
using SimCrewOps.PhaseEngine.PhaseEngine;
using SimCrewOps.Runways.Models;
using SimCrewOps.Runways.Services;
using SimCrewOps.Runtime.Models;
using SimCrewOps.Scoring.Models;
using SimCrewOps.Scoring.Scoring;
using SimCrewOps.Tracking.Models;
using SimCrewOps.Tracking.Tracking;

namespace SimCrewOps.Runtime.Runtime;

public sealed class RuntimeCoordinator
{
    private FlightSessionContext _context;
    private readonly FlightPhaseEngine _phaseEngine;
    private readonly FlightSessionScoringTracker _scoringTracker;
    private readonly ScoringEngine _scoringEngine;
    private readonly RunwayResolver _runwayResolver;
    private ILivePositionUploader? _livePositionUploader;

    private FlightSessionBlockTimes _blockTimes = new();
    private RunwayResolutionResult? _landingRunwayResolution;
    private RunwayResolutionResult? _approachRunwayResolution;
    private readonly TouchdownZoneCalculator _tzCalc = new();
    private FlightPhase _lastKnownPhase = FlightPhase.Preflight;
    private double? _approachDistanceNm;
    private double? _glidepathDeviationFeet;
    private TelemetryFrame? _lastTelemetryFrame;
    private DateTimeOffset? _lastLivePositionSentUtc;
    private double? _lastLivePositionLatitude;
    private double? _lastLivePositionLongitude;
    // Session-end trigger: set once when engines off + beacon off + parking brake set after landing.
    private bool _sessionEndTriggered;
    // ── Approach path recording ───────────────────────────────────────────────────────────────
    // Lat/lon of the arrival airport reference point used for haversine distance calculations.
    // Set on Descent entry (early, using a runway resolution at that point) and refined when
    // the approach runway pre-resolution fires on Approach entry.
    private double? _approachAirportRefLat;
    private double? _approachAirportRefLon;
    private bool _approachAirportRefResolved;
    // Time guard: only record a sample when at least 2 s have elapsed since the last one.
    private DateTimeOffset? _approachPathLastSampleAt;
    // True once the aircraft enters recording range (dist ≤ 15 nm in Descent/Approach).
    private bool _approachPathRecording;

    /// <summary>
    /// UTC timestamp of the most recent live-position upload that the server accepted (HTTP 200).
    /// Null until the first successful upload.
    /// </summary>
    public DateTimeOffset? LastSuccessfulUploadUtc { get; private set; }

    public RuntimeCoordinator(
        FlightSessionContext context,
        RunwayResolver runwayResolver,
        ILivePositionUploader? livePositionUploader = null,
        FlightPhaseEngine? phaseEngine = null,
        FlightSessionScoringTracker? scoringTracker = null,
        ScoringEngine? scoringEngine = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(runwayResolver);

        _context = context;
        _runwayResolver = runwayResolver;
        _livePositionUploader = livePositionUploader;
        _phaseEngine = phaseEngine ?? new FlightPhaseEngine();
        _scoringTracker = scoringTracker ?? new FlightSessionScoringTracker(context.Profile);
        _scoringEngine = scoringEngine ?? new ScoringEngine();
    }

    /// <summary>
    /// Hot-swaps the live position uploader. Safe to call at any time — the next telemetry
    /// frame will use the new uploader (or send nothing if <paramref name="uploader"/> is null).
    /// </summary>
    public void UpdateLivePositionUploader(ILivePositionUploader? uploader)
    {
        _livePositionUploader = uploader;
    }

    /// <summary>
    /// Updates the flight session context (departure, arrival, flight mode, etc.).
    /// Only applies when no session is in progress — if blocks-off has already fired
    /// the context is preserved to keep the current flight's data intact.
    /// </summary>
    public FlightSessionContext CurrentContext => _context;

    public void UpdateContext(FlightSessionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Don't overwrite context mid-flight — blocks-off already fired means
        // the session is in progress and changing context would corrupt its records.
        if (_blockTimes.BlocksOffUtc is null)
        {
            _context = context;
        }
    }

    /// <summary>
    /// Updates the aircraft type and category from a SimConnect detection event.
    /// Unlike <see cref="UpdateContext"/>, this is intentionally NOT gated on blocks-off —
    /// the ATC MODEL SimVar fires asynchronously and typically arrives a few seconds into
    /// pushback, after blocks-off has already fired.  The detected type is authoritative
    /// telemetry (what MSFS actually has loaded) and must always win over any bid or
    /// previous-session value in the context.
    /// </summary>
    public void UpdateAircraftType(string aircraftType, string aircraftCategory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aircraftType);

        _context = _context with
        {
            AircraftType     = aircraftType,
            AircraftCategory = aircraftCategory,
        };
    }

    public void Restore(FlightSessionRuntimeState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        _context = state.Context;
        _blockTimes = state.BlockTimes;
        _landingRunwayResolution = state.LandingRunwayResolution;
        _approachRunwayResolution = state.ApproachRunwayResolution;
        _approachDistanceNm = state.ApproachDistanceNm;
        _glidepathDeviationFeet = state.GlidepathDeviationFeet;
        _lastKnownPhase = state.CurrentPhase;
        _lastTelemetryFrame = state.LastTelemetryFrame;
        _lastLivePositionSentUtc = null;
        _lastLivePositionLatitude = state.LastTelemetryFrame?.Latitude;
        _lastLivePositionLongitude = state.LastTelemetryFrame?.Longitude;
        _sessionEndTriggered = state.BlockTimes.SessionEndTriggeredUtc is not null;
        // Approach path recording fields reset — the buffer is cleared in the tracker's
        // Restore() and ref coords will be re-resolved on the next Descent/Approach entry.
        _approachAirportRefLat = null;
        _approachAirportRefLon = null;
        _approachAirportRefResolved = false;
        _approachPathLastSampleAt = null;
        _approachPathRecording = false;

        _phaseEngine.Restore(
            state.CurrentPhase,
            state.LastTelemetryFrame,
            BuildRestoredBlockEvents(state));

        _scoringTracker.Restore(
            state.ScoreInput,
            state.CurrentPhase,
            state.LastTelemetryFrame,
            state.Context.Profile,
            state.BlockTimes.WheelsOffUtc,
            state.BlockTimes.WheelsOnUtc);
    }

    public async Task<RuntimeFrameResult> ProcessFrameAsync(
        TelemetryFrame telemetryFrame,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(telemetryFrame);

        var phaseFrame = _phaseEngine.Process(telemetryFrame);

        // --- Approach runway pre-resolution and live approach metrics ---
        var previousPhase = _lastKnownPhase;
        _lastKnownPhase = phaseFrame.Phase;

        // ── Approach airport reference coords (for approach-path distance) ──────────────────
        // On Descent entry, pre-resolve any runway at the arrival airport so we have a
        // reference lat/lon for haversine distance calculations during Descent (before the
        // per-approach runway resolution fires on Approach entry).
        if (phaseFrame.Phase == FlightPhase.Descent
            && previousPhase != FlightPhase.Descent
            && !_approachAirportRefResolved
            && !string.IsNullOrWhiteSpace(_context.ArrivalAirportIcao))
        {
            try
            {
                var refResolution = await _runwayResolver.ResolveAsync(
                    new RunwayResolutionRequest
                    {
                        ArrivalAirportIcao         = _context.ArrivalAirportIcao,
                        TouchdownLatitude          = telemetryFrame.Latitude,
                        TouchdownLongitude         = telemetryFrame.Longitude,
                        TouchdownHeadingTrueDegrees = telemetryFrame.HeadingTrueDegrees,
                    },
                    cancellationToken).ConfigureAwait(false);

                if (refResolution is not null)
                {
                    _approachAirportRefLat     = refResolution.Runway.ThresholdLatitude;
                    _approachAirportRefLon     = refResolution.Runway.ThresholdLongitude;
                    _approachAirportRefResolved = true;
                }
            }
            catch
            {
                // Best-effort — missing ref coords means no Descent approach-path samples.
            }
        }

        // Pre-resolve the runway once when entering Approach phase.
        // Uses heading as the best-match hint so the correct runway end is picked even
        // before the aircraft is close enough for a precise position-based resolution.
        // Also refines the approach airport reference coords with the resolved threshold.
        if (phaseFrame.Phase == FlightPhase.Approach
            && previousPhase != FlightPhase.Approach
            && !string.IsNullOrWhiteSpace(_context.ArrivalAirportIcao))
        {
            try
            {
                _approachRunwayResolution = await _runwayResolver.ResolveAsync(
                    new RunwayResolutionRequest
                    {
                        ArrivalAirportIcao         = _context.ArrivalAirportIcao,
                        TouchdownLatitude          = telemetryFrame.Latitude,
                        TouchdownLongitude         = telemetryFrame.Longitude,
                        TouchdownHeadingTrueDegrees = telemetryFrame.HeadingTrueDegrees,
                    },
                    cancellationToken).ConfigureAwait(false);

                if (_approachRunwayResolution is not null)
                {
                    // Use the resolved threshold for all subsequent distance calculations —
                    // more accurate than the Descent-entry estimate.
                    _approachAirportRefLat     = _approachRunwayResolution.Runway.ThresholdLatitude;
                    _approachAirportRefLon     = _approachRunwayResolution.Runway.ThresholdLongitude;
                    _approachAirportRefResolved = true;
                }
            }
            catch
            {
                // Swallow — approach runway resolution is best-effort; missing it
                // just means DIST THR and G/PATH stay blank for this approach.
            }
        }

        // Compute live distance-to-threshold and geometric glidepath deviation every
        // frame while in Approach.  Clear when leaving Approach so stale values don't
        // persist into the Landing phase (where the confirmed LandingRunwayResolution
        // drives the TDZ section instead).
        if (phaseFrame.Phase == FlightPhase.Approach && _approachRunwayResolution is not null)
        {
            var projection = _tzCalc.ProjectTouchdown(
                _approachRunwayResolution.Runway,
                telemetryFrame.Latitude,
                telemetryFrame.Longitude);

            // AlongTrackDistanceFeet is negative when the aircraft is still before
            // the threshold.  Negate to get a positive "distance to go" value.
            var distToThresholdFt = Math.Max(0.0, -projection.AlongTrackDistanceFeet);
            _approachDistanceNm    = distToThresholdFt / 6076.115;

            // Ideal AGL on a 3° glidepath = dist_ft × tan(3°) = dist_ft × 0.05241.
            // Deviation > 0 → above path (fly down), < 0 → below path (fly up).
            var idealAglFt         = distToThresholdFt * 0.05241;
            _glidepathDeviationFeet = telemetryFrame.AltitudeAglFeet - idealAglFt;
        }
        else if (phaseFrame.Phase != FlightPhase.Approach)
        {
            _approachDistanceNm     = null;
            _glidepathDeviationFeet = null;
        }

        // ── Approach path recording ──────────────────────────────────────────────────────────
        // Record a sample every ≥ 2 s while in Descent/Approach and within 15 nm of the
        // arrival airport reference point.  Record one final sample at the Landing transition.
        if (_approachAirportRefLat.HasValue && _approachAirportRefLon.HasValue)
        {
            var phase = phaseFrame.Phase;
            bool inApproachRange = phase == FlightPhase.Descent || phase == FlightPhase.Approach;
            bool enteringLanding = phase == FlightPhase.Landing && previousPhase != FlightPhase.Landing;

            if (inApproachRange || enteringLanding)
            {
                var distNm = HaversineNm(
                    telemetryFrame.Latitude, telemetryFrame.Longitude,
                    _approachAirportRefLat.Value, _approachAirportRefLon.Value);

                bool withinRange = distNm <= 15.0 || enteringLanding;
                bool timeOk = enteringLanding
                    || _approachPathLastSampleAt is null
                    || (telemetryFrame.TimestampUtc - _approachPathLastSampleAt.Value).TotalSeconds >= 2.0;

                if (withinRange && timeOk)
                {
                    _approachPathRecording = true;
                    _scoringTracker.RecordApproachSample(
                        distNm,
                        telemetryFrame.AltitudeAglFeet,
                        telemetryFrame.IndicatedAirspeedKnots,
                        telemetryFrame.VerticalSpeedFpm);
                    _approachPathLastSampleAt = telemetryFrame.TimestampUtc;
                }
            }
        }
        // ----------------------------------------------------------------

        var enrichedTelemetryFrame = await EnrichTelemetryFrameAsync(phaseFrame, cancellationToken).ConfigureAwait(false);
        var enrichedPhaseFrame = new PhaseFrame
        {
            Raw = enrichedTelemetryFrame,
            Phase = phaseFrame.Phase,
            BlockEvent = phaseFrame.BlockEvent,
        };

        var labelledFrame = enrichedTelemetryFrame with { Phase = enrichedPhaseFrame.Phase };
        _scoringTracker.Ingest(labelledFrame);
        // Discard approach path on crash so no partial data is uploaded.
        if (labelledFrame.CrashDetected)
            _scoringTracker.DiscardApproachPath();
        _lastTelemetryFrame = labelledFrame;
        CaptureBlockEvent(enrichedPhaseFrame.BlockEvent);
        CheckSessionEndCondition(labelledFrame);
        TryDispatchLivePosition(labelledFrame, cancellationToken);

        var scoreInput = _scoringTracker.BuildScoreInput();
        var scoreResult = _scoringEngine.Calculate(scoreInput);

        var state = new FlightSessionRuntimeState
        {
            Context = _context,
            CurrentPhase = enrichedPhaseFrame.Phase,
            BlockTimes = _blockTimes,
            LastTelemetryFrame = _lastTelemetryFrame,
            LandingRunwayResolution = _landingRunwayResolution,
            ApproachRunwayResolution = _approachRunwayResolution,
            ApproachDistanceNm = _approachDistanceNm,
            GlidepathDeviationFeet = _glidepathDeviationFeet,
            ScoreInput = scoreInput,
            ScoreResult = scoreResult,
        };

        return new RuntimeFrameResult
        {
            PhaseFrame = enrichedPhaseFrame,
            EnrichedTelemetryFrame = labelledFrame,
            RunwayResolution = _landingRunwayResolution,
            State = state,
        };
    }

    private async Task<TelemetryFrame> EnrichTelemetryFrameAsync(PhaseFrame phaseFrame, CancellationToken cancellationToken)
    {
        if (phaseFrame.BlockEvent?.Type == BlockEventType.WheelsOn &&
            !string.IsNullOrWhiteSpace(_context.ArrivalAirportIcao))
        {
            _landingRunwayResolution = await _runwayResolver.ResolveAsync(
                new RunwayResolutionRequest
                {
                    ArrivalAirportIcao = _context.ArrivalAirportIcao!,
                    TouchdownLatitude = phaseFrame.Raw.Latitude,
                    TouchdownLongitude = phaseFrame.Raw.Longitude,
                    TouchdownHeadingTrueDegrees = phaseFrame.Raw.HeadingTrueDegrees,
                },
                cancellationToken).ConfigureAwait(false);

            if (_landingRunwayResolution is not null)
            {
                _scoringTracker.SetTouchdownRunwayMetrics(
                    _landingRunwayResolution.Projection.CrossTrackDistanceFeet,
                    _landingRunwayResolution.HeadingDifferenceDegrees);

                // Compute headwind and crosswind components from wind data at touchdown.
                var (headwind, crosswind) = ComputeWindComponents(
                    phaseFrame.Raw.WindSpeedKnots,
                    phaseFrame.Raw.WindDirectionDegrees,
                    _landingRunwayResolution.Runway.TrueHeadingDegrees);
                _scoringTracker.SetTouchdownWindComponents(headwind, crosswind);
            }
        }

        var touchdownZoneExcess = phaseFrame.Raw.TouchdownZoneExcessDistanceFeet
            ?? _landingRunwayResolution?.Projection.TouchdownZoneExcessDistanceFeet;

        return touchdownZoneExcess == phaseFrame.Raw.TouchdownZoneExcessDistanceFeet
            ? phaseFrame.Raw
            : phaseFrame.Raw with { TouchdownZoneExcessDistanceFeet = touchdownZoneExcess };
    }

    private void CaptureBlockEvent(BlockEvent? blockEvent)
    {
        if (blockEvent is null)
        {
            return;
        }

        _blockTimes = blockEvent.Type switch
        {
            BlockEventType.BlocksOff => _blockTimes with { BlocksOffUtc = blockEvent.TimestampUtc },
            BlockEventType.WheelsOff => _blockTimes with { WheelsOffUtc = blockEvent.TimestampUtc },
            BlockEventType.WheelsOn => _blockTimes with { WheelsOnUtc = blockEvent.TimestampUtc },
            BlockEventType.BlocksOn => _blockTimes with { BlocksOnUtc = blockEvent.TimestampUtc },
            _ => _blockTimes,
        };
    }

    /// <summary>
    /// Detects the post-flight session-end condition: all engines off, beacon light off,
    /// and parking brake set — after a completed landing (WheelsOn recorded).
    /// Sets <see cref="SessionEndTriggeredUtc"/> once and never again for the session.
    /// </summary>
    private void CheckSessionEndCondition(TelemetryFrame frame)
    {
        if (_sessionEndTriggered)
        {
            return;
        }

        // Guard: only fire after a landing has been recorded.
        if (_blockTimes.WheelsOnUtc is null)
        {
            return;
        }

        // Condition: all engines off AND beacon off AND parking brake set.
        var allEnginesOff = !frame.Engine1Running
                         && !frame.Engine2Running
                         && !frame.Engine3Running
                         && !frame.Engine4Running;

        if (!allEnginesOff || frame.BeaconLightOn || !frame.ParkingBrakeSet)
        {
            return;
        }

        _sessionEndTriggered = true;
        _blockTimes = _blockTimes with { SessionEndTriggeredUtc = frame.TimestampUtc };
    }

    private void TryDispatchLivePosition(TelemetryFrame telemetryFrame, CancellationToken cancellationToken)
    {
        // Upload whenever we have a valid uploader and GPS data — no longer gated on
        // IsActiveFlight().  The previous gate required BlocksOffUtc to be set, which
        // meant position never uploaded if the tracker started mid-flight (e.g. after
        // an auto-update restart) or if blocks-off was missed.
        if (_livePositionUploader is null)
        {
            return;
        }

        var hasMovedEnough = _lastLivePositionLatitude is null ||
            _lastLivePositionLongitude is null ||
            Math.Abs(telemetryFrame.Latitude - _lastLivePositionLatitude.Value) > 0.0001 ||
            Math.Abs(telemetryFrame.Longitude - _lastLivePositionLongitude.Value) > 0.0001;

        var elapsed = _lastLivePositionSentUtc is null
            ? TimeSpan.MaxValue
            : telemetryFrame.TimestampUtc - _lastLivePositionSentUtc.Value;

        if (!hasMovedEnough && elapsed < TimeSpan.FromSeconds(4))
        {
            return;
        }

        _lastLivePositionSentUtc = telemetryFrame.TimestampUtc;
        _lastLivePositionLatitude = telemetryFrame.Latitude;
        _lastLivePositionLongitude = telemetryFrame.Longitude;

        var payload = new LivePositionPayload
        {
            Latitude = telemetryFrame.Latitude,
            Longitude = telemetryFrame.Longitude,
            HeadingMagnetic = telemetryFrame.HeadingMagneticDegrees,
            HeadingTrue     = telemetryFrame.HeadingTrueDegrees,
            OnGround        = telemetryFrame.OnGround,
            AltitudeFt = telemetryFrame.AltitudeFeet,
            AltitudeAglFt = telemetryFrame.AltitudeAglFeet,
            IndicatedAirspeedKts = telemetryFrame.IndicatedAirspeedKnots,
            GroundSpeedKts = telemetryFrame.GroundSpeedKnots,
            VerticalSpeedFpm = telemetryFrame.VerticalSpeedFpm,
            Phase = telemetryFrame.Phase.ToString(),
            FlightMode = _context.FlightMode,
            BidId = string.IsNullOrWhiteSpace(_context.BidId) ? null : _context.BidId,
            Departure      = _context.DepartureAirportIcao,
            Arrival        = _context.ArrivalAirportIcao,
            FlightNumber   = _context.FlightNumber,
            Aircraft       = _context.AircraftType,
            AircraftCategory = _context.AircraftCategory,
        };

        _ = SendLivePositionAsync(payload, cancellationToken);
    }

    private async Task SendLivePositionAsync(LivePositionPayload payload, CancellationToken cancellationToken)
    {
        if (_livePositionUploader is null)
        {
            return;
        }

        try
        {
            var ok = await _livePositionUploader.SendPositionAsync(payload, cancellationToken).ConfigureAwait(false);
            if (ok)
                LastSuccessfulUploadUtc = DateTimeOffset.UtcNow;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Trace.TraceWarning("Tracker live position dispatch failed: {0}", ex.Message);
        }
    }

    /// <summary>
    /// Decomposes wind speed and direction into headwind and crosswind components
    /// relative to the runway.
    /// </summary>
    /// <param name="windSpeedKnots">Ambient wind speed in knots.</param>
    /// <param name="windDirectionDegrees">Direction the wind is coming FROM, in degrees true.</param>
    /// <param name="runwayTrueHeadingDegrees">Runway heading in degrees true (direction of landing roll).</param>
    /// <returns>
    /// HeadwindKnots: positive = headwind component (opposing motion).
    /// CrosswindKnots: positive = wind from the right side of the runway.
    /// </returns>
    private static (double HeadwindKnots, double CrosswindKnots) ComputeWindComponents(
        double windSpeedKnots,
        double windDirectionDegrees,
        double runwayTrueHeadingDegrees)
    {
        // Relative angle: wind-from minus runway heading.
        // At 0° relative angle the wind is on the nose (pure headwind).
        // At 90° relative angle the wind is from the right (pure crosswind right).
        var relAngleRad = (windDirectionDegrees - runwayTrueHeadingDegrees) * Math.PI / 180.0;
        var headwind  = windSpeedKnots * Math.Cos(relAngleRad);  // positive = headwind
        var crosswind = windSpeedKnots * Math.Sin(relAngleRad);  // positive = from the right
        return (headwind, crosswind);
    }

    /// <summary>
    /// Haversine great-circle distance between two WGS-84 coordinates, in nautical miles.
    /// Used to compute how far the aircraft is from the arrival airport reference point
    /// for approach-path recording range checks.
    /// </summary>
    private static double HaversineNm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 3440.065; // Earth mean radius in nautical miles
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLon = (lon2 - lon1) * Math.PI / 180.0;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0)
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
    }

    private bool IsActiveFlight() =>
        _blockTimes.BlocksOffUtc is not null && _blockTimes.BlocksOnUtc is null;

    private static IReadOnlyList<BlockEvent> BuildRestoredBlockEvents(FlightSessionRuntimeState state)
    {
        var events = new List<BlockEvent>(capacity: 4);
        AddBlockEvent(events, BlockEventType.BlocksOff, state.BlockTimes.BlocksOffUtc, state.LastTelemetryFrame);
        AddBlockEvent(events, BlockEventType.WheelsOff, state.BlockTimes.WheelsOffUtc, state.LastTelemetryFrame);
        AddBlockEvent(events, BlockEventType.WheelsOn, state.BlockTimes.WheelsOnUtc, state.LastTelemetryFrame);
        AddBlockEvent(events, BlockEventType.BlocksOn, state.BlockTimes.BlocksOnUtc, state.LastTelemetryFrame);
        return events;
    }

    private static void AddBlockEvent(
        ICollection<BlockEvent> events,
        BlockEventType type,
        DateTimeOffset? timestampUtc,
        TelemetryFrame? referenceFrame)
    {
        if (timestampUtc is null)
        {
            return;
        }

        events.Add(new BlockEvent
        {
            Type = type,
            TimestampUtc = timestampUtc.Value,
            LatitudeDeg = referenceFrame?.Latitude ?? 0,
            LongitudeDeg = referenceFrame?.Longitude ?? 0,
        });
    }
}
