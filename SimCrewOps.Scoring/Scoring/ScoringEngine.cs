using SimCrewOps.Scoring.Models;

namespace SimCrewOps.Scoring.Scoring;

public sealed class ScoringEngine
{
    public ScoreResult Calculate(FlightScoreInput input, ScoringWeights? weights = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        weights ??= ScoringWeights.Default;

        var phaseScores = new List<PhaseScoreResult>
        {
            ScorePreflight(input.Preflight, weights.Preflight),
            ScoreTaxiOut(input.TaxiOut, weights.TaxiOut),
            ScoreTakeoff(input.Takeoff, weights.Takeoff),
            ScoreClimb(input.Climb, weights.Climb),
            ScoreCruise(input.Cruise, weights.Cruise),
            ScoreDescent(input.Descent, weights.Descent),
            ScoreApproach(input.Approach, weights.Approach),
            ScoreLanding(input.Landing, weights.Landing),
            ScoreTaxiIn(input.TaxiIn, weights.TaxiIn),
            ScoreArrival(input.Arrival, weights.Arrival),
        };

        var globalFindings = ScoreSafety(input.Safety, weights.Safety);

        var maximumScore = phaseScores.Sum(phase => phase.MaxPoints);
        var phaseSubtotal = phaseScores.Sum(phase => phase.AwardedPoints);
        var globalDeductions = globalFindings.Sum(finding => finding.PointsDeducted);
        var automaticFail = globalFindings.Any(finding => finding.IsAutomaticFail);
        var finalScore = ScoreMath.Clamp(phaseSubtotal - globalDeductions, 0, maximumScore);
        var grade = automaticFail ? "F" : GradeFromScore(finalScore, maximumScore);

        return new ScoreResult(maximumScore, finalScore, grade, automaticFail, phaseScores, globalFindings);
    }

    private static PhaseScoreResult ScorePreflight(PreflightMetrics metrics, PreflightWeights weights)
    {
        var findings = new List<ScoreFinding>();

        if (!metrics.BeaconOnBeforeTaxi)
        {
            findings.Add(new ScoreFinding(
                "PREFLIGHT_BEACON_OFF",
                "Beacon light was not on before taxi.",
                weights.BeaconOnBeforeTaxi));
        }

        return CreatePhaseResult(FlightPhase.Preflight, weights.Total, findings);
    }

    private static PhaseScoreResult ScoreTaxiOut(TaxiMetrics metrics, TaxiWeights weights)
    {
        var findings = new List<ScoreFinding>();

        AddPenalty(
            findings,
            "TAXI_OUT_SPEED",
            "Taxi out speed exceeded 40 knots.",
            ScoreMath.LinearPenalty(metrics.MaxGroundSpeedKnots, 40, 60, weights.MaxGroundSpeed));

        AddPenalty(
            findings,
            "TAXI_OUT_TURN_SPEED",
            "Taxi out included high-speed turns above the turn-speed target.",
            ScoreMath.PerEventPenalty(metrics.ExcessiveTurnSpeedEvents, 1.5, weights.TurnSpeed));

        if (!metrics.TaxiLightsOn)
        {
            findings.Add(new ScoreFinding(
                "TAXI_OUT_LIGHTS",
                "Taxi lights were not on during taxi out.",
                weights.TaxiLights));
        }

        return CreatePhaseResult(FlightPhase.TaxiOut, weights.Total, findings);
    }

    private static PhaseScoreResult ScoreTakeoff(TakeoffMetrics metrics, TakeoffWeights weights)
    {
        var findings = new List<ScoreFinding>();

        AddPenalty(
            findings,
            "TAKEOFF_BOUNCE",
            "The aircraft returned to the runway during takeoff.",
            ScoreMath.PerEventPenalty(metrics.BounceCount, 1.0, weights.Bounce));

        if (metrics.TailStrikeDetected)
        {
            findings.Add(new ScoreFinding(
                "TAKEOFF_TAIL_STRIKE",
                "Tail strike detected during takeoff roll or rotation.",
                weights.TailStrike));
        }

        AddPenalty(
            findings,
            "TAKEOFF_BANK",
            "Takeoff bank angle exceeded the 30 degree target.",
            ScoreMath.AbsoluteLinearPenalty(metrics.MaxBankAngleDegrees, 30, 45, weights.BankAngle));

        AddPenalty(
            findings,
            "TAKEOFF_PITCH",
            "Takeoff pitch exceeded the 20 degree target.",
            ScoreMath.AbsoluteLinearPenalty(metrics.MaxPitchAngleDegrees, 20, 30, weights.PitchAngle));

        AddPenalty(
            findings,
            "TAKEOFF_GFORCE",
            "Takeoff G-force exceeded the comfort target.",
            ScoreMath.LinearPenalty(metrics.MaxGForce, weights.GForcePerfect, weights.GForceMax, weights.GForce));

        if (!metrics.LandingLightsOnBeforeTakeoff)
        {
            findings.Add(new ScoreFinding(
                "TAKEOFF_LANDING_LIGHTS_OFF",
                "Landing lights were not on before takeoff.",
                weights.LandingLightsOnBeforeTakeoff));
        }

        if (!metrics.LandingLightsOffByFl180)
        {
            findings.Add(new ScoreFinding(
                "TAKEOFF_LANDING_LIGHTS_LATE",
                "Landing lights were not turned off by FL180.",
                weights.LandingLightsOffByFl180));
        }

        if (!metrics.StrobesOnFromTakeoffToLanding)
        {
            findings.Add(new ScoreFinding(
                "TAKEOFF_STROBES_OFF",
                "Strobes were not on from takeoff through landing.",
                weights.StrobesOnFromTakeoffToLanding));
        }

        return CreatePhaseResult(FlightPhase.Takeoff, weights.Total, findings);
    }

