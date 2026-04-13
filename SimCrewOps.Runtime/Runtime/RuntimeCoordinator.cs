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
    private readonly FlightSessionContext _context;
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
}
