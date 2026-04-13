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

    private FlightSessionBlockTimes _blockTimes = new();
    private RunwayResolutionResult? _landingRunwayResolution;
    private TelemetryFrame? _lastTelemetryFrame;

    public RuntimeCoordinator(
        FlightSessionContext context,
        RunwayResolver runwayResolver,
        FlightPhaseEngine? phaseEngine = null,
        FlightSessionScoringTracker? scoringTracker = null,
        ScoringEngine? scoringEngine = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(runwayResolver);

        _context = context;
        _runwayResolver = runwayResolver;
        _phaseEngine = phaseEngine ?? new FlightPhaseEngine();
        _scoringTracker = scoringTracker ?? new FlightSessionScoringTracker(context.Profile);
        _scoringEngine = scoringEngine ?? new ScoringEngine();
    }

    public void Restore(FlightSessionRuntimeState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        _context = state.Context;
        _blockTimes = state.BlockTimes;
        _landingRunwayResolution = state.LandingRunwayResolution;
        _lastTelemetryFrame = state.LastTelemetryFrame;

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
