namespace SimCrewOps.Hosting.Models;

public sealed record TrackerAppSettings
{
    public required TrackerStorageSettings Storage { get; init; }
    public TrackerApiSettings Api { get; init; } = new();
    public BackgroundSyncSettings BackgroundSync { get; init; } = new();
    public TrackerDebugSettings Debug { get; init; } = new();
}
