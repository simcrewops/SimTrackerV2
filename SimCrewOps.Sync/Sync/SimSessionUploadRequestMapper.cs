using SimCrewOps.Persistence.Models;
using SimCrewOps.Scoring.Models;
using SimCrewOps.Sync.Models;

namespace SimCrewOps.Sync.Sync;

public sealed class SimSessionUploadRequestMapper
{
    public SimSessionUploadRequest Map(PendingCompletedSession session, string trackerVersion)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(trackerVersion);

        var state = session.State;

        return new SimSessionUploadRequest
        {
            Bounces = state.ScoreInput.Landing.BounceCount,
            TouchdownVS = state.ScoreInput.Landing.TouchdownVerticalSpeedFpm,
            TouchdownBank = state.ScoreInput.Landing.TouchdownBankAngleDegrees,
            TouchdownIAS = state.ScoreInput.Landing.TouchdownIndicatedAirspeedKnots,
            TouchdownPitch = state.ScoreInput.Landing.TouchdownPitchAngleDegrees,
            ActualBlocksOff = state.BlockTimes.BlocksOffUtc,
            ActualWheelsOff = state.BlockTimes.WheelsOffUtc,
            ActualWheelsOn = state.BlockTimes.WheelsOnUtc,
            ActualBlocksOn = state.BlockTimes.BlocksOnUtc,
            BlockTimeActual = CalculateActualBlockHours(state.BlockTimes),
            BlockTimeScheduled = state.Context.ScheduledBlockHours,
            CrashDetected = state.ScoreInput.Safety.CrashDetected,
            OverspeedEvents = state.ScoreInput.Safety.OverspeedEvents,
            StallEvents = state.ScoreInput.Safety.StallEvents,
            GpwsEvents = state.ScoreInput.Safety.GpwsEvents,
            Grade = state.ScoreResult.Grade,
            ScoreFinal = state.ScoreResult.FinalScore,
            TrackerVersion = trackerVersion,
            FlightMode = state.Context.FlightMode,
            BidId      = string.IsNullOrWhiteSpace(state.Context.BidId) ? null : state.Context.BidId,
            Departure       = string.IsNullOrWhiteSpace(state.Context.DepartureAirportIcao) ? null : state.Context.DepartureAirportIcao,
            Arrival         = string.IsNullOrWhiteSpace(state.Context.ArrivalAirportIcao)   ? null : state.Context.ArrivalAirportIcao,
            AircraftType    = string.IsNullOrWhiteSpace(state.Context.AircraftType)         ? null : state.Context.AircraftType,
            AircraftCategory = string.IsNullOrWhiteSpace(state.Context.AircraftCategory)    ? null : state.Context.AircraftCategory,
            PhaseFindings  = MapPhaseFindings(state.ScoreResult),
            GlobalFindings = MapFindings(state.ScoreResult.GlobalFindings),
        };
    }

    private static IReadOnlyList<PhaseScoreFindingUpload> MapPhaseFindings(ScoreResult scoreResult) =>
        scoreResult.PhaseScores
            .Select(p => new PhaseScoreFindingUpload
            {
                Phase         = p.Phase.ToString(),
                MaxPoints     = p.MaxPoints,
                AwardedPoints = p.AwardedPoints,
                Findings      = MapFindings(p.Findings),
            })
            .ToList();

    private static IReadOnlyList<ScoreFindingUpload> MapFindings(IReadOnlyList<ScoreFinding> findings) =>
        findings
            .Where(f => f.PointsDeducted > 0 || f.IsAutomaticFail)
            .Select(f => new ScoreFindingUpload
            {
                Description     = f.Description,
                PointsDeducted  = f.PointsDeducted,
                IsAutomaticFail = f.IsAutomaticFail,
            })
            .ToList();

    private static double? CalculateActualBlockHours(SimCrewOps.Runtime.Models.FlightSessionBlockTimes blockTimes)
    {
        if (blockTimes.BlocksOffUtc is null || blockTimes.BlocksOnUtc is null)
        {
            return null;
        }

        return Math.Round((blockTimes.BlocksOnUtc.Value - blockTimes.BlocksOffUtc.Value).TotalHours, 4);
    }
}
