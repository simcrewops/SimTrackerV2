using SimCrewOps.PhaseEngine.Models;
using SimCrewOps.Tracking.Models;

namespace SimCrewOps.Runtime.Models;

public sealed record RuntimeFrameResult
{
    public required PhaseFrame PhaseFrame { get; init; }
    public required TelemetryFrame EnrichedTelemetryFrame { get; init; }
    public required FlightSessionRuntimeState State { get; init; }

    /// <summary>
    /// True when a teleport/reposition was detected this frame and the session was
    /// automatically reset. The UI should surface a brief notification to the pilot.
    /// </summary>
    public bool WasRepositionReset { get; init; }
}
