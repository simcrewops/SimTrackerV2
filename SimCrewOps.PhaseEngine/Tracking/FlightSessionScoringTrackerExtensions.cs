using SimCrewOps.PhaseEngine.Models;
using SimCrewOps.Tracking.Tracking;

namespace SimCrewOps.PhaseEngine.Tracking;

/// <summary>
/// Extension methods that let <see cref="FlightSessionScoringTracker"/> consume
/// <see cref="PhaseFrame"/> objects produced by <see cref="PhaseEngine.FlightPhaseEngine"/>.
/// Kept as extensions (rather than modifying the tracker directly) to avoid a
/// circular project reference between Tracking and PhaseEngine.
/// </summary>
public static class FlightSessionScoringTrackerExtensions
{
    /// <summary>
    /// Ingest a <see cref="PhaseFrame"/> into the tracker.  The engine-assigned phase
    /// is stamped onto the raw frame before forwarding, so the tracker sees a fully
    /// labelled <c>TelemetryFrame</c> without the caller needing to set it manually.
    /// </summary>
    public static void Ingest(this FlightSessionScoringTracker tracker, PhaseFrame phaseFrame)
    {
        var labelledFrame = phaseFrame.Raw with { Phase = phaseFrame.Phase };
        tracker.Ingest(labelledFrame);
    }
}
