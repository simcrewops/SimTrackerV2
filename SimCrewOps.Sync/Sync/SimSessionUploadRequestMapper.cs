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
            TrackerVersion   = trackerVersion,
            FlightMode       = state.Context.FlightMode,
            BidId            = string.IsNullOrWhiteSpace(state.Context.BidId)                ? null : state.Context.BidId,
            Departure        = string.IsNullOrWhiteSpace(state.Context.DepartureAirportIcao) ? null : state.Context.DepartureAirportIcao,
            Arrival          = string.IsNullOrWhiteSpace(state.Context.ArrivalAirportIcao)   ? null : state.Context.ArrivalAirportIcao,
            AircraftType     = string.IsNullOrWhiteSpace(state.Context.AircraftType)          ? null : state.Context.AircraftType,
            AircraftCategory = string.IsNullOrWhiteSpace(state.Context.AircraftCategory)      ? null : state.Context.AircraftCategory,
            // Block times
            ActualBlocksOff  = state.BlockTimes.BlocksOffUtc,
            ActualWheelsOff  = state.BlockTimes.WheelsOffUtc,
            ActualWheelsOn   = state.BlockTimes.WheelsOnUtc,
            ActualBlocksOn   = state.BlockTimes.BlocksOnUtc,
            BlockTimeActual  = CalculateActualBlockHours(state.BlockTimes),
            BlockTimeScheduled = state.Context.ScheduledBlockHours,
            // Session timing
            EnginesStartedAt = state.ScoreInput.Session.EnginesStartedAtUtc,
            WheelsOffAt      = state.ScoreInput.Session.WheelsOffAtUtc,
            WheelsOnAt       = state.ScoreInput.Session.WheelsOnAtUtc,
            EnginesOffAt     = state.ScoreInput.Session.EnginesOffAtUtc,
            // Fuel
            FuelAtDepartureLbs = state.ScoreInput.Session.FuelAtDepartureLbs,
            FuelAtLandingLbs   = state.ScoreInput.Session.FuelAtLandingLbs,
            FuelBurnedLbs      = state.ScoreInput.Session.FuelBurnedLbs,
            // Touchdown position
            TouchdownLat     = state.ScoreInput.Landing.TouchdownLatitude,
            TouchdownLon     = state.ScoreInput.Landing.TouchdownLongitude,
            // Crash flag
            CrashDetected    = state.ScoreInput.Safety.CrashDetected,
            // GPS track (null when empty — avoids sending an empty JSON array)
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
            // Structured raw phase metrics for webapp scoring
            ScoreInputV5 = MapScoreInputV5(state.ScoreInput),
            // Landing analysis — omit entirely when no approach path was recorded
            LandingAnalysis = state.ScoreInput.ApproachPath.Count > 0
                ? MapLandingAnalysis(state.ScoreInput.ApproachPath, state.ScoreInput.Landing)
                : null,
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

    private static FlightScoreInputV5Upload MapScoreInputV5(FlightScoreInput s) =>
        new()
        {
            TaxiOut = new ScoreInputTaxiV5
            {
                MaxGroundSpeedKts      = s.TaxiOut.MaxGroundSpeedKnots,
                MaxTurnSpeedKts        = s.TaxiOut.MaxTurnSpeedKnots,
                NavLightsOn            = s.TaxiOut.TaxiLightsOn,
                StrobeLightOnDuringTaxi = s.TaxiOut.StrobeLightOnDuringTaxi,
            },
            TaxiIn = new ScoreInputTaxiInV5
            {
                MaxGroundSpeedKts      = s.TaxiIn.MaxGroundSpeedKnots,
                MaxTurnSpeedKts        = s.TaxiIn.MaxTurnSpeedKnots,
                NavLightsOn            = s.TaxiIn.TaxiLightsOn,
                StrobeLightOnDuringTaxi = s.TaxiIn.StrobeLightOnDuringTaxi,
                SmoothDeceleration     = s.TaxiIn.SmoothDeceleration,
            },
            Takeoff = new ScoreInputTakeoffV5
            {
                BounceOnRotation           = s.Takeoff.BounceCount > 0,
                PositiveRateBeforeGearUp   = s.Takeoff.PositiveRateBeforeGearUp,
                MaxBankBelow1000AglDeg     = s.Takeoff.MaxBankAngleDegrees,
                MaxPitchWhileWowDeg        = s.Takeoff.MaxPitchAngleDegrees,
                StrobeLightsOn             = s.Takeoff.StrobesOnFromTakeoffToLanding,
                LandingLightsOn            = s.Takeoff.LandingLightsOnBeforeTakeoff,
                GForceAtRotation           = s.Takeoff.GForceAtRotation,
            },
            Climb = new ScoreInputClimbV5
            {
                IsHeavy                    = s.Climb.HeavyFourEngineAircraft,
                MaxIasBelowFl100Kts        = s.Climb.MaxIasBelowFl100Knots,
                MaxBankDeg                 = s.Climb.MaxBankAngleDegrees,
                MinGForce                  = s.Climb.MinGForce,
                MaxGForce                  = s.Climb.MaxGForce,
                LandingLightsOffAboveFL180 = s.Climb.LandingLightsOffAboveFL180,
            },
            Cruise = new ScoreInputCruiseV5
            {
                CruiseAltitudeFt       = s.Cruise.CruiseAltitudeFeet,
                MaxAltitudeDeviationFt = s.Cruise.MaxAltitudeDeviationFeet,
                MachTarget             = s.Cruise.MachTarget,
                MaxMachDeviation       = s.Cruise.MaxMachDeviation,
                IasTarget              = s.Cruise.IasTarget,
                MaxIasDeviationKts     = s.Cruise.MaxIasDeviationKnots,
                SpeedInstabilityEvents = s.Cruise.SpeedInstabilityEvents,
                MaxBankDeg             = s.Cruise.MaxBankAngleDegrees,
                MaxTurnBankDeg         = s.Cruise.MaxTurnBankAngleDegrees,
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
                LandingLightsOnBeforeFL180 = s.Descent.LandingLightsOnBeforeFL180,
                MaxNoseDownPitchDeg        = s.Descent.MaxNoseDownPitchDegrees,
            },
            Approach = new ScoreInputApproachV5
            {
                ApproachSpeedKts         = s.Approach.ApproachSpeedKnots,
                MaxIasDeviationKts       = s.Approach.MaxIasDeviationKnots,
                GearDownAglFt            = s.Approach.GearDownAglFeet,
                FlapsConfiguredBy1000Agl = s.Approach.FlapsConfiguredBy1000Agl,
                MaxBankDeg               = s.Approach.MaxBankAngleDegrees3000to500,
                IlsDetected              = s.Approach.IlsApproachDetected,
                IlsMaxGlideslopeDevDots  = s.Approach.MaxGlideslopeDeviationDots,
                IlsAvgGlideslopeDevDots  = s.Approach.AvgGlideslopeDeviationDots,
                IlsMaxLocalizerDevDots   = s.Approach.MaxLocalizerDeviationDots,
                IlsAvgLocalizerDevDots   = s.Approach.AvgLocalizerDeviationDots,
            },
            StabilizedApproach = new ScoreInputStabilizedApproachV5
            {
                ApproachSpeedKts       = s.StabilizedApproach.ApproachSpeedKnots,
                MaxIasDeviationKts     = s.StabilizedApproach.MaxIasDeviationKnots,
                MaxDescentRateFpm      = s.StabilizedApproach.MaxDescentRateFpm,
                ConfigChanged          = s.StabilizedApproach.ConfigChanged,
                MaxHeadingDeviationDeg = s.StabilizedApproach.MaxHeadingDeviationDegrees,
                IlsAvailable           = s.StabilizedApproach.IlsAvailable,
                MaxGlideslopeDevDots   = s.StabilizedApproach.MaxGlideslopeDeviationDots,
                PitchAtGateDeg             = s.StabilizedApproach.PitchAtGateDegrees,
            },
            Landing = new ScoreInputLandingV5
            {
                TouchdownRateFpm   = s.Landing.TouchdownVerticalSpeedFpm,
                TouchdownGForce    = s.Landing.TouchdownGForce,
                BounceCount        = s.Landing.BounceCount,
                TouchdownBankDeg   = s.Landing.TouchdownBankAngleDegrees,
                GearUpAtTouchdown  = s.Landing.GearUpAtTouchdown,
                MaxPitchWhileWowDeg = s.Landing.MaxPitchDuringRolloutDegrees,
            },
            LightsSystems = new ScoreInputLightsSystemsV5
            {
                BeaconOnThroughoutFlight   = s.LightsSystems.BeaconOnThroughoutFlight,
                NavLightsOnThroughoutFlight = s.LightsSystems.NavLightsOnThroughoutFlight,
                StrobesCorrect             = s.LightsSystems.StrobesCorrect,
                LandingLightsCompliance    = s.LightsSystems.LandingLightsCompliance,
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
            Arrival = new ScoreInputArrivalV5
            {
                EnginesOffAfterParkingBrake = s.Arrival.EnginesOffAfterParkingBrake,
                BeaconOffAfterEngines       = s.Arrival.BeaconOffAfterEngines,
            },
        };

    private static LandingAnalysisUpload MapLandingAnalysis(
        IReadOnlyList<SimCrewOps.Scoring.Models.ApproachSamplePoint> approachPath,
        SimCrewOps.Scoring.Models.LandingMetrics landing) =>
        new()
        {
            ApproachPath = approachPath
                .Select(s => new ApproachSampleUpload
                {
                    DistanceToThresholdNm = s.DistanceToThresholdNm,
                    AltitudeFt            = s.AltitudeFeet,
                    IasKts                = s.IndicatedAirspeedKnots,
                    VsFpm                 = s.VerticalSpeedFpm,
                })
                .ToList(),
            TouchdownLat       = landing.TouchdownLatitude != 0 ? landing.TouchdownLatitude : null,
            TouchdownLon       = landing.TouchdownLongitude != 0 ? landing.TouchdownLongitude : null,
            TouchdownHeadingDeg = landing.TouchdownHeadingDegrees != 0 ? landing.TouchdownHeadingDegrees : null,
        };
}
