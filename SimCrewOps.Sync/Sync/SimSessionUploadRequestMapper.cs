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
                // V1/Vr/V2 are not observable from MSFS SimConnect telemetry — sent null.
                TakeoffPitchDeg  = s.Takeoff.MaxPitchAngleDegrees,
                FlapsAtTakeoff   = s.Takeoff.FlapsHandleIndexAtLiftoff,
                InitialClimbFpm  = s.Takeoff.InitialClimbFpm,
            },
            Climb = new ClimbScoringDto
            {
                AvgClimbFpm      = s.Climb.AvgClimbFpm,
                TimeToFL100Min   = s.Climb.TimeToFL100Minutes,
                VsStabilityScore = s.Climb.VsStabilityScore,
            },
            Cruise = new CruiseScoringDto
            {
                AltitudeDeviationFt = s.Cruise.MaxAltitudeDeviationFeet,
                SpeedDeviationKts   = s.Cruise.MaxSpeedDeviationKts,
            },
            Descent = new DescentScoringDto
            {
                AvgDescentFpm   = s.Descent.AvgDescentFpm,
                SpeedAtFL100Kts = s.Descent.SpeedAtFL100Kts,
            },
            Landing = new LandingScoringDto
            {
                // Tracker stores positive magnitude; negate for payload convention (negative = descending).
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

    private static LandingAnalysisDto MapLandingAnalysis(FlightScoreInput s)
    {
        var tdLat = s.LandingAnalysis.TouchdownLat;
        var tdLon = s.LandingAnalysis.TouchdownLon;
        var hasTouch = tdLat.HasValue && tdLon.HasValue;

        return new LandingAnalysisDto
        {
            TouchdownLat                    = tdLat,
            TouchdownLon                    = tdLon,
            TouchdownHeadingDeg             = s.LandingAnalysis.TouchdownHeadingMagneticDeg,
            TouchdownAltFt                  = s.LandingAnalysis.TouchdownAltFt,
            TouchdownIAS                    = s.LandingAnalysis.TouchdownIAS,
            WindSpeedAtTouchdownKnots       = s.LandingAnalysis.WindSpeedKnots,
            WindDirectionAtTouchdownDegrees = s.LandingAnalysis.WindDirectionDegrees,
            ApproachPath                    = s.ApproachPath
                .Select(p => new ApproachPathPointDto
                {
                    Lat        = p.Lat,
                    Lon        = p.Lon,
                    AltitudeFt = p.AltFt,
                    IasKts     = p.IasKts,
                    VsFpm      = p.VsFpm,
                    DistanceToThresholdNm = hasTouch
                        ? HaversineNm(p.Lat, p.Lon, tdLat!.Value, tdLon!.Value)
                        : null,
                })
                .ToArray(),
        };
    }

    private static double HaversineNm(double lat1, double lon1, double lat2, double lon2)
    {
        const double EarthRadiusNm = 3440.065;
        const double DegToRad = Math.PI / 180.0;
        var dLat = (lat2 - lat1) * DegToRad;
        var dLon = (lon2 - lon1) * DegToRad;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1 * DegToRad) * Math.Cos(lat2 * DegToRad)
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return EarthRadiusNm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

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
