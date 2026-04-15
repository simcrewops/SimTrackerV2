using SimCrewOps.Hosting.Config;
using SimCrewOps.Hosting.Hosting;
using SimCrewOps.Hosting.Models;

namespace SimCrewOps.App.Wpf.Services;

public sealed record TrackerShellBootstrapResult
{
    public required TrackerShellHost ShellHost { get; init; }
    public required ITrackerAppSettingsStore SettingsStore { get; init; }
    public required TrackerAppSettings Settings { get; init; }
    public required string SettingsFilePath { get; init; }

    /// <summary>
    /// The live map polling service, or null when no API token is configured.
    /// </summary>
    public LiveMapService? LiveMapService { get; init; }
}
