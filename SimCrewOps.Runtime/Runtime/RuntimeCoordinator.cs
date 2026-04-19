using System.Diagnostics;
using SimCrewOps.PhaseEngine.Models;
using SimCrewOps.PhaseEngine.PhaseEngine;
using SimCrewOps.Runways.Models;
using SimCrewOps.Runways.Services;
using SimCrewOps.Runtime.Models;
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
    private TelemetryFrame? _lastTelemetryFrame;
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

    public void Restore(FlightSessionRuntimeState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        _context = state.Context;
        _blockTimes = state.BlockTimes;
        _landingRunwayResolution = state.LandingRunwayResolution;
        _lastTelemetryFrame = state.LastTelemetryFrame;
        _lastLivePositionSentUtc = null;
        _lastLivePositionLatitude = state.LastTelemetryFrame?.Latitude;
        _lastLivePositionLongitude = state.LastTelemetryFrame?.Longitude;

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
        var enrichedTelemetryFrame = await EnrichTelemetryFrameAsync(phaseFrame, cancellationToken).ConfigureAwait(false);
        var enrichedPhaseFrame = new PhaseFrame
        {
            Raw = enrichedTelemetryFrame,
            Phase = phaseFrame.Phase,
            BlockEvent = phaseFrame.BlockEvent,
        };

        var labelledFrame = enrichedTelemetryFrame with { Phase = enrichedPhaseFrame.Phase };
        _scoringTracker.Ingest(labelledFrame);
        _lastTelemetryFrame = labelledFrame;
        CaptureBlockEvent(enrichedPhaseFrame.BlockEvent);
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
