using SimCrewOps.PhaseEngine.Models;
using SimCrewOps.Runways.Models;
using SimCrewOps.Tracking.Models;

namespace SimCrewOps.Runtime.Models;

public sealed record RuntimeFrameResult
{
    public required PhaseFrame PhaseFrame { get; init; }
    public required TelemetryFrame EnrichedTelemetryFrame { get; init; }
    public RunwayResolutionResult? RunwayResolution { get; init; }
    public required FlightSessionRuntimeState State { get; init; }
}
