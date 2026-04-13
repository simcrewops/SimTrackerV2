namespace SimCrewOps.Persistence.Persistence;

public sealed record FileSystemFlightSessionStoreOptions
{
    public required string RootDirectory { get; init; }
    public string CurrentSessionFileName { get; init; } = "current-session.json";
    public string CompletedSessionsDirectoryName { get; init; } = "completed-sessions";
}
