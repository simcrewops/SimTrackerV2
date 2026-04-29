using SimCrewOps.Persistence.Models;
using SimCrewOps.Runtime.Models;
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
        var s = state.ScoreInput;

        return new SimSessionUploadRequest
        {
            TrackerVersion    = trackerVersion,
            FlightMode        = state.Context.FlightMode,
            BidId             = NullIfEmpty(state.Context.BidId),
            Departure         = NullIfEmpty(state.Context.DepartureAirportIcao),
            Arrival           = NullIfEmpty(state.Context.ArrivalAirportIcao),
            Aircraft          = NullIfEmpty(state.Context.AircraftType),
            AircraftCategory  = NullIfEmpty(state.Context.AircraftCategory),
            ActualBlocksOff   = state.BlockTimes.BlocksOffUtc,
            ActualWheelsOff   = state.BlockTimes.WheelsOffUtc,
            ActualWheelsOn    = state.BlockTimes.WheelsOnUtc,
            ActualBlocksOn    = state.BlockTimes.BlocksOnUtc,
            BlockTimeActual   = CalculateActualBlockHours(state.BlockTimes),
            BlockTimeScheduled = state.Context.ScheduledBlockHours,
            ScoringInput      = MapScoringInput(s),
            LandingAnalysis   = MapLandingAnalysis(s),
            FlightPath        = MapFlightPath(s.FlightPath),
        };
    }

    private static ScoringInputDto MapScoringInput(FlightScoreInput s) =>
        new()
        {
            Departure = new DepartureScoringDto
            {
                TakeoffPitchDeg  = s.Takeoff.MaxPitchAngleDegrees,
                FlapsAtTakeoff   = s.Takeoff.BounceCount > 0 ? 0 : 0, // placeholder; tracker doesn't capture flaps at rotation
            },
            Climb = new ClimbScoringDto
            {
                AvgClimbFpm      = 0, // placeholder; tracker doesn't compute averages
                VsStabilityScore = 0,
            },
            Cruise = new CruiseScoringDto
            {
                AltitudeDeviationFt = s.Cruise.MaxAltitudeDeviationFeet,
                SpeedDeviationKts   = 0,
            },
            Descent = new DescentScoringDto
            {
                AvgDescentFpm = 0, // placeholder
            },
            Landing = new LandingScoringDto
            {
                // Tracker stores positive magnitude (PR #36 fixed the sign); negate for payload convention
                TouchdownRateFpm    = -(s.Landing.TouchdownVerticalSpeedFpm),
                TouchdownPitchDeg   = s.Landing.TouchdownPitchAngleDegrees,
                MaxPitchWhileWowDeg = s.Landing.MaxPitchWhileWowDegrees,
                TouchdownBankDeg    = s.Landing.TouchdownBankAngleDegrees,
                TouchdownGForce     = s.Landing.TouchdownGForce,
                BounceCount         = s.Landing.BounceCount,
                GearUpAtTouchdown   = s.Landing.GearUpAtTouchdown,
            },
            Safety = new SafetyScoringDto
            {
                CrashDetected        = s.Safety.CrashDetected,
                OverspeedWarningCount = s.Safety.OverspeedEvents,
                StallWarningCount    = s.Safety.StallEvents,
                GpwsAlertCount       = s.Safety.GpwsEvents,
            },
        };

    private static LandingAnalysisDto MapLandingAnalysis(FlightScoreInput s) =>
        new()
        {
            TouchdownLat                  = s.LandingAnalysis.TouchdownLat,
            TouchdownLon                  = s.LandingAnalysis.TouchdownLon,
            TouchdownHeadingDeg           = s.LandingAnalysis.TouchdownHeadingMagneticDeg,
            TouchdownAltFt                = s.LandingAnalysis.TouchdownAltFt,
            TouchdownIAS                  = s.LandingAnalysis.TouchdownIAS,
            WindSpeedAtTouchdownKnots     = s.LandingAnalysis.WindSpeedKnots,
            WindDirectionAtTouchdownDegrees = s.LandingAnalysis.WindDirectionDegrees,
            ApproachPath                  = s.ApproachPath
                .Select(p => new ApproachPathPointDto
                {
                    Lat        = p.Lat,
                    Lon        = p.Lon,
                    AltitudeFt = p.AltFt,
                    IasKts     = p.IasKts,
                    VsFpm      = p.VsFpm,
                })
                .ToArray(),
        };

    private static FlightPathPointDto[] MapFlightPath(IReadOnlyList<FlightPathPoint> points) =>
        points
            .Select(p => new FlightPathPointDto
            {
                Lat   = p.Lat,
                Lon   = p.Lon,
                AltFt = p.AltFt,
                TMin  = p.TMin,
            })
            .ToArray();

    private static double? CalculateActualBlockHours(FlightSessionBlockTimes blockTimes)
    {
        if (blockTimes.BlocksOffUtc is null || blockTimes.BlocksOnUtc is null)
            return null;

        return Math.Round((blockTimes.BlocksOnUtc.Value - blockTimes.BlocksOffUtc.Value).TotalHours, 3);
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
