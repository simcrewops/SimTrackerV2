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
            // Structured raw phase metrics for webapp scoring
            ScoreInputV5 = MapScoreInputV5(state.ScoreInput),
            // Landing analysis — omit entirely when no approach path was recorded
            LandingAnalysis = state.ScoreInput.ApproachPath.Count > 0
                ? MapLandingAnalysis(state.ScoreInput.ApproachPath)
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

    private static FlightScoreInputV5Upload MapScoreInputV5(FlightScoreInput s) =>
        new()
        {
            Preflight = new ScoreInputPreflightV5
            {
                BeaconOnBeforeTaxi = s.Preflight.BeaconOnBeforeTaxi,
            },
            TaxiOut = new ScoreInputTaxiV5
            {
                MaxGroundSpeedKts      = s.TaxiOut.MaxGroundSpeedKnots,
                ExcessiveTurnSpeedEvents = s.TaxiOut.ExcessiveTurnSpeedEvents,
                TaxiLightsOn           = s.TaxiOut.TaxiLightsOn,
            },
            Takeoff = new ScoreInputTakeoffV5
            {
                Bounces                     = s.Takeoff.BounceCount,
                TailStrike                  = s.Takeoff.TailStrikeDetected,
                MaxBankDeg                  = s.Takeoff.MaxBankAngleDegrees,
                MaxPitchDeg                 = s.Takeoff.MaxPitchAngleDegrees,
                MaxGForce                   = s.Takeoff.MaxGForce,
                LandingLightsOnBeforeTakeoff = s.Takeoff.LandingLightsOnBeforeTakeoff,
                LandingLightsOffByFl180     = s.Takeoff.LandingLightsOffByFl180,
                StrobesOn                   = s.Takeoff.StrobesOnFromTakeoffToLanding,
            },
            Climb = new ScoreInputClimbV5
            {
                MaxIasBelowFl100Kts = s.Climb.MaxIasBelowFl100Knots,
                MaxBankDeg          = s.Climb.MaxBankAngleDegrees,
                MaxGForce           = s.Climb.MaxGForce,
            },
            Cruise = new ScoreInputCruiseV5
            {
                MaxAltitudeDeviationFt     = s.Cruise.MaxAltitudeDeviationFeet,
                NewFlightLevelCaptureSeconds = s.Cruise.NewFlightLevelCaptureSeconds,
                SpeedInstabilityEvents     = s.Cruise.SpeedInstabilityEvents,
                MaxBankDeg                 = s.Cruise.MaxBankAngleDegrees,
                MaxGForce                  = s.Cruise.MaxGForce,
            },
            Descent = new ScoreInputDescentV5
            {
                MaxIasBelowFl100Kts  = s.Descent.MaxIasBelowFl100Knots,
                MaxBankDeg           = s.Descent.MaxBankAngleDegrees,
                MaxPitchDeg          = s.Descent.MaxPitchAngleDegrees,
                MaxGForce            = s.Descent.MaxGForce,
                LandingLightsOnBy9900 = s.Descent.LandingLightsOnBy9900,
            },
            Approach = new ScoreInputApproachV5
            {
                GearDownBy1000Agl         = s.Approach.GearDownBy1000Agl,
                FlapsIndexAt500Agl        = s.Approach.FlapsHandleIndexAt500Agl,
                VsAt500AglFpm             = s.Approach.VerticalSpeedAt500AglFpm,
                BankAt500AglDeg           = s.Approach.BankAngleAt500AglDegrees,
                PitchAt500AglDeg          = s.Approach.PitchAngleAt500AglDegrees,
                GearDownAt500Agl          = s.Approach.GearDownAt500Agl,
                IlsDetected               = s.Approach.IlsApproachDetected,
                IlsMaxGlideslopeDevDots   = s.Approach.MaxGlideslopeDeviationDots,
                IlsAvgGlideslopeDevDots   = s.Approach.AvgGlideslopeDeviationDots,
                IlsMaxLocalizerDevDots    = s.Approach.MaxLocalizerDeviationDots,
                IlsAvgLocalizerDevDots    = s.Approach.AvgLocalizerDeviationDots,
            },
            Landing = new ScoreInputLandingV5
            {
                Bounces                    = s.Landing.BounceCount,
                TdzExcessFt                = s.Landing.TouchdownZoneExcessDistanceFeet,
                TouchdownVsFpm             = s.Landing.TouchdownVerticalSpeedFpm,
                TouchdownBankDeg           = s.Landing.TouchdownBankAngleDegrees,
                TouchdownIasKts            = s.Landing.TouchdownIndicatedAirspeedKnots,
                TouchdownPitchDeg          = s.Landing.TouchdownPitchAngleDegrees,
                TouchdownGForce            = s.Landing.TouchdownGForce,
                CenterlineDeviationFt      = s.Landing.TouchdownCenterlineDeviationFeet,
                CrabAngleDeg               = s.Landing.TouchdownCrabAngleDegrees,
                TouchdownLat               = s.Landing.TouchdownLatitude,
                TouchdownLon               = s.Landing.TouchdownLongitude,
                AutopilotEngaged           = s.Landing.AutopilotEngagedAtTouchdown,
                SpoilersDeployed           = s.Landing.SpoilersDeployedAtTouchdown,
                ReverseThrustUsed          = s.Landing.ReverseThrustUsed,
                WindSpeedKts               = s.Landing.WindSpeedAtTouchdownKnots,
                WindDirectionDeg           = s.Landing.WindDirectionAtTouchdownDegrees,
                HeadwindKts                = s.Landing.HeadwindComponentKnots,
                CrosswindKts               = s.Landing.CrosswindComponentKnots,
                OatCelsius                 = s.Landing.OatCelsiusAtTouchdown,
            },
            TaxiIn = new ScoreInputTaxiV5
            {
                MaxGroundSpeedKts        = s.TaxiIn.MaxGroundSpeedKnots,
                ExcessiveTurnSpeedEvents = s.TaxiIn.ExcessiveTurnSpeedEvents,
                TaxiLightsOn             = s.TaxiIn.TaxiLightsOn,
            },
            Arrival = new ScoreInputArrivalV5
            {
                TaxiLightsOffBeforeParkingBrakeSet    = s.Arrival.TaxiLightsOffBeforeParkingBrakeSet,
                AllEnginesOffBeforeParkingBrakeSet    = s.Arrival.AllEnginesOffBeforeParkingBrakeSet,
                AllEnginesOffByEndOfSession           = s.Arrival.AllEnginesOffByEndOfSession,
            },
            Safety = new ScoreInputSafetyV5
            {
                CrashDetected            = s.Safety.CrashDetected,
                OverspeedEvents          = s.Safety.OverspeedEvents,
                SustainedOverspeedEvents = s.Safety.SustainedOverspeedEvents,
                StallEvents              = s.Safety.StallEvents,
                GpwsEvents               = s.Safety.GpwsEvents,
                EngineShutdownsInFlight  = s.Safety.EngineShutdownsInFlight,
            },
        };

    private static LandingAnalysisUpload MapLandingAnalysis(
        IReadOnlyList<SimCrewOps.Scoring.Models.ApproachSamplePoint> approachPath) =>
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
        };
}
