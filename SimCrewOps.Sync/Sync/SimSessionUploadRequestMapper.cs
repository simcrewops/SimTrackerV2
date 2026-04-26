using SimCrewOps.Persistence.Models;
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
            BidId = string.IsNullOrWhiteSpace(state.Context.BidId) ? null : state.Context.BidId,
            RunwayIdentifier   = state.LandingRunwayResolution?.Runway.RunwayIdentifier,
            RunwayHeadingTrue  = state.LandingRunwayResolution?.Runway.TrueHeadingDegrees,
            RunwayLengthFt     = state.LandingRunwayResolution?.Runway.LengthFeet,
            RunwayWidthFt      = state.LandingRunwayResolution?.Runway.WidthFeet,
            RunwayThresholdLat = state.LandingRunwayResolution?.Runway.ThresholdLatitude,
            RunwayThresholdLon = state.LandingRunwayResolution?.Runway.ThresholdLongitude,
            Aircraft          = string.IsNullOrWhiteSpace(state.Context.AircraftType)     ? null : state.Context.AircraftType,
            AircraftCategory  = string.IsNullOrWhiteSpace(state.Context.AircraftCategory) ? null : state.Context.AircraftCategory,
            TouchdownCenterlineDeviationFt = state.ScoreInput.Landing.TouchdownCenterlineDeviationFeet == 0 ? null : state.ScoreInput.Landing.TouchdownCenterlineDeviationFeet,
            TouchdownCrabAngleDegrees      = state.ScoreInput.Landing.TouchdownCrabAngleDegrees == 0 ? null : state.ScoreInput.Landing.TouchdownCrabAngleDegrees,
        };
    }

    private static double? CalculateActualBlockHours(SimCrewOps.Runtime.Models.FlightSessionBlockTimes blockTimes)
    {
        if (blockTimes.BlocksOffUtc is null || blockTimes.BlocksOnUtc is null)
        {
            return null;
        }

        return Math.Round((blockTimes.BlocksOnUtc.Value - blockTimes.BlocksOffUtc.Value).TotalHours, 4);
    }
}
