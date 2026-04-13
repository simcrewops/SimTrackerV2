namespace SimCrewOps.Hosting.Models;

public sealed record BackgroundSyncSettings
{
    public bool Enabled { get; init; } = true;
    public int IntervalSeconds { get; init; } = 300;
    public int? MaxSessionsPerPass { get; init; }
}
