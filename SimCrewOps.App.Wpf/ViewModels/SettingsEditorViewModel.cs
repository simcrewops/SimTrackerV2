using SimCrewOps.App.Wpf.Infrastructure;
using SimCrewOps.Hosting.Models;

namespace SimCrewOps.App.Wpf.ViewModels;

public sealed class SettingsEditorViewModel : ObservableObject
{
    private string _baseUrl = string.Empty;
    private string _simSessionsPath = string.Empty;
    private string _pilotApiToken = string.Empty;
    private string _trackerVersion = string.Empty;
    private string _storageRootDirectory = string.Empty;
    private bool _backgroundSyncEnabled;
    private bool _enableTelemetryDiagnostics;
    private int _backgroundSyncIntervalSeconds;
    private int? _maxSessionsPerPass;

    public string BaseUrl
    {
        get => _baseUrl;
        set => SetProperty(ref _baseUrl, value);
    }

    public string SimSessionsPath
    {
        get => _simSessionsPath;
        set => SetProperty(ref _simSessionsPath, value);
    }

    public string PilotApiToken
    {
        get => _pilotApiToken;
        set => SetProperty(ref _pilotApiToken, value);
    }

    public string TrackerVersion
    {
        get => _trackerVersion;
        set => SetProperty(ref _trackerVersion, value);
    }

    public string StorageRootDirectory
    {
        get => _storageRootDirectory;
        set => SetProperty(ref _storageRootDirectory, value);
    }

    public bool BackgroundSyncEnabled
    {
        get => _backgroundSyncEnabled;
        set => SetProperty(ref _backgroundSyncEnabled, value);
    }

    public bool EnableTelemetryDiagnostics
    {
        get => _enableTelemetryDiagnostics;
        set => SetProperty(ref _enableTelemetryDiagnostics, value);
    }

    public int BackgroundSyncIntervalSeconds
    {
        get => _backgroundSyncIntervalSeconds;
        set => SetProperty(ref _backgroundSyncIntervalSeconds, value);
    }

    public int? MaxSessionsPerPass
    {
        get => _maxSessionsPerPass;
        set => SetProperty(ref _maxSessionsPerPass, value);
    }

    public static SettingsEditorViewModel FromSettings(TrackerAppSettings settings) =>
        new()
        {
            BaseUrl = settings.Api.BaseUri.ToString(),
            SimSessionsPath = settings.Api.SimSessionsPath,
            PilotApiToken = settings.Api.PilotApiToken ?? string.Empty,
            TrackerVersion = settings.Api.TrackerVersion,
            StorageRootDirectory = settings.Storage.RootDirectory,
            BackgroundSyncEnabled = settings.BackgroundSync.Enabled,
            EnableTelemetryDiagnostics = settings.Debug.EnableTelemetryDiagnostics,
            BackgroundSyncIntervalSeconds = settings.BackgroundSync.IntervalSeconds,
            MaxSessionsPerPass = settings.BackgroundSync.MaxSessionsPerPass,
        };

    public TrackerAppSettings ToSettings() =>
        new()
        {
            Storage = new TrackerStorageSettings
            {
                RootDirectory = StorageRootDirectory,
            },
            Api = new TrackerApiSettings
            {
                BaseUri = new Uri(BaseUrl, UriKind.Absolute),
                SimSessionsPath = SimSessionsPath,
                PilotApiToken = string.IsNullOrWhiteSpace(PilotApiToken) ? null : PilotApiToken,
                TrackerVersion = TrackerVersion,
            },
            BackgroundSync = new BackgroundSyncSettings
            {
                Enabled = BackgroundSyncEnabled,
                IntervalSeconds = BackgroundSyncIntervalSeconds,
                MaxSessionsPerPass = MaxSessionsPerPass,
            },
            Debug = new TrackerDebugSettings
            {
                EnableTelemetryDiagnostics = EnableTelemetryDiagnostics,
            },
        };
}