    private static PhaseScoreResult ScoreClimb(ClimbMetrics metrics, ClimbWeights weights)
    {
        var findings = new List<ScoreFinding>();
        var speedLimit = metrics.HeavyFourEngineAircraft ? 300 : 250;

        // 10-knot buffer before the penalty ramp starts (250+10 = 260 kts perfect threshold).
        AddPenalty(
            findings,
            "CLIMB_SPEED",
            $"Climb speed below FL100 exceeded the {speedLimit + 10} knot target.",
            ScoreMath.LinearPenalty(metrics.MaxIasBelowFl100Knots, speedLimit + 10, speedLimit + 50, weights.SpeedCompliance));

        AddPenalty(
            findings,
            "CLIMB_BANK",
            "Climb bank angle exceeded the 30 degree target.",
            ScoreMath.AbsoluteLinearPenalty(metrics.MaxBankAngleDegrees, 30, 45, weights.BankAngle));

        AddPenalty(
            findings,
            "CLIMB_GFORCE",
            "Climb G-force exceeded the comfort target.",
            ScoreMath.LinearPenalty(metrics.MaxGForce, weights.GForcePerfect, weights.GForceMax, weights.GForce));

        return CreatePhaseResult(FlightPhase.Climb, weights.Total, findings);
    }

    private static PhaseScoreResult ScoreCruise(CruiseMetrics metrics, CruiseWeights weights)
    {
        var findings = new List<ScoreFinding>();

        AddPenalty(
            findings,
            "CRUISE_ALTITUDE",
            "Cruise altitude deviated outside the +/-100 foot target.",
            ScoreMath.LinearPenalty(metrics.MaxAltitudeDeviationFeet, 100, 300, weights.AltitudeHold));

        if (metrics.NewFlightLevelCaptureSeconds is > 60)
        {
            AddPenalty(
                findings,
                "CRUISE_LEVEL_CAPTURE",
                "A new flight level was not captured within 60 seconds.",
                ScoreMath.LinearPenalty(metrics.NewFlightLevelCaptureSeconds.Value, 60, 180, weights.FlightLevelCapture));
        }

        AddPenalty(
            findings,
            "CRUISE_SPEED_STABILITY",
            "Cruise speed or Mach stability was inconsistent.",
            ScoreMath.PerEventPenalty(metrics.SpeedInstabilityEvents, 1.0, weights.SpeedStability));

        AddPenalty(
            findings,
            "CRUISE_BANK",
            "Cruise bank angle exceeded the 30 degree target.",
            ScoreMath.AbsoluteLinearPenalty(metrics.LevelMaxBankDegrees, 30, 45, weights.BankAngle));

        AddPenalty(
            findings,
            "CRUISE_GFORCE",
            "Cruise G-force exceeded the comfort target.",
            ScoreMath.LinearPenalty(metrics.MaxGForce, weights.GForcePerfect, weights.GForceMax, weights.GForce));

        return CreatePhaseResult(FlightPhase.Cruise, weights.Total, findings);
    }

