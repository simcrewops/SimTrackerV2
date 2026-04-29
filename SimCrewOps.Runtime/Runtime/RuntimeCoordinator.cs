using System.Diagnostics;
using SimCrewOps.PhaseEngine.Models;
using SimCrewOps.PhaseEngine.PhaseEngine;
using SimCrewOps.Runtime.Models;
using SimCrewOps.Scoring.Models;
using SimCrewOps.Scoring.Scoring;
using SimCrewOps.Tracking.Models;
using SimCrewOps.Tracking.Tracking;

namespace SimCrewOps.Runtime.Runtime;

public sealed class RuntimeCoordinator
{
    // Threshold above which an inter-frame position jump is treated as a sim reposition
    // rather than normal flight. 1 nm ≈ 6,076 ft — impossible in a single telemetry poll
    // (~200 ms) even for supersonic aircraft.
    private const double RepositionThresholdNm = 1.0;

    private FlightSessionContext _context;
    private FlightPhaseEngine _phaseEngine;
    private FlightSessionScoringTracker _scoringTracker;
    private readonly ScoringEngine _scoringEngine;
    private ILivePositionUploader? _livePositionUploader;

    private FlightSessionBlockTimes _blockTimes = new();
    private TelemetryFrame? _lastTelemetryFrame;

    // Beacon state — cursor is only advanced inside SendLivePositionAsync after confirmed success,
    // so a failed send does not suppress the next retry attempt.
    private bool _beaconInitialized;
    private DateTimeOffset? _lastLivePositionSentUtc;
    private double? _lastLivePositionLatitude;
    private double? _lastLivePositionLongitude;

    /// <summary>
    /// UTC timestamp of the most recent live-position upload that the server accepted (HTTP 200).
    /// Null until the first successful upload.
    /// </summary>
    public DateTimeOffset? LastSuccessfulUploadUtc { get; private set; }

