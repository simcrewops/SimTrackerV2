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

    /// <summary>
    /// Runway resolved at the start of the Approach phase.
    /// Populated by best-heading-match against the arrival airport — used for live
    /// DIST THR and geometric glidepath calculations during the approach.
    /// Distinct from <see cref="LandingRunwayResolution"/> which is confirmed at WheelsOn
    /// and drives TDZ scoring.
    /// </summary>
    public RunwayResolutionResult? ApproachRunwayResolution { get; init; }

    /// <summary>
    /// Distance from the aircraft to the runway threshold in nautical miles.
    /// Computed each frame while in Approach phase. Null when no approach runway is resolved.
    /// </summary>
    public double? ApproachDistanceNm { get; init; }

    /// <summary>
    /// Geometric glidepath deviation in feet: actual AGL minus ideal AGL on a 3° path.
    /// Positive = above the 3° glidepath, negative = below.
    /// Null when no approach runway is resolved or when not in Approach phase.
    /// </summary>
    public double? GlidepathDeviationFeet { get; init; }

    public required FlightScoreInput ScoreInput { get; init; }
    public required ScoreResult ScoreResult { get; init; }

    public bool HasResolvedLandingRunway => LandingRunwayResolution is not null;
    public bool IsComplete => BlockTimes.BlocksOnUtc is not null;
}
