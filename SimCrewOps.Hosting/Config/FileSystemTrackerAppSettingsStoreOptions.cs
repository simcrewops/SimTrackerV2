namespace SimCrewOps.Hosting.Config;

public sealed record FileSystemTrackerAppSettingsStoreOptions
{
    public required string SettingsFilePath { get; init; }
}