    public RuntimeCoordinator(
        FlightSessionContext context,
        ILivePositionUploader? livePositionUploader = null,
        FlightPhaseEngine? phaseEngine = null,
        FlightSessionScoringTracker? scoringTracker = null,
        ScoringEngine? scoringEngine = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
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
    /// Resets the session to a clean Preflight state without losing the current flight context
    /// (departure/arrival ICAO, flight number, etc.).  Called automatically when a sim
    /// reposition is detected, or explicitly by the pilot via the Reset button.
    /// </summary>
    public void Reset()
    {
        _blockTimes = new FlightSessionBlockTimes();
        _lastTelemetryFrame = null;
        _beaconInitialized = false;
        _lastLivePositionSentUtc = null;
        _lastLivePositionLatitude = null;
        _lastLivePositionLongitude = null;

        // Replace rather than reset — avoids needing a Reset() on FlightSessionScoringTracker
        // which would otherwise have to zero out dozens of fields and risk missing new ones.
        _phaseEngine = new FlightPhaseEngine();
        _scoringTracker = new FlightSessionScoringTracker(_context.Profile);
    }

    public void Restore(FlightSessionRuntimeState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        _context = state.Context;
        _blockTimes = state.BlockTimes;
        _lastTelemetryFrame = state.LastTelemetryFrame;
        _lastLivePositionSentUtc = null;
        _lastLivePositionLatitude = state.LastTelemetryFrame?.Latitude;
        _lastLivePositionLongitude = state.LastTelemetryFrame?.Longitude;
        // Restored sessions know their position already — beacon can fire on the next frame.
        _beaconInitialized = state.LastTelemetryFrame is not null;

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

    public Task<RuntimeFrameResult> ProcessFrameAsync(
        TelemetryFrame telemetryFrame,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(telemetryFrame);

        // ── Reposition detection ─────────────────────────────────────────────────
        // If the aircraft's position jumps more than RepositionThresholdNm between
        // consecutive frames (impossible at any realistic aircraft speed during a
        // ~200 ms poll interval), treat it as a sim "Set on Ground" or world-map
        // reposition and wipe the session so it doesn't record a phantom flight.
        // Guard against (0, 0) frames that SimConnect can emit before GPS is ready.
        var wasRepositionReset = false;
        if (_lastTelemetryFrame is not null
            && (_lastTelemetryFrame.Latitude != 0.0 || _lastTelemetryFrame.Longitude != 0.0)
            && (telemetryFrame.Latitude != 0.0 || telemetryFrame.Longitude != 0.0)
            && CalculateDistanceNm(
                   _lastTelemetryFrame.Latitude, _lastTelemetryFrame.Longitude,
                   telemetryFrame.Latitude,      telemetryFrame.Longitude) > RepositionThresholdNm)
        {
            Reset();
            wasRepositionReset = true;
        }

        var phaseFrame = _phaseEngine.Process(telemetryFrame);
        var labelledFrame = telemetryFrame with { Phase = phaseFrame.Phase };

        _scoringTracker.Ingest(labelledFrame);
        _lastTelemetryFrame = labelledFrame;
        CaptureBlockEvent(phaseFrame.BlockEvent);
        TryDispatchLivePosition(labelledFrame, cancellationToken);

        var scoreInput = _scoringTracker.BuildScoreInput();
        var scoreResult = _scoringEngine.Calculate(scoreInput);

        var state = new FlightSessionRuntimeState
        {
            Context = _context,
            CurrentPhase = phaseFrame.Phase,
            BlockTimes = _blockTimes,
            LastTelemetryFrame = _lastTelemetryFrame,
            ScoreInput = scoreInput,
            ScoreResult = scoreResult,
        };

        return Task.FromResult(new RuntimeFrameResult
        {
            PhaseFrame = phaseFrame,
            EnrichedTelemetryFrame = labelledFrame,
            State = state,
            WasRepositionReset = wasRepositionReset,
        });
    }

    /// <summary>
    /// Haversine great-circle distance between two WGS-84 coordinates, in nautical miles.
    /// </summary>
    private static double CalculateDistanceNm(double lat1, double lon1, double lat2, double lon2)
    {
        const double EarthRadiusNm = 3440.065;
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLon = (lon2 - lon1) * Math.PI / 180.0;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0)
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return EarthRadiusNm * 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
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
            BlockEventType.WheelsOn  => _blockTimes with { WheelsOnUtc  = blockEvent.TimestampUtc },
            BlockEventType.BlocksOn  => _blockTimes with { BlocksOnUtc  = blockEvent.TimestampUtc },
            _ => _blockTimes,
        };
    }

