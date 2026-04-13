namespace SimCrewOps.Scoring.Models;

public sealed record ScoreFinding(
    string Code,
    string Description,
    double PointsDeducted,
    bool IsAutomaticFail = false);

public sealed record PhaseScoreResult(
    FlightPhase Phase,
    double MaxPoints,
    double AwardedPoints,
    IReadOnlyList<ScoreFinding> Findings)
{
    public double PointsDeducted => Findings.Sum(f => f.PointsDeducted);
    public bool SectionFailed => Findings.Any(f => f.IsAutomaticFail);
}

public sealed record ScoreResult(
    double MaximumScore,
    double FinalScore,
    string Grade,
    bool AutomaticFail,
    IReadOnlyList<PhaseScoreResult> PhaseScores,
    IReadOnlyList<ScoreFinding> GlobalFindings)
{
    public double PhaseSubtotal => PhaseScores.Sum(p => p.AwardedPoints);
    public double GlobalDeductions => GlobalFindings.Sum(f => f.PointsDeducted);
}