    private static PhaseScoreResult ScoreDescent(DescentMetrics metrics, DescentWeights weights)
    {
        var findings = new List<ScoreFinding>();

        // 10-knot buffer: penalty ramp starts at 260 kts, full penalty at 300 kts.
        AddPenalty(
            findings,
            "DESCENT_SPEED",
            "Descent speed below FL100 exceeded the 260 knot target.",
            ScoreMath.LinearPenalty(metrics.MaxIasBelowFl100Knots, 260, 300, weights.SpeedCompliance));

        AddPenalty(
            findings,
            "DESCENT_BANK",
            "Descent bank angle exceeded the target.",
            ScoreMath.AbsoluteLinearPenalty(metrics.MaxBankAngleDegrees, 30, 45, weights.BankAngle));

        AddPenalty(
            findings,
            "DESCENT_PITCH",
            "Descent pitch exceeded the 20 degree target.",
            ScoreMath.AbsoluteLinearPenalty(metrics.MaxPitchAngleDegrees, 20, 30, weights.PitchAngle));

        AddPenalty(
            findings,
            "DESCENT_GFORCE",
            "Descent G-force exceeded the comfort target.",
            ScoreMath.LinearPenalty(metrics.MaxGForce, weights.GForcePerfect, weights.GForceMax, weights.GForce));

        if (!metrics.LandingLightsOnBy9900)
        {
            findings.Add(new ScoreFinding(
                "DESCENT_LANDING_LIGHTS_OFF",
                "Landing lights were not on by 9,900 ft during descent.",
                weights.LandingLightsOnBy9900));
        }

        return CreatePhaseResult(FlightPhase.Descent, weights.Total, findings);
    }

    private static PhaseScoreResult ScoreApproach(ApproachMetrics metrics, ApproachWeights weights)
    {
        var findings = new List<ScoreFinding>();

        if (!metrics.GearDownBy1000Agl)
        {
            findings.Add(new ScoreFinding(
                "APPROACH_GEAR_1000",
                "Gear was not down by 1000 feet AGL.",
                weights.GearDownBy1000Agl));
        }

        if (metrics.FlapsHandleIndexAt500Agl <= 1)
        {
            findings.Add(new ScoreFinding(
                "APPROACH_FLAPS",
                "Flaps were not greater than 1 by 500 feet AGL.",
                weights.FlapsConfiguredAt500Agl));
        }

        AddPenalty(
            findings,
            "APPROACH_VS",
            "Approach vertical speed at 500 feet AGL exceeded 1000 fpm.",
            ScoreMath.AbsoluteLinearPenalty(metrics.VerticalSpeedAt500AglFpm, 1000, 1500, weights.StabilizedVerticalSpeed));

        AddPenalty(
            findings,
            "APPROACH_BANK",
            "Approach bank angle at 500 feet AGL exceeded 10 degrees.",
            ScoreMath.AbsoluteLinearPenalty(metrics.BankAngleAt500AglDegrees, 10, 20, weights.StabilizedBankAngle));

        AddPenalty(
            findings,
            "APPROACH_PITCH",
            "Approach pitch at 500 feet AGL exceeded 10 degrees.",
            ScoreMath.AbsoluteLinearPenalty(metrics.PitchAngleAt500AglDegrees, 10, 20, weights.StabilizedPitchAngle));

        if (!metrics.GearDownAt500Agl)
        {
            findings.Add(new ScoreFinding(
                "APPROACH_GEAR_500",
                "Approach was not stabilized by 500 feet AGL because the gear was still up.",
                weights.GearDownAt500Agl));
        }

        if (metrics.FlapsHandleIndexAt500Agl <= 1)
        {
            findings.Add(new ScoreFinding(
                "APPROACH_STABLE_FLAPS",
                "Approach was not stabilized by 500 feet AGL because landing flaps were not set.",
                weights.StabilizedFlapsConfiguredAt500Agl));
        }

        return CreatePhaseResult(FlightPhase.Approach, weights.Total, findings);
    }

