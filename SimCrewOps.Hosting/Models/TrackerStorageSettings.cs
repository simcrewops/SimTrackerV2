namespace SimCrewOps.Hosting.Models;

public sealed record TrackerStorageSettings
{
    public required string RootDirectory { get; init; }
    public string CurrentSessionFileName { get; init; } = "current-session.json";
    public string CompletedSessionsDirectoryName { get; init; } = "completed-sessions";
}