    private void TryDispatchLivePosition(TelemetryFrame telemetryFrame, CancellationToken cancellationToken)
    {
        if (_livePositionUploader is null)
            return;

        // Ignore (0,0) frames that SimConnect emits before GPS is ready.
        if (telemetryFrame.Latitude == 0.0 && telemetryFrame.Longitude == 0.0)
            return;

        if (!_beaconInitialized)
        {
            // First valid GPS frame: seed the position reference so subsequent movement
            // checks have a baseline. Do NOT send on this frame — wait for the second
            // confirmed frame to avoid beaconing on a stale "cold start" position.
            _lastLivePositionLatitude  = telemetryFrame.Latitude;
            _lastLivePositionLongitude = telemetryFrame.Longitude;
            _beaconInitialized = true;
            return;
        }

        // Stop beaconing once the session is fully complete (BlocksOn received in Arrival phase).
        if (telemetryFrame.Phase == FlightPhase.Arrival && _blockTimes.BlocksOnUtc is not null)
            return;

        var hasMovedEnough =
            Math.Abs(telemetryFrame.Latitude  - (_lastLivePositionLatitude  ?? telemetryFrame.Latitude))  > 0.0001 ||
            Math.Abs(telemetryFrame.Longitude - (_lastLivePositionLongitude ?? telemetryFrame.Longitude)) > 0.0001;

        var elapsed = _lastLivePositionSentUtc is null
            ? TimeSpan.MaxValue
            : telemetryFrame.TimestampUtc - _lastLivePositionSentUtc.Value;

        if (!hasMovedEnough && elapsed < TimeSpan.FromSeconds(4))
            return;

        var payload = new LivePositionPayload
        {
            Latitude             = telemetryFrame.Latitude,
            Longitude            = telemetryFrame.Longitude,
            HeadingMagnetic      = telemetryFrame.HeadingMagneticDegrees,
            AltitudeFt           = telemetryFrame.AltitudeFeet,
            AltitudeAglFt        = telemetryFrame.AltitudeAglFeet,
            IndicatedAirspeedKts = telemetryFrame.IndicatedAirspeedKnots,
            GroundSpeedKts       = telemetryFrame.GroundSpeedKnots,
            VerticalSpeedFpm     = telemetryFrame.VerticalSpeedFpm,
            Phase                = telemetryFrame.Phase.ToString(),
            FlightMode           = _context.FlightMode,
            BidId                = string.IsNullOrWhiteSpace(_context.BidId) ? null : _context.BidId,
            Departure            = _context.DepartureAirportIcao,
            Arrival              = _context.ArrivalAirportIcao,
            FlightNumber         = _context.FlightNumber,
            Aircraft             = _context.AircraftType,
            AircraftCategory     = _context.AircraftCategory,
        };

        // Cursor is NOT advanced here — SendLivePositionAsync advances it only on confirmed success.
        // This means a failed send does not suppress the next retry.
        _ = SendLivePositionAsync(
            payload,
            telemetryFrame.TimestampUtc,
            telemetryFrame.Latitude,
            telemetryFrame.Longitude,
            cancellationToken);
    }

    private async Task SendLivePositionAsync(
        LivePositionPayload payload,
        DateTimeOffset timestamp,
        double lat,
        double lon,
        CancellationToken cancellationToken)
    {
        if (_livePositionUploader is null)
            return;

        try
        {
            var ok = await _livePositionUploader.SendPositionAsync(payload, cancellationToken).ConfigureAwait(false);
            if (ok)
            {
                _lastLivePositionSentUtc = timestamp;
                _lastLivePositionLatitude = lat;
                _lastLivePositionLongitude = lon;
                LastSuccessfulUploadUtc = DateTimeOffset.UtcNow;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Trace.TraceWarning("Tracker live position dispatch failed: {0}", ex.Message);
        }
    }

    private static IReadOnlyList<BlockEvent> BuildRestoredBlockEvents(FlightSessionRuntimeState state)
    {
        var events = new List<BlockEvent>(capacity: 4);
        AddBlockEvent(events, BlockEventType.BlocksOff, state.BlockTimes.BlocksOffUtc, state.LastTelemetryFrame);
        AddBlockEvent(events, BlockEventType.WheelsOff, state.BlockTimes.WheelsOffUtc, state.LastTelemetryFrame);
        AddBlockEvent(events, BlockEventType.WheelsOn,  state.BlockTimes.WheelsOnUtc,  state.LastTelemetryFrame);
        AddBlockEvent(events, BlockEventType.BlocksOn,  state.BlockTimes.BlocksOnUtc,  state.LastTelemetryFrame);
        return events;
    }

    private static void AddBlockEvent(
        ICollection<BlockEvent> events,
        BlockEventType type,
        DateTimeOffset? timestampUtc,
        TelemetryFrame? referenceFrame)
    {
        if (timestampUtc is null)
            return;

        events.Add(new BlockEvent
        {
            Type         = type,
            TimestampUtc = timestampUtc.Value,
            LatitudeDeg  = referenceFrame?.Latitude  ?? 0,
            LongitudeDeg = referenceFrame?.Longitude ?? 0,
        });
    }
}