    private static PhaseScoreResult ScoreLanding(LandingMetrics metrics, LandingWeights weights)
    {
        var findings = new List<ScoreFinding>();

        AddPenalty(
            findings,
            "LANDING_TOUCHDOWN_ZONE",
            "Touchdown occurred outside the touchdown zone target.",
            ScoreMath.LinearPenalty(metrics.TouchdownZoneExcessDistanceFeet, 0, 300, weights.TouchdownZone));

        if (metrics.TouchdownVerticalSpeedFpm > weights.VerticalSpeedAutoFailFpm)
        {
            findings.Add(new ScoreFinding(
                "LANDING_VERTICAL_SPEED",
                $"Hard landing: touchdown vertical speed exceeded {weights.VerticalSpeedAutoFailFpm} fpm and failed the landing section.",
                weights.VerticalSpeed,
                IsAutomaticFail: true));
        }
        else
        {
            AddPenalty(
                findings,
                "LANDING_VERTICAL_SPEED",
                $"Touchdown vertical speed exceeded {weights.VerticalSpeedPerfectFpm} fpm.",
                ScoreMath.LinearPenalty(
                    metrics.TouchdownVerticalSpeedFpm,
                    weights.VerticalSpeedPerfectFpm,
                    weights.VerticalSpeedAutoFailFpm,
                    weights.VerticalSpeed));
        }

        if (metrics.TouchdownGForce > weights.GForceAutoFail)
        {
            findings.Add(new ScoreFinding(
                "LANDING_GFORCE",
                $"Hard landing: touchdown G-force exceeded {weights.GForceAutoFail}G and failed the landing section.",
                weights.GForce,
                IsAutomaticFail: true));
        }
        else
        {
            AddPenalty(
                findings,
                "LANDING_GFORCE",
                $"Touchdown G-force exceeded {weights.GForcePerfect}G.",
                ScoreMath.LinearPenalty(
                    metrics.TouchdownGForce,
                    weights.GForcePerfect,
                    weights.GForceAutoFail,
                    weights.GForce));
        }

        AddPenalty(
            findings,
            "LANDING_BOUNCE",
            "Landing bounce(s) detected.",
            ScoreMath.PerEventPenalty(metrics.BounceCount, 2.5, weights.Bounce));

        // Only score centerline and crab when runway data was available (non-zero values).
        if (metrics.TouchdownCenterlineDeviationFeet != 0 || metrics.TouchdownCrabAngleDegrees != 0)
        {
            AddPenalty(
                findings,
                "LANDING_CENTERLINE",
                "Touchdown centerline deviation exceeded target.",
                CenterlinePenalty(metrics.TouchdownCenterlineDeviationFeet, weights.CenterlineDeviation));

            AddPenalty(
                findings,
                "LANDING_CRAB_ANGLE",
                "Touchdown crab angle exceeded target.",
                CrabAnglePenalty(metrics.TouchdownCrabAngleDegrees, weights.CrabAngle));
        }

        return CreatePhaseResult(FlightPhase.Landing, weights.Total, findings);
    }

    // Tiered penalty based on real-world aviation centerline standards (metres converted to feet).
    // Dead zone: 0–1 m (0–3.3 ft) = perfect; >20 m (>65.6 ft) = zero points.
    private static double CenterlinePenalty(double feet, double maxPoints)
    {
        var absFeet = Math.Abs(feet);
        var fraction = absFeet switch
        {
            <= 3.3  => 0.0,   // 10/10 pts — dead zone
            <= 16.4 => 0.2,   //  8/10 pts
            <= 32.8 => 0.4,   //  6/10 pts
            <= 65.6 => 0.7,   //  3/10 pts
            _       => 1.0,   //  0/10 pts
        };
        return fraction * maxPoints;
    }

    // Tiered penalty based on real-world aviation crab angle standards.
    // Dead zone: 0–0.5° = perfect; >5° = zero points.
    private static double CrabAnglePenalty(double degrees, double maxPoints)
    {
        var absDeg = Math.Abs(degrees);
        var fraction = absDeg switch
        {
            <= 0.5 => 0.0,   // 10/10 pts — dead zone
            <= 2.0 => 0.2,   //  8/10 pts
            <= 3.5 => 0.5,   //  5/10 pts
            <= 5.0 => 0.8,   //  2/10 pts
            _      => 1.0,   //  0/10 pts
        };
        return fraction * maxPoints;
    }

    private static PhaseScoreResult ScoreTaxiIn(TaxiInMetrics metrics, TaxiInWeights weights)
    {
        var findings = new List<ScoreFinding>();

        if (!metrics.LandingLightsOff)
        {
            findings.Add(new ScoreFinding(
                "TAXI_IN_LANDING_LIGHTS",
                "Landing lights were not turned off during taxi in.",
                weights.LandingLightsOff));
        }

        if (!metrics.StrobesOff)
        {
            findings.Add(new ScoreFinding(
                "TAXI_IN_STROBES",
                "Strobes were not turned off during taxi in.",
                weights.StrobesOff));
        }

        AddPenalty(
            findings,
            "TAXI_IN_SPEED",
            "Taxi in speed exceeded 40 knots.",
            ScoreMath.LinearPenalty(metrics.MaxGroundSpeedKnots, 40, 60, weights.MaxGroundSpeed));

        AddPenalty(
            findings,
            "TAXI_IN_TURN_SPEED",
            "Taxi in included high-speed turns above the turn-speed target.",
            ScoreMath.PerEventPenalty(metrics.ExcessiveTurnSpeedEvents, 1.0, weights.TurnSpeed));

        if (!metrics.TaxiLightsOn)
        {
            findings.Add(new ScoreFinding(
                "TAXI_IN_LIGHTS",
                "Taxi lights were not on during taxi in.",
                weights.TaxiLights));
        }

        return CreatePhaseResult(FlightPhase.TaxiIn, weights.Total, findings);
    }

