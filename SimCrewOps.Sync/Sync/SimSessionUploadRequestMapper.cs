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
            // Normalise to 0–100 so the webapp always receives a consistent scale even
            // when runway-data scoring raises the raw maximum above 100.
            ScoreFinal = state.ScoreResult.MaximumScore > 0
                ? Math.Round(state.ScoreResult.FinalScore / state.ScoreResult.MaximumScore * 100.0, 1)
                : state.ScoreResult.FinalScore,
            TrackerVersion = trackerVersion,
            FlightMode = state.Context.FlightMode,
            BidId            = string.IsNullOrWhiteSpace(state.Context.BidId)                ? null : state.Context.BidId,
            Departure        = string.IsNullOrWhiteSpace(state.Context.DepartureAirportIcao) ? null : state.Context.DepartureAirportIcao,
            Arrival          = string.IsNullOrWhiteSpace(state.Context.ArrivalAirportIcao)   ? null : state.Context.ArrivalAirportIcao,
            AircraftType     = string.IsNullOrWhiteSpace(state.Context.AircraftType)          ? null : state.Context.AircraftType,
            AircraftCategory = string.IsNullOrWhiteSpace(state.Context.AircraftCategory)      ? null : state.Context.AircraftCategory,
            TouchdownLat     = state.ScoreInput.Landing.TouchdownLatitude,
            TouchdownLon     = state.ScoreInput.Landing.TouchdownLongitude,
            RunwayIdentifier   = state.LandingRunwayResolution?.Runway.RunwayIdentifier,
            RunwayHeadingTrue  = state.LandingRunwayResolution?.Runway.TrueHeadingDegrees,
            RunwayLengthFt     = state.LandingRunwayResolution?.Runway.LengthFeet,
            RunwayWidthFt      = state.LandingRunwayResolution?.Runway.WidthFeet,
            RunwayThresholdLat = state.LandingRunwayResolution?.Runway.ThresholdLatitude,
            RunwayThresholdLon = state.LandingRunwayResolution?.Runway.ThresholdLongitude,
            TouchdownCenterlineDeviationFt = state.ScoreInput.Landing.TouchdownCenterlineDeviationFeet == 0 ? null : state.ScoreInput.Landing.TouchdownCenterlineDeviationFeet,
            TouchdownCrabAngleDegrees      = state.ScoreInput.Landing.TouchdownCrabAngleDegrees == 0        ? null : state.ScoreInput.Landing.TouchdownCrabAngleDegrees,
            PhaseFindings  = MapPhaseFindings(state.ScoreResult),
            GlobalFindings = MapFindings(state.ScoreResult.GlobalFindings),
            // Session timing
            EnginesStartedAt = state.ScoreInput.Session.EnginesStartedAtUtc,
            WheelsOffAt      = state.ScoreInput.Session.WheelsOffAtUtc,
            WheelsOnAt       = state.ScoreInput.Session.WheelsOnAtUtc,
            EnginesOffAt     = state.ScoreInput.Session.EnginesOffAtUtc,
            // Fuel
            FuelAtDepartureLbs = state.ScoreInput.Session.FuelAtDepartureLbs,
            FuelAtLandingLbs   = state.ScoreInput.Session.FuelAtLandingLbs,
            FuelBurnedLbs      = state.ScoreInput.Session.FuelBurnedLbs,
            // ILS approach quality
            IlsApproachDetected     = state.ScoreInput.Approach.IlsApproachDetected,
            IlsMaxGlideslopeDevDots = state.ScoreInput.Approach.MaxGlideslopeDeviationDots,
            IlsAvgGlideslopeDevDots = state.ScoreInput.Approach.AvgGlideslopeDeviationDots,
            IlsMaxLocalizerDevDots  = state.ScoreInput.Approach.MaxLocalizerDeviationDots,
            IlsAvgLocalizerDevDots  = state.ScoreInput.Approach.AvgLocalizerDeviationDots,
            // Extended touchdown context
            TouchdownAutopilotEngaged = state.ScoreInput.Landing.AutopilotEngagedAtTouchdown,
            TouchdownSpoilersDeployed = state.ScoreInput.Landing.SpoilersDeployedAtTouchdown,
            TouchdownReverseThrustUsed = state.ScoreInput.Landing.ReverseThrustUsed,
            TouchdownWindSpeedKts     = state.ScoreInput.Landing.WindSpeedAtTouchdownKnots,
            TouchdownWindDirectionDeg = state.ScoreInput.Landing.WindDirectionAtTouchdownDegrees,
            TouchdownHeadwindKts      = state.ScoreInput.Landing.HeadwindComponentKnots,
            TouchdownCrosswindKts     = state.ScoreInput.Landing.CrosswindComponentKnots,
            TouchdownOatCelsius       = state.ScoreInput.Landing.OatCelsiusAtTouchdown,
            // GPS track (null when empty — avoids sending an empty array)
            GpsTrack = state.ScoreInput.GpsTrack.Count > 0
                ? state.ScoreInput.GpsTrack
                    .Select(p => new GpsTrackPointUpload
                    {
                        TimestampUtc     = p.TimestampUtc,
                        Latitude         = p.Latitude,
                        Longitude        = p.Longitude,
                        AltitudeFeet     = p.AltitudeFeet,
                        GroundSpeedKnots = p.GroundSpeedKnots,
                        Phase            = p.Phase.ToString(),
                    })
                    .ToList()
                : null,
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
