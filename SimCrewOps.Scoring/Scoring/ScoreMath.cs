namespace SimCrewOps.Scoring.Scoring;

internal static class ScoreMath
{
    public static double Clamp(double value, double min, double max) =>
        Math.Min(Math.Max(value, min), max);

    public static double LinearPenalty(double observed, double limit, double fullPenaltyAt, double maxPenalty)
    {
        if (observed <= limit)
        {
            return 0;
        }

        if (observed >= fullPenaltyAt)
        {
            return maxPenalty;
        }

        var ratio = (observed - limit) / (fullPenaltyAt - limit);
        return ratio * maxPenalty;
    }

    public static double AbsoluteLinearPenalty(double observed, double limit, double fullPenaltyAt, double maxPenalty) =>
        LinearPenalty(Math.Abs(observed), limit, fullPenaltyAt, maxPenalty);

    public static double PerEventPenalty(int eventCount, double penaltyPerEvent, double maxPenalty)
    {
        if (eventCount <= 0)
        {
            return 0;
        }

        return Math.Min(eventCount * penaltyPerEvent, maxPenalty);
    }
}