    private static PhaseScoreResult ScoreArrival(ArrivalMetrics metrics, ArrivalWeights weights)
    {
        var findings = new List<ScoreFinding>();

        if (!metrics.TaxiLightsOffBeforeParkingBrakeSet)
        {
            findings.Add(new ScoreFinding(
                "ARRIVAL_TAXI_LIGHTS_ORDER",
                "Taxi lights were not turned off before the parking brake was set.",
                weights.TaxiLightsOffBeforeParkingBrakeSet));
        }

        if (!metrics.AllEnginesOffBeforeParkingBrakeSet)
        {
            findings.Add(new ScoreFinding(
                "ARRIVAL_PARKING_BRAKE_ORDER",
                "Parking brake was set while engines were still running. Shut down all engines before setting the parking brake.",
                weights.EnginesOffBeforeParkingBrake));
        }

        if (!metrics.AllEnginesOffByEndOfSession)
        {
            findings.Add(new ScoreFinding(
                "ARRIVAL_ENGINE_SHUTDOWN_COMPLETE",
                "All engines were not shut down by the end of the session.",
                weights.AllEnginesOffByEndOfSession));
        }

        return CreatePhaseResult(FlightPhase.Arrival, weights.Total, findings);
    }

    private static IReadOnlyList<ScoreFinding> ScoreSafety(SafetyMetrics metrics, SafetyWeights weights)
    {
        var findings = new List<ScoreFinding>();

        if (metrics.CrashDetected)
        {
            findings.Add(new ScoreFinding(
                "SAFETY_CRASH",
                "Crash or simulator reset detected.",
                weights.CrashPenalty,
                IsAutomaticFail: true));
        }

        AddPenalty(
            findings,
            "SAFETY_OVERSPEED",
            "Overspeed events detected.",
            ScoreMath.PerEventPenalty(metrics.OverspeedEvents, weights.OverspeedEvent, weights.MaxOverspeedPenalty));

        AddPenalty(
            findings,
            "SAFETY_OVERSPEED_SUSTAINED",
            "Sustained overspeed events detected.",
            ScoreMath.PerEventPenalty(metrics.SustainedOverspeedEvents, weights.SustainedOverspeedEvent, weights.MaxSustainedOverspeedPenalty));

        AddPenalty(
            findings,
            "SAFETY_STALL",
            "Stall warning events detected.",
            ScoreMath.PerEventPenalty(metrics.StallEvents, weights.StallEvent, weights.MaxStallPenalty));

        AddPenalty(
            findings,
            "SAFETY_GPWS",
            "GPWS alert events detected.",
            ScoreMath.PerEventPenalty(metrics.GpwsEvents, weights.GpwsEvent, weights.MaxGpwsPenalty));

        AddPenalty(
            findings,
            "SAFETY_ENGINE_SHUTDOWN",
            "Engine shutdown(s) detected in flight.",
            ScoreMath.PerEventPenalty(metrics.EngineShutdownsInFlight, weights.EngineShutdownInFlight, double.MaxValue));

        return findings;
    }

    private static PhaseScoreResult CreatePhaseResult(FlightPhase phase, double maxPoints, IReadOnlyList<ScoreFinding> findings)
    {
        var deductions = findings.Sum(finding => finding.PointsDeducted);
        var sectionFailed = findings.Any(finding => finding.IsAutomaticFail);
        var awardedPoints = sectionFailed ? 0 : ScoreMath.Clamp(maxPoints - deductions, 0, maxPoints);
        return new PhaseScoreResult(phase, maxPoints, awardedPoints, findings);
    }

    private static void AddPenalty(ICollection<ScoreFinding> findings, string code, string description, double points)
    {
        if (points <= 0)
        {
            return;
        }

        findings.Add(new ScoreFinding(code, description, points));
    }

    /// <summary>
    /// Converts a raw score into a letter grade using percentage thresholds so the grade
    /// remains consistent regardless of the maximum possible score (e.g. 100 or 120).
    /// </summary>
    private static string GradeFromScore(double score, double maximumScore)
    {
        var pct = maximumScore > 0 ? score / maximumScore * 100.0 : score;
        return pct switch
        {
            >= 90 => "A",
            >= 75 => "B",
            >= 60 => "C",
            >= 50 => "D",
            _     => "F",
        };
    }
}
