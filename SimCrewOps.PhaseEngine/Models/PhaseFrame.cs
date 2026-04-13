using SimCrewOps.Scoring.Models;
using SimCrewOps.Tracking.Models;

namespace SimCrewOps.PhaseEngine.Models;

public sealed class PhaseFrame
{
    public required TelemetryFrame Raw { get; init; }
    public FlightPhase Phase { get; init; }

    /// <summary>Non-null only on the frame where a block event fires.</summary>
    public BlockEvent? BlockEvent { get; init; }
}
