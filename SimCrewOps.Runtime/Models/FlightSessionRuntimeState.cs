using SimCrewOps.Runways.Models;
using SimCrewOps.Scoring.Models;
using SimCrewOps.Tracking.Models;

namespace SimCrewOps.Runtime.Models;

public sealed record FlightSessionRuntimeState
{
    public required FlightSessionContext Context { get; init; }
    public required FlightPhase CurrentPhase { get; init; }
    public required FlightSessionBlockTimes BlockTimes { get; init; }
    public TelemetryFrame? LastTelemetryFrame { get; init; }
    public RunwayResolutionResult? LandingRunwayResolution { get; init; }
    public required FlightScoreInput ScoreInput { get; init; }
    public required ScoreResult ScoreResult { get; init; }

    public bool HasResolvedLandingRunway => LandingRunwayResolution is not null;
    public bool IsComplete => BlockTimes.BlocksOnUtc is not null;
}
