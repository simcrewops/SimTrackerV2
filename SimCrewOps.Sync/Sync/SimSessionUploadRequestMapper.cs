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

    private static FlightScoreInputV5Upload MapScoringInput(FlightScoreInput s) =>
        new()
        {
            Preflight = new ScoreInputPreflightV5
            {
                BeaconOnBeforeTaxi = s.Preflight.BeaconOnBeforeTaxi,
            },
            TaxiOut = new ScoreInputTaxiV5
            {
                MaxGroundSpeedKts       = s.TaxiOut.MaxGroundSpeedKnots,
                MaxTurnSpeedKts         = s.TaxiOut.MaxTurnSpeedKnots,
                NavLightsOn             = s.TaxiOut.NavLightsOn,
                StrobeLightOnDuringTaxi = !s.TaxiOut.StrobesOff,
            },
            Takeoff = new ScoreInputTakeoffV5
            {
                BounceOnRotation         = s.Takeoff.BounceCount > 0,
                PositiveRateBeforeGearUp = s.Takeoff.PositiveRateBeforeGearUp,
                MaxBankBelow1000AglDeg   = s.Takeoff.MaxBankAngleDegrees,
                MaxPitchWhileWowDeg      = s.Takeoff.MaxPitchWhileWowDegrees,
                MaxPitchAglFt            = s.Takeoff.MaxPitchAglFt,
                StrobeLightsOn           = s.Takeoff.StrobesOnFromTakeoffToLanding,
                LandingLightsOn          = s.Takeoff.LandingLightsOnBeforeTakeoff,
                GForceAtRotation         = s.Takeoff.GForceAtRotation,
            },
            Climb = new ScoreInputClimbV5
            {
                IsHeavy                    = s.Climb.HeavyFourEngineAircraft,
                MaxIasBelowFl100Kts        = s.Climb.MaxIasBelowFl100Knots,
                MaxBankDeg                 = s.Climb.MaxBankAngleDegrees,
                MinGForce                  = s.Climb.MinGForce,
                MaxGForce                  = s.Climb.MaxGForce,
                LandingLightsOffAboveFL180 = s.Takeoff.LandingLightsOffByFl180,
            },
            Cruise = new ScoreInputCruiseV5
            {
                CruiseAltitudeFt       = s.Cruise.CruiseTargetAltitudeFt,
                MaxAltitudeDeviationFt = s.Cruise.MaxAltitudeDeviationFeet,
                MachTarget             = s.Cruise.MachTarget,
                MaxMachDeviation       = s.Cruise.MaxMachDeviation,
                IasTarget              = s.Cruise.IasTarget,
                MaxIasDeviationKts     = s.Cruise.MaxSpeedDeviationKts,
                SpeedInstabilityEvents = s.Cruise.SpeedInstabilityEvents,
                MaxBankDeg             = s.Cruise.LevelMaxBankDegrees,
                MaxTurnBankDeg         = s.Cruise.TurnMaxBankDegrees,
                MinGForce              = s.Cruise.MinGForce,
                MaxGForce              = s.Cruise.MaxGForce,
            },
            Descent = new ScoreInputDescentV5
            {
                IsHeavy                    = s.Climb.HeavyFourEngineAircraft,
                MaxIasBelowFl100Kts        = s.Descent.MaxIasBelowFl100Knots,
                MaxBankDeg                 = s.Descent.MaxBankAngleDegrees,
                MinGForce                  = s.Descent.MinGForce,
                MaxGForce                  = s.Descent.MaxGForce,
                MaxDescentRateFpm          = s.Descent.MaxDescentRateFpm,
                LandingLightsOnBeforeFL180 = s.Descent.LandingLightsOnByFl180,
                MaxNoseDownPitchDeg        = s.Descent.MaxNoseDownPitchDeg,
            },
            Approach = new ScoreInputApproachV5
            {
                ApproachSpeedKts         = s.Approach.ApproachSpeedKts,
                MaxIasDeviationKts       = s.Approach.MaxIasDeviationKts,
                GearDownAglFt            = s.Approach.GearDownAglFt,
                FlapsConfiguredBy1000Agl = s.Approach.FlapsConfiguredBy1000Agl,
                MaxBankDeg               = s.Approach.MaxBankDegrees,
                // ILS fields default to 0/false until SimConnect ILS SimVars are wired.
            },
            StabilizedApproach = new ScoreInputStabilizedApproachV5
            {
                ApproachSpeedKts       = s.StabilizedApproach.ApproachSpeedKts,
                MaxIasDeviationKts     = s.StabilizedApproach.MaxIasDeviationKts,
                MaxDescentRateFpm      = s.StabilizedApproach.MaxDescentRateFpm,
                ConfigChanged          = s.StabilizedApproach.ConfigChanged,
                MaxHeadingDeviationDeg = s.StabilizedApproach.MaxHeadingDeviationDeg,
                IlsAvailable           = s.StabilizedApproach.IlsAvailable,
                MaxGlideslopeDevDots   = s.StabilizedApproach.MaxGlideslopeDevDots,
                PitchAtGateDeg         = s.StabilizedApproach.PitchAtGateDeg,
            },
            Landing = new ScoreInputLandingV5
            {
                // Tracker stores positive magnitude; negate for payload convention (negative = descending).
                TouchdownRateFpm    = -(s.Landing.TouchdownVerticalSpeedFpm),
                TouchdownGForce     = s.Landing.TouchdownGForce,
                BounceCount         = s.Landing.BounceCount,
                TouchdownBankDeg    = s.Landing.TouchdownBankAngleDegrees,
                GearUpAtTouchdown   = s.Landing.GearUpAtTouchdown,
                MaxPitchWhileWowDeg = s.Landing.MaxPitchWhileWowDegrees,
                TouchdownPitchDeg   = s.Landing.TouchdownPitchAngleDegrees,
            },
            TaxiIn = new ScoreInputTaxiInV5
            {
                MaxGroundSpeedKts       = s.TaxiIn.MaxGroundSpeedKnots,
                MaxTurnSpeedKts         = s.TaxiIn.MaxTurnSpeedKnots,
                NavLightsOn             = s.TaxiIn.NavLightsOn,
                StrobeLightOnDuringTaxi = !s.TaxiIn.StrobesOff,
                SmoothDeceleration      = s.TaxiIn.SmoothDeceleration,
            },
            LightsSystems = new ScoreInputLightsSystemsV5
            {
                BeaconOnThroughoutFlight    = s.LightsSystems.BeaconOnThroughoutFlight,
                NavLightsOnThroughoutFlight = s.LightsSystems.NavLightsOnThroughoutFlight,
                StrobesCorrect              = s.LightsSystems.StrobesCorrect,
                LandingLightsCompliance     = s.LightsSystems.LandingLightsCompliance,
            },
            Safety = new ScoreInputSafetyV5
            {
                CrashDetected            = s.Safety.CrashDetected,
                OverspeedWarningCount    = s.Safety.OverspeedEvents,
                SustainedOverspeedEvents = s.Safety.SustainedOverspeedEvents,
                StallWarningCount        = s.Safety.StallEvents,
                GpwsAlertCount           = s.Safety.GpwsEvents,
                EngineShutdownsInFlight  = s.Safety.EngineShutdownsInFlight,
                EngineShutdownInFlight   = s.Safety.EngineShutdownsInFlight > 0,
            },
            Arrival = s.Arrival.ArrivalReached ? new ScoreInputArrivalV5
            {
                EnginesOffAfterParkingBrake = !s.Arrival.ParkingBrakeSetBeforeAllEnginesShutdown,
                BeaconOffAfterEngines       = s.Arrival.BeaconOffAfterEngines,
            } : null,
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
