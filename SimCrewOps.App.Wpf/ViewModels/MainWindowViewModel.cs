using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using SimCrewOps.App.Wpf.Infrastructure;
using SimCrewOps.App.Wpf.Models;
using SimCrewOps.App.Wpf.Services;
using SimCrewOps.App.Wpf.Views;
using SimCrewOps.Runtime.Models;
using SimCrewOps.Scoring.Models;
using SimCrewOps.SimConnect.Models;
using SimCrewOps.Sync.Models;
using SimCrewOps.Tracking.Models;
using Brush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;

namespace SimCrewOps.App.Wpf.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly TrackerShellHost _shellHost;
    private readonly DispatcherTimer _pollingTimer;
    private readonly SimCrewOps.Hosting.Hosting.LiveMapService? _liveMapService;

    private TrackerShellSnapshot? _latestSnapshot;
    private bool _isRefreshing;
    private NavPage _selectedPage = NavPage.Dashboard;
    private string _msfsStatusText = "WAITING FOR SIM";
    private Brush _msfsStatusBrush = new SolidColorBrush(MediaColor.FromRgb(56, 91, 105));
    private string _syncStatusText = "SYNC READY";
    private Brush _syncStatusBrush = new SolidColorBrush(MediaColor.FromRgb(56, 91, 105));
    private string _headerFlightText = "Waiting for flight context • MSFS telemetry will populate here";
    private string _phaseTitle = "PREFLIGHT";
    private string _phaseSubtitle = "The live ops board stays blank until the tracker receives real telemetry.";
    private string _phaseStatusLine = "Waiting for telemetry";
    private string _careerTier = "Standard Profile";
    private string _bidDisplay = "Free Flight";
    private string _reputationDisplay = "Free Flight";
    private string _outTime = "--:--";
    private string _offTime = "--:--";
    private string _onTime = "--:--";
    private string _inTime = "--:--";
    private string _scoreText = "--";
    private string _gradeText = "Grade --";
    private string _scoreSummary = "No score available until a live session is in progress.";
    private string _alertPrimaryTitle = "Waiting for telemetry";
    private string _alertPrimaryBody = "The tracker will populate live alerts once SimConnect data is flowing.";
    private string _alertSecondaryTitle = "No events yet";
    private string _alertSecondaryBody = "Overspeed, stall, and GPWS events will appear during flight.";
    private string _dispatchTitle = "Dispatch channel ready";
    private string _dispatchBody = "Live dispatch and ACARS items will appear here when messaging is wired.";
    private string _sessionHealthTitle = "Autosave OK";
    private string _sessionHealthLine1 = "API Queue 0";
    private string _sessionHealthLine2 = "Last sync --:--";
    private string _sessionHealthLine3 = "Runtime healthy";
    private string _reviewSummary = "Landing metrics will populate once a real touchdown is captured.";
    private string _reviewLandingMetrics = "VS --   G --   Bank --   Pitch --   TZ Excess --";
    private string _diagnosticsSimState = "Waiting for simulator process";
    private string _diagnosticsSyncState = "Background sync not yet initialized";
    private string _diagnosticsRecoveryState = "No recoverable session";
    private string _diagnosticsSettingsPath = string.Empty;
    private string _diagnosticsStoragePath = string.Empty;
    private string _diagnosticsLastTelemetry = "No telemetry received yet";
    private bool _telemetryDiagnosticsEnabled;
    private string _diagnosticsTelemetryClient = "Telemetry debug disabled in Settings.";
    private string _diagnosticsTelemetryFlow = "Enable telemetry diagnostics to inspect raw SimConnect frame flow.";
    private string _diagnosticsTelemetryCounters = "Poll, null-frame, and mapping counters will appear here.";
    private string _diagnosticsRawTelemetry = "Raw SimConnect values will appear here when telemetry diagnostics are enabled.";
    private string _settingsSaveStatus = "Changes are saved for the next launch.";

    // Persistent instruments
    private string _liveIas = "---";
    private string _liveGs = "---";
    private string _liveMach = "-.---";
    private string _liveAlt = "-----";
    private string _liveAgl = "-----";
    private string _liveVs = "----";
    private string _liveHdg = "---°";
    private string _liveGForce = "-.--";
    private bool _liveVsAlert;
    private bool _liveGForceAlert;
    private bool _liveAglAlert;

    // Systems LED brushes
    private Brush _ledBeaconBrush = new SolidColorBrush(MediaColor.FromRgb(42, 64, 80));
    private Brush _ledStrobeBrush = new SolidColorBrush(MediaColor.FromRgb(42, 64, 80));
    private Brush _ledLandingBrush = new SolidColorBrush(MediaColor.FromRgb(42, 64, 80));
    private Brush _ledTaxiBrush = new SolidColorBrush(MediaColor.FromRgb(42, 64, 80));
    private Brush _ledGearBrush = new SolidColorBrush(MediaColor.FromRgb(42, 64, 80));
    private string _flapsLabel = "F--";
    private Brush _ledEng1Brush = new SolidColorBrush(MediaColor.FromRgb(42, 64, 80));
    private Brush _ledEng2Brush = new SolidColorBrush(MediaColor.FromRgb(42, 64, 80));
    private Brush _ledParkingBrakeBrush = new SolidColorBrush(MediaColor.FromRgb(42, 64, 80));

    // Live sync / account connection status
    private string _liveSyncStatusText = "NOT CONNECTED";
    private Brush _liveSyncStatusBrush = new SolidColorBrush(MediaColor.FromRgb(90, 106, 112));

    public MainWindowViewModel(TrackerShellBootstrapResult bootstrap)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);

        _shellHost = bootstrap.ShellHost;
        SettingsEditor = SettingsEditorViewModel.FromSettings(bootstrap.Settings);
        _diagnosticsSettingsPath = bootstrap.SettingsFilePath;
        _diagnosticsStoragePath = bootstrap.Settings.Storage.RootDirectory;

        MetricTiles = new ObservableCollection<MetricTileModel>();
        StatusChips = new ObservableCollection<string>();
        PrimaryScoreRows = new ObservableCollection<ScoreRowModel>();
        SecondaryScoreRows = new ObservableCollection<ScoreRowModel>();
        ReviewScoreRows = new ObservableCollection<ScoreRowModel>();
        PhasePills = new ObservableCollection<PhasePillModel>();
        PlaneMarkers = new ObservableCollection<PlaneMarkerModel>();

        ShowDashboardCommand = new RelayCommand(() => SelectedPage = NavPage.Dashboard);
        ShowLiveMapCommand   = new RelayCommand(() => SelectedPage = NavPage.LiveMap);
        ShowReviewCommand = new RelayCommand(() => SelectedPage = NavPage.Review);
        ShowDiagnosticsCommand = new RelayCommand(() => SelectedPage = NavPage.Diagnostics);
        ShowSettingsCommand = new RelayCommand(() => SelectedPage = NavPage.Settings);

        // Wire up the live map service if one was created for this service stack.
        _liveMapService = bootstrap.LiveMapService;
        if (_liveMapService is not null)
        {
            _liveMapService.PositionsUpdated += OnLiveMapPositionsUpdated;
        }
        RetrySyncCommand = new RelayCommand(() => _ = RetrySyncAsync());
        SaveSettingsCommand = new RelayCommand(() => _ = SaveSettingsAsync());

        _pollingTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _pollingTimer.Tick += async (_, _) => await RefreshAsync();

        ApplySampleState();
    }

    public SettingsEditorViewModel SettingsEditor { get; }

    public ObservableCollection<MetricTileModel> MetricTiles { get; }
    public ObservableCollection<string> StatusChips { get; }
    public ObservableCollection<ScoreRowModel> PrimaryScoreRows { get; }
    public ObservableCollection<ScoreRowModel> SecondaryScoreRows { get; }
    public ObservableCollection<ScoreRowModel> ReviewScoreRows { get; }
    public ObservableCollection<PhasePillModel> PhasePills { get; }

    public RelayCommand ShowDashboardCommand { get; }
    public RelayCommand ShowLiveMapCommand   { get; }
    public RelayCommand ShowReviewCommand { get; }
    public RelayCommand ShowDiagnosticsCommand { get; }
    public RelayCommand ShowSettingsCommand { get; }

    /// <summary>Live-updated collection of aircraft markers for the map canvas.</summary>
    public ObservableCollection<PlaneMarkerModel> PlaneMarkers { get; }

    /// <summary>True when the live map service is running (API token configured).</summary>
    public bool LiveMapAvailable => _liveMapService is not null;

    private IReadOnlyList<SimCrewOps.Hosting.Models.LiveFlight> _liveFlights = Array.Empty<SimCrewOps.Hosting.Models.LiveFlight>();

    /// <summary>
    /// Latest snapshot of fleet positions, suitable for the map canvas to project and render.
    /// The canvas code-behind listens for changes via PropertyChanged and redraws.
    /// </summary>
    public IReadOnlyList<SimCrewOps.Hosting.Models.LiveFlight> LiveFlights
    {
        get => _liveFlights;
        private set => SetProperty(ref _liveFlights, value);
    }
    public RelayCommand RetrySyncCommand { get; }
    public RelayCommand SaveSettingsCommand { get; }
    public RelayCommand OpenAccountSettingsCommand { get; } = new RelayCommand(() =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://simcrewops.com/settings",
            UseShellExecute = true,
        }));

    public string LiveSyncStatusText
    {
        get => _liveSyncStatusText;
        private set => SetProperty(ref _liveSyncStatusText, value);
    }

    public Brush LiveSyncStatusBrush
    {
        get => _liveSyncStatusBrush;
        private set => SetProperty(ref _liveSyncStatusBrush, value);
    }

    public string MsfsStatusText
    {
        get => _msfsStatusText;
        private set => SetProperty(ref _msfsStatusText, value);
    }

    public Brush MsfsStatusBrush
    {
        get => _msfsStatusBrush;
        private set => SetProperty(ref _msfsStatusBrush, value);
    }

    public string SyncStatusText
    {
        get => _syncStatusText;
        private set => SetProperty(ref _syncStatusText, value);
    }

    public Brush SyncStatusBrush
    {
        get => _syncStatusBrush;
        private set => SetProperty(ref _syncStatusBrush, value);
    }

    public string PhaseTitle
    {
        get => _phaseTitle;
        private set => SetProperty(ref _phaseTitle, value);
    }

    public string HeaderFlightText
    {
        get => _headerFlightText;
        private set => SetProperty(ref _headerFlightText, value);
    }

    public string PhaseSubtitle
    {
        get => _phaseSubtitle;
        private set => SetProperty(ref _phaseSubtitle, value);
    }

    public string PhaseStatusLine
    {
        get => _phaseStatusLine;
        private set => SetProperty(ref _phaseStatusLine, value);
    }

    public string CareerTier
    {
        get => _careerTier;
        private set => SetProperty(ref _careerTier, value);
    }

    public string BidDisplay
    {
        get => _bidDisplay;
        private set => SetProperty(ref _bidDisplay, value);
    }

    public string ReputationDisplay
    {
        get => _reputationDisplay;
        private set => SetProperty(ref _reputationDisplay, value);
    }

    public string OutTime
    {
        get => _outTime;
        private set => SetProperty(ref _outTime, value);
    }

    public string OffTime
    {
        get => _offTime;
        private set => SetProperty(ref _offTime, value);
    }

    public string OnTime
    {
        get => _onTime;
        private set => SetProperty(ref _onTime, value);
    }

    public string InTime
    {
        get => _inTime;
        private set => SetProperty(ref _inTime, value);
    }

    public string ScoreText
    {
        get => _scoreText;
        private set => SetProperty(ref _scoreText, value);
    }

    public string GradeText
    {
        get => _gradeText;
        private set => SetProperty(ref _gradeText, value);
    }

    public string ScoreSummary
    {
        get => _scoreSummary;
        private set => SetProperty(ref _scoreSummary, value);
    }

    public string AlertPrimaryTitle
    {
        get => _alertPrimaryTitle;
        private set => SetProperty(ref _alertPrimaryTitle, value);
    }

    public string AlertPrimaryBody
    {
        get => _alertPrimaryBody;
        private set => SetProperty(ref _alertPrimaryBody, value);
    }

    public string AlertSecondaryTitle
    {
        get => _alertSecondaryTitle;
        private set => SetProperty(ref _alertSecondaryTitle, value);
    }

    public string AlertSecondaryBody
    {
        get => _alertSecondaryBody;
        private set => SetProperty(ref _alertSecondaryBody, value);
    }

    public string DispatchTitle
    {
        get => _dispatchTitle;
        private set => SetProperty(ref _dispatchTitle, value);
    }

    public string DispatchBody
    {
        get => _dispatchBody;
        private set => SetProperty(ref _dispatchBody, value);
    }

    public string SessionHealthTitle
    {
        get => _sessionHealthTitle;
        private set => SetProperty(ref _sessionHealthTitle, value);
    }

    public string SessionHealthLine1
    {
        get => _sessionHealthLine1;
        private set => SetProperty(ref _sessionHealthLine1, value);
    }

    public string SessionHealthLine2
    {
        get => _sessionHealthLine2;
        private set => SetProperty(ref _sessionHealthLine2, value);
    }

    public string SessionHealthLine3
    {
        get => _sessionHealthLine3;
        private set => SetProperty(ref _sessionHealthLine3, value);
    }

    public string ReviewSummary
    {
        get => _reviewSummary;
        private set => SetProperty(ref _reviewSummary, value);
    }

    public string ReviewLandingMetrics
    {
        get => _reviewLandingMetrics;
        private set => SetProperty(ref _reviewLandingMetrics, value);
    }

    public string DiagnosticsSimState
    {
        get => _diagnosticsSimState;
        private set => SetProperty(ref _diagnosticsSimState, value);
    }

    public string DiagnosticsSyncState
    {
        get => _diagnosticsSyncState;
        private set => SetProperty(ref _diagnosticsSyncState, value);
    }

    public string DiagnosticsRecoveryState
    {
        get => _diagnosticsRecoveryState;
        private set => SetProperty(ref _diagnosticsRecoveryState, value);
    }

    public string DiagnosticsSettingsPath
    {
        get => _diagnosticsSettingsPath;
        private set => SetProperty(ref _diagnosticsSettingsPath, value);
    }

    public string DiagnosticsStoragePath
    {
        get => _diagnosticsStoragePath;
        private set => SetProperty(ref _diagnosticsStoragePath, value);
    }

    public string DiagnosticsLastTelemetry
    {
        get => _diagnosticsLastTelemetry;
        private set => SetProperty(ref _diagnosticsLastTelemetry, value);
    }

    public bool TelemetryDiagnosticsEnabled
    {
        get => _telemetryDiagnosticsEnabled;
        private set => SetProperty(ref _telemetryDiagnosticsEnabled, value);
    }

    public string DiagnosticsTelemetryClient
    {
        get => _diagnosticsTelemetryClient;
        private set => SetProperty(ref _diagnosticsTelemetryClient, value);
    }

    public string DiagnosticsTelemetryFlow
    {
        get => _diagnosticsTelemetryFlow;
        private set => SetProperty(ref _diagnosticsTelemetryFlow, value);
    }

    public string DiagnosticsTelemetryCounters
    {
        get => _diagnosticsTelemetryCounters;
        private set => SetProperty(ref _diagnosticsTelemetryCounters, value);
    }

    public string DiagnosticsRawTelemetry
    {
        get => _diagnosticsRawTelemetry;
        private set => SetProperty(ref _diagnosticsRawTelemetry, value);
    }

    public string SettingsSaveStatus
    {
        get => _settingsSaveStatus;
        private set => SetProperty(ref _settingsSaveStatus, value);
    }

    // Persistent instrument display properties
    public string LiveIas { get => _liveIas; private set => SetProperty(ref _liveIas, value); }
    public string LiveGs { get => _liveGs; private set => SetProperty(ref _liveGs, value); }
    public string LiveMach { get => _liveMach; private set => SetProperty(ref _liveMach, value); }
    public string LiveAlt { get => _liveAlt; private set => SetProperty(ref _liveAlt, value); }
    public string LiveAgl { get => _liveAgl; private set => SetProperty(ref _liveAgl, value); }
    public string LiveVs { get => _liveVs; private set => SetProperty(ref _liveVs, value); }
    public string LiveHdg { get => _liveHdg; private set => SetProperty(ref _liveHdg, value); }
    public string LiveGForce { get => _liveGForce; private set => SetProperty(ref _liveGForce, value); }
    public bool LiveVsAlert { get => _liveVsAlert; private set => SetProperty(ref _liveVsAlert, value); }
    public bool LiveGForceAlert { get => _liveGForceAlert; private set => SetProperty(ref _liveGForceAlert, value); }
    public bool LiveAglAlert { get => _liveAglAlert; private set => SetProperty(ref _liveAglAlert, value); }

    // Systems LED brushes
    public Brush LedBeaconBrush { get => _ledBeaconBrush; private set => SetProperty(ref _ledBeaconBrush, value); }
    public Brush LedStrobeBrush { get => _ledStrobeBrush; private set => SetProperty(ref _ledStrobeBrush, value); }
    public Brush LedLandingBrush { get => _ledLandingBrush; private set => SetProperty(ref _ledLandingBrush, value); }
    public Brush LedTaxiBrush { get => _ledTaxiBrush; private set => SetProperty(ref _ledTaxiBrush, value); }
    public Brush LedGearBrush { get => _ledGearBrush; private set => SetProperty(ref _ledGearBrush, value); }
    public string FlapsLabel { get => _flapsLabel; private set => SetProperty(ref _flapsLabel, value); }
    public Brush LedEng1Brush { get => _ledEng1Brush; private set => SetProperty(ref _ledEng1Brush, value); }
    public Brush LedEng2Brush { get => _ledEng2Brush; private set => SetProperty(ref _ledEng2Brush, value); }
    public Brush LedParkingBrakeBrush { get => _ledParkingBrakeBrush; private set => SetProperty(ref _ledParkingBrakeBrush, value); }

    public NavPage SelectedPage
    {
        get => _selectedPage;
        private set
        {
            if (!SetProperty(ref _selectedPage, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(IsDashboardVisible));
            RaisePropertyChanged(nameof(IsLiveMapVisible));
            RaisePropertyChanged(nameof(IsReviewVisible));
            RaisePropertyChanged(nameof(IsDiagnosticsVisible));
            RaisePropertyChanged(nameof(IsSettingsVisible));
            RaisePropertyChanged(nameof(DashboardNavUnderlineVisibility));
            RaisePropertyChanged(nameof(LiveMapNavUnderlineVisibility));
            RaisePropertyChanged(nameof(ReviewNavUnderlineVisibility));
            RaisePropertyChanged(nameof(DiagnosticsNavUnderlineVisibility));
            RaisePropertyChanged(nameof(SettingsNavUnderlineVisibility));

            // Start/stop the map polling based on whether the Live Map tab is visible.
            if (_liveMapService is not null)
            {
                if (value == NavPage.LiveMap)
                    _liveMapService.Start();
                else
                    _ = _liveMapService.StopAsync();
            }
        }
    }

    public bool IsDashboardVisible => SelectedPage == NavPage.Dashboard;
    public bool IsLiveMapVisible   => SelectedPage == NavPage.LiveMap;
    public bool IsReviewVisible => SelectedPage == NavPage.Review;
    public bool IsDiagnosticsVisible => SelectedPage == NavPage.Diagnostics;
    public bool IsSettingsVisible => SelectedPage == NavPage.Settings;

    public Visibility DashboardNavUnderlineVisibility => IsDashboardVisible ? Visibility.Visible : Visibility.Hidden;
    public Visibility LiveMapNavUnderlineVisibility   => IsLiveMapVisible   ? Visibility.Visible : Visibility.Hidden;
    public Visibility ReviewNavUnderlineVisibility => IsReviewVisible ? Visibility.Visible : Visibility.Hidden;
    public Visibility DiagnosticsNavUnderlineVisibility => IsDiagnosticsVisible ? Visibility.Visible : Visibility.Hidden;
    public Visibility SettingsNavUnderlineVisibility => IsSettingsVisible ? Visibility.Visible : Visibility.Hidden;

    public async Task InitializeAsync(Window owner)
    {
        await _shellHost.InitializeAsync();
        var snapshot = _shellHost.GetSnapshot();
        _latestSnapshot = snapshot;

        if (snapshot.RecoverySnapshot.HasRecoverableCurrentSession)
        {
            var recoveryDialog = new RecoveryDialog(snapshot.RecoverySnapshot)
            {
                Owner = owner,
            };

            recoveryDialog.ShowDialog();
            if (recoveryDialog.DiscardRequested)
            {
                await _shellHost.DiscardRecoveryAsync();
                snapshot = _shellHost.GetSnapshot();
                _latestSnapshot = snapshot;
            }
            else if (recoveryDialog.ResumeRequested)
            {
                snapshot = await _shellHost.ResumeRecoveryAsync();
                _latestSnapshot = snapshot;
            }
        }

        ApplySnapshot(snapshot);
        await RefreshAsync();
        _pollingTimer.Start();
    }

    public Task RetrySyncFromTrayAsync() => RetrySyncAsync();

    public string BuildTrayTooltip()
    {
        var phase = string.IsNullOrWhiteSpace(PhaseTitle) ? "IDLE" : PhaseTitle;
        var sim = string.IsNullOrWhiteSpace(MsfsStatusText) ? "SIM IDLE" : MsfsStatusText;
        return $"SimCrewOps Tracker | {phase} | {sim}";
    }

    private async Task RefreshAsync()
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;
        try
        {
            _latestSnapshot = await _shellHost.PollAsync();
            ApplySnapshot(_latestSnapshot);
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private async Task RetrySyncAsync()
    {
        await _shellHost.SyncNowAsync();
        if (_latestSnapshot is not null)
        {
            ApplySnapshot(await _shellHost.PollAsync());
        }
    }

    /// <summary>
    /// Called on the thread-pool whenever LiveMapService delivers a new fleet snapshot.
    /// Marshals the update onto the UI thread via LiveFlights; the view's canvas
    /// code-behind listens for PropertyChanged and redraws using its own ActualWidth/Height.
    /// </summary>
    private void OnLiveMapPositionsUpdated(object? sender, IReadOnlyList<SimCrewOps.Hosting.Models.LiveFlight> flights)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            LiveFlights = flights;
        });
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            var updatedSettings = SettingsEditor.ToSettings();
            await _shellHost.SaveSettingsAsync(updatedSettings);
            var hasToken = !string.IsNullOrWhiteSpace(updatedSettings.Api.PilotApiToken);
            SettingsSaveStatus = hasToken
                ? "Settings saved. Live position sync is now active."
                : "Settings saved. Enter your Pilot API Token to enable live position sync.";
            DiagnosticsStoragePath = updatedSettings.Storage.RootDirectory;
        }
        catch (Exception ex)
        {
            SettingsSaveStatus = $"Unable to save settings: {ex.Message}";
        }
    }

    private void ApplySnapshot(TrackerShellSnapshot snapshot)
    {
        var activeState = snapshot.RuntimeState;
        var telemetry = activeState?.LastTelemetryFrame;
        var phase = activeState?.CurrentPhase ?? FlightPhase.Preflight;

        MsfsStatusText = snapshot.SimConnectStatus.ConnectionState switch
        {
            SimConnectConnectionState.Connected => "MSFS CONNECTED",
            SimConnectConnectionState.WaitingForSimulatorProcess => "WAITING FOR SIM",
            SimConnectConnectionState.Connecting => "CONNECTING",
            SimConnectConnectionState.Faulted => "SIMCONNECT FAULT",
            SimConnectConnectionState.Disconnected => "DISCONNECTED",
            _ => "SIM IDLE",
        };
        MsfsStatusBrush = snapshot.SimConnectStatus.ConnectionState switch
        {
            SimConnectConnectionState.Connected => new SolidColorBrush(MediaColor.FromRgb(44, 122, 125)),
            SimConnectConnectionState.Faulted => new SolidColorBrush(MediaColor.FromRgb(212, 98, 90)),
            _ => new SolidColorBrush(MediaColor.FromRgb(56, 91, 105)),
        };

        SyncStatusText = snapshot.BackgroundSyncStatus?.Enabled == true
            ? (snapshot.BackgroundSyncStatus.LastErrorMessage is null ? "SYNCED" : "SYNC RETRY")
            : "SYNC READY";
        SyncStatusBrush = snapshot.BackgroundSyncStatus?.LastErrorMessage is null
            ? new SolidColorBrush(MediaColor.FromRgb(56, 91, 105))
            : new SolidColorBrush(MediaColor.FromRgb(212, 98, 90));

        LiveSyncStatusText = snapshot.LivePositionEnabled ? "LIVE SYNC ON" : "NOT CONNECTED";
        LiveSyncStatusBrush = snapshot.LivePositionEnabled
            ? new SolidColorBrush(MediaColor.FromRgb(44, 122, 125))
            : new SolidColorBrush(MediaColor.FromRgb(90, 106, 112));

        PhaseTitle = phase.ToString().ToUpperInvariant();
        HeaderFlightText = BuildHeaderFlightText(activeState, snapshot.ActiveFlight);
        PhaseSubtitle = BuildPhaseSubtitle(phase);
        PhaseStatusLine = BuildPhaseStatusLine(telemetry, phase);

        CareerTier = BuildProfileDisplay(activeState);
        BidDisplay = BuildBidDisplay(activeState, snapshot.ActiveFlight);
        ReputationDisplay = BuildModeDisplay(activeState);

        OutTime = FormatTime(activeState?.BlockTimes.BlocksOffUtc, "19:45");
        OffTime = FormatTime(activeState?.BlockTimes.WheelsOffUtc, "19:58");
        OnTime = FormatTime(activeState?.BlockTimes.WheelsOnUtc, "--:--");
        InTime = FormatTime(activeState?.BlockTimes.BlocksOnUtc, "--:--");

        UpdatePersistentInstruments(telemetry);
        PopulatePhasePills(phase);
        PopulateMetricTiles(phase, telemetry);
        PopulateStatusChips(phase, telemetry);
        PopulateScoreRows(activeState?.ScoreResult);

        ScoreText = activeState?.ScoreResult.FinalScore.ToString("0", CultureInfo.InvariantCulture) ?? "--";
        GradeText = activeState?.ScoreResult.Grade is { Length: > 0 } grade ? $"Grade {grade}" : "Grade --";
        ScoreSummary = activeState is null
            ? "No score available until a live session is in progress."
            : $"Phase subtotal {activeState.ScoreResult.PhaseSubtotal:0.#} with global deductions {activeState.ScoreResult.GlobalDeductions:0.#}.";

        AlertPrimaryTitle = activeState is null
            ? "Waiting for telemetry"
            : phase == FlightPhase.Approach ? "Stable by 500 AGL" : "Live phase monitoring";
        AlertPrimaryBody = activeState is null
            ? "The tracker will populate live alerts once SimConnect data is flowing."
            : phase == FlightPhase.Approach
            ? "VS, gear, and flap state actively monitored."
            : "The shell switches the board content by phase as live data updates.";

        var overspeedFindings = activeState?.ScoreResult.GlobalFindings.Count(f => f.Code.Contains("overspeed", StringComparison.OrdinalIgnoreCase)) ?? 0;
        AlertSecondaryTitle = activeState is null ? "No events yet" : overspeedFindings > 0 ? $"Overspeed x{overspeedFindings}" : "No overspeed events";
        AlertSecondaryBody = overspeedFindings > 0
            ? "Captured in descent below FL100."
            : activeState is null
            ? "Overspeed, stall, and GPWS events will appear during flight."
            : "No overspeed deductions are active in the current session.";

        DispatchTitle = activeState is null ? "Dispatch channel ready" : phase == FlightPhase.Approach ? "Expect ILS 27R" : "Dispatch channel ready";
        DispatchBody = activeState is null
            ? "Live dispatch and ACARS items will appear here when messaging is wired."
            : phase == FlightPhase.Approach
            ? "22:43 Dispatch updated winds to 270/18G24."
            : "ACARS and dispatch threads will surface here when messaging is wired.";

        SessionHealthTitle = activeState is null && snapshot.RecoverySnapshot.HasRecoverableCurrentSession ? "Recovery available" : "Autosave OK";
        SessionHealthLine1 = $"API Queue {snapshot.RecoverySnapshot.PendingCompletedSessions.Count}";
        SessionHealthLine2 = snapshot.BackgroundSyncStatus?.LastRunCompletedUtc is { } lastRun
            ? $"Last sync {lastRun.ToLocalTime():HH:mm}"
            : "Last sync --:--";
        SessionHealthLine3 = snapshot.SimConnectStatus.LastErrorMessage ?? "Runtime healthy";

        ReviewSummary = activeState?.IsComplete == true
            ? "Completed session ready for review."
            : "Live review reflects the current runtime snapshot.";
        ReviewLandingMetrics = BuildLandingMetrics(activeState);

        DiagnosticsSimState = $"{snapshot.SimConnectStatus.ConnectionState} {(snapshot.SimConnectStatus.LastErrorMessage is { Length: > 0 } err ? $"• {err}" : string.Empty)}".Trim();
        DiagnosticsSyncState = snapshot.BackgroundSyncStatus is null
            ? "Background sync disabled"
            : $"{snapshot.BackgroundSyncStatus.LastTrigger ?? "idle"} • failures {snapshot.BackgroundSyncStatus.ConsecutiveFailureCount}";
        DiagnosticsRecoveryState = activeState is not null && snapshot.RecoverySnapshot.HasRecoverableCurrentSession
            ? $"Resumed session active • last autosave {snapshot.RecoverySnapshot.CurrentSession!.SavedUtc.ToLocalTime():g}"
            : snapshot.RecoverySnapshot.HasRecoverableCurrentSession
            ? $"Recoverable current session saved {snapshot.RecoverySnapshot.CurrentSession!.SavedUtc.ToLocalTime():g}"
            : "No recoverable session";
        DiagnosticsSettingsPath = snapshot.SettingsFilePath;
        DiagnosticsStoragePath = snapshot.Settings.Storage.RootDirectory;
        DiagnosticsLastTelemetry = telemetry is null
            ? "No telemetry received yet"
            : $"IAS {telemetry.IndicatedAirspeedKnots:0} • VS {telemetry.VerticalSpeedFpm:0} • AGL {telemetry.AltitudeAglFeet:0}";

        TelemetryDiagnosticsEnabled = snapshot.Settings.Debug.EnableTelemetryDiagnostics;
        if (TelemetryDiagnosticsEnabled)
        {
            DiagnosticsTelemetryClient =
                $"{snapshot.SimConnectStatus.ClientPath} • raw {FormatTimeAgo(snapshot.SimConnectStatus.LastRawFrameUtc)} • mapped {FormatTimeAgo(snapshot.SimConnectStatus.LastTelemetryUtc)}";
            DiagnosticsTelemetryFlow =
                $"Flight-critical: {ToYesNo(snapshot.SimConnectStatus.HasReceivedFlightCriticalData)} • Operational: {ToYesNo(snapshot.SimConnectStatus.HasReceivedOperationalData)}";
            DiagnosticsTelemetryCounters =
                $"Polls {snapshot.SimConnectStatus.PollCount} • Null polls {snapshot.SimConnectStatus.NullPollCount} • Raw frames {snapshot.SimConnectStatus.RawFrameCount} • Mapped frames {snapshot.SimConnectStatus.TelemetryFrameCount}";
            DiagnosticsRawTelemetry = BuildRawTelemetryDebug(snapshot.LastRawTelemetryFrame);
        }
        else
        {
            DiagnosticsTelemetryClient = "Telemetry debug disabled in Settings.";
            DiagnosticsTelemetryFlow = "Enable telemetry diagnostics to inspect raw SimConnect frame flow.";
            DiagnosticsTelemetryCounters = "Poll, null-frame, and mapping counters will appear here.";
            DiagnosticsRawTelemetry = "Raw SimConnect values will appear here when telemetry diagnostics are enabled.";
        }
    }

    private void ApplySampleState()
    {
        PopulatePhasePills(FlightPhase.Preflight);
        PopulateMetricTiles(FlightPhase.Preflight, null);
        PopulateStatusChips(FlightPhase.Preflight, null);
        PopulateScoreRows(null);
    }

    private void PopulateMetricTiles(FlightPhase phase, TelemetryFrame? telemetry)
    {
        MetricTiles.Clear();

        foreach (var tile in BuildMetricTiles(phase, telemetry))
        {
            MetricTiles.Add(tile);
        }
    }

    private IReadOnlyList<MetricTileModel> BuildMetricTiles(FlightPhase phase, TelemetryFrame? telemetry) => phase switch
    {
        FlightPhase.Preflight or FlightPhase.TaxiOut or FlightPhase.TaxiIn => new[]
        {
            new MetricTileModel("GS", telemetry is null ? "-- kt" : $"{telemetry.GroundSpeedKnots:0} kt"),
            new MetricTileModel("HDG", telemetry is null ? "--°" : $"{telemetry.HeadingMagneticDegrees:0}°"),
            new MetricTileModel("TAXI LT", telemetry is null ? "--" : telemetry.TaxiLightsOn ? "ON" : "OFF"),
            new MetricTileModel("PB", telemetry is null ? "--" : telemetry.ParkingBrakeSet ? "SET" : "OFF"),
        },
        FlightPhase.Takeoff => new[]
        {
            new MetricTileModel("IAS", telemetry is null ? "-- kt" : $"{telemetry.IndicatedAirspeedKnots:0} kt"),
            new MetricTileModel("VS", telemetry is null ? "-- fpm" : $"{telemetry.VerticalSpeedFpm:0} fpm"),
            new MetricTileModel("PITCH", telemetry is null ? "--°" : $"{telemetry.PitchAngleDegrees:0.#}°"),
            new MetricTileModel("BANK", telemetry is null ? "--°" : $"{telemetry.BankAngleDegrees:0.#}°"),
            new MetricTileModel("G", telemetry is null ? "--" : $"{telemetry.GForce:0.00}"),
        },
        FlightPhase.Climb or FlightPhase.Cruise or FlightPhase.Descent => new[]
        {
            new MetricTileModel("IAS", telemetry is null ? "-- kt" : $"{telemetry.IndicatedAirspeedKnots:0} kt"),
            new MetricTileModel("ALT", telemetry is null ? "-- ft" : $"{telemetry.IndicatedAltitudeFeet:0} ft"),
            new MetricTileModel("VS", telemetry is null ? "-- fpm" : $"{telemetry.VerticalSpeedFpm:0} fpm", telemetry is { VerticalSpeedFpm: < -1000 or > 1000 }),
            new MetricTileModel("HDG", telemetry is null ? "--°" : $"{telemetry.HeadingMagneticDegrees:0}°"),
        },
        FlightPhase.Landing => new[]
        {
            new MetricTileModel("TD VS", telemetry is null ? "-- fpm" : $"{telemetry.VerticalSpeedFpm:0} fpm", telemetry is { VerticalSpeedFpm: < -600 }),
            new MetricTileModel("G", telemetry is null ? "--" : $"{telemetry.GForce:0.00}", telemetry is { GForce: > 1.5 }),
            new MetricTileModel("BANK", telemetry is null ? "--°" : $"{telemetry.BankAngleDegrees:0.#}°"),
            new MetricTileModel("PITCH", telemetry is null ? "--°" : $"{telemetry.PitchAngleDegrees:0.#}°"),
        },
        FlightPhase.Arrival => new[]
        {
            new MetricTileModel("PB", telemetry is null ? "--" : telemetry.ParkingBrakeSet ? "SET" : "OFF"),
            new MetricTileModel("ENG 1", telemetry is null ? "--" : telemetry.Engine1Running ? "ON" : "OFF"),
            new MetricTileModel("ENG 2", telemetry is null ? "--" : telemetry.Engine2Running ? "ON" : "OFF"),
            new MetricTileModel("TAXI LT", telemetry is null ? "--" : telemetry.TaxiLightsOn ? "ON" : "OFF"),
        },
        _ => new[]
        {
            new MetricTileModel("IAS", telemetry is null ? "-- kt" : $"{telemetry.IndicatedAirspeedKnots:0} kt"),
            new MetricTileModel("VS", telemetry is null ? "-- fpm" : $"{telemetry.VerticalSpeedFpm:0} fpm", telemetry is { VerticalSpeedFpm: < -1000 }),
            new MetricTileModel("ALT AGL", telemetry is null ? "-- ft" : $"{telemetry.AltitudeAglFeet:0} ft"),
            new MetricTileModel("G/S", "-- dot"),
            new MetricTileModel("DIST THR", "-- nm"),
            new MetricTileModel("LOC", "-- dot"),
        },
    };

    private void PopulateStatusChips(FlightPhase phase, TelemetryFrame? telemetry)
    {
        StatusChips.Clear();
        foreach (var chip in BuildStatusChips(phase, telemetry))
        {
            StatusChips.Add(chip);
        }
    }

    private IReadOnlyList<string> BuildStatusChips(FlightPhase phase, TelemetryFrame? telemetry) => phase switch
    {
        FlightPhase.Approach => new[]
        {
            "RUNWAY --",
            telemetry is null ? "GEAR --" : telemetry.GearDown ? "GEAR DOWN" : "GEAR UP",
            telemetry is null ? "FLAPS --" : $"FLAPS {telemetry.FlapsHandleIndex}",
            telemetry is null ? "500 --" : "500 OK",
        },
        FlightPhase.Landing => new[]
        {
            telemetry is null ? "LANDING --" : "TOUCHDOWN",
            telemetry is null ? "GEAR --" : telemetry.GearDown ? "GEAR DOWN" : "GEAR UP",
            telemetry is null ? "ROLLOUT --" : "ROLL OUT",
        },
        FlightPhase.Arrival => new[]
        {
            telemetry is null ? "PB --" : telemetry.ParkingBrakeSet ? "PB SET" : "PB OFF",
            telemetry is null ? "TAXI LT --" : telemetry.TaxiLightsOn ? "TAXI LT ON" : "TAXI LT OFF",
            telemetry is null ? "ENG --" : !telemetry.Engine1Running && !telemetry.Engine2Running ? "ENG OFF" : "ENG RUN",
        },
        _ => new[]
        {
            $"PHASE {phase.ToString().ToUpperInvariant()}",
            telemetry is null ? "LDG LT --" : telemetry.LandingLightsOn ? "LDG LT ON" : "LDG LT OFF",
            telemetry is null ? "STROBES --" : telemetry.StrobesOn ? "STROBES ON" : "STROBES OFF",
        },
    };

    private void PopulateScoreRows(ScoreResult? scoreResult)
    {
        PrimaryScoreRows.Clear();
        SecondaryScoreRows.Clear();
        ReviewScoreRows.Clear();

        var rows = scoreResult?.PhaseScores.Select(CreateScoreRow).ToList() ?? CreateSampleScoreRows();
        foreach (var row in rows.Take(2))
        {
            PrimaryScoreRows.Add(row);
        }

        foreach (var row in rows.Skip(2).Take(2))
        {
            SecondaryScoreRows.Add(row);
        }

        foreach (var row in rows)
        {
            ReviewScoreRows.Add(row);
        }
    }

    private static ScoreRowModel CreateScoreRow(PhaseScoreResult phaseScore)
    {
        var accentBrush = phaseScore.Phase == FlightPhase.Approach
            ? new SolidColorBrush(MediaColor.FromRgb(243, 169, 106))
            : new SolidColorBrush(MediaColor.FromRgb(54, 130, 139));
        var fillWidth = phaseScore.MaxPoints <= 0
            ? 0
            : 112 * (phaseScore.AwardedPoints / phaseScore.MaxPoints);

        return new ScoreRowModel(
            phaseScore.Phase switch
            {
                FlightPhase.TaxiOut => "Taxi Out",
                _ => phaseScore.Phase.ToString(),
            },
            phaseScore.MaxPoints <= 0
                ? "--"
                : $"{phaseScore.AwardedPoints:0.#}/{phaseScore.MaxPoints:0.#}",
            fillWidth,
            accentBrush);
    }

    private static List<ScoreRowModel> CreateSampleScoreRows() =>
        new()
        {
            new ScoreRowModel("Preflight", "--", 0, new SolidColorBrush(MediaColor.FromRgb(54, 130, 139))),
            new ScoreRowModel("Taxi Out", "--", 0, new SolidColorBrush(MediaColor.FromRgb(54, 130, 139))),
            new ScoreRowModel("Approach", "--", 0, new SolidColorBrush(MediaColor.FromRgb(243, 169, 106))),
            new ScoreRowModel("Landing", "--", 0, new SolidColorBrush(MediaColor.FromRgb(217, 223, 214))),
        };

    private void PopulatePhasePills(FlightPhase currentPhase)
    {
        PhasePills.Clear();
        foreach (var phase in Enum.GetValues<FlightPhase>())
        {
            PhasePills.Add(new PhasePillModel(ToPhaseLabel(phase), phase == currentPhase));
        }
    }

    private static string ToPhaseLabel(FlightPhase phase) => phase switch
    {
        FlightPhase.Preflight => "PREFLT",
        FlightPhase.TaxiOut => "TAXI",
        FlightPhase.Takeoff => "TO",
        FlightPhase.Climb => "CLB",
        FlightPhase.Cruise => "CRZ",
        FlightPhase.Descent => "DES",
        FlightPhase.Approach => "APP",
        FlightPhase.Landing => "LDG",
        FlightPhase.TaxiIn => "TAXI IN",
        FlightPhase.Arrival => "ARR",
        _ => phase.ToString().ToUpperInvariant(),
    };

    private static string BuildPhaseSubtitle(FlightPhase phase) => phase switch
    {
        FlightPhase.Approach => "Fast, readable, and phase-specific. Diagram moved to post-flight web debrief.",
        FlightPhase.Takeoff => "Takeoff board prioritizes IAS, VS, pitch, bank, and G-force.",
        FlightPhase.Arrival => "Arrival board focuses on brake, engine, and light compliance.",
        _ => "The shell adjusts the live board to match the active flight phase.",
    };

    private static string BuildPhaseStatusLine(TelemetryFrame? telemetry, FlightPhase phase) => phase switch
    {
        FlightPhase.Approach => "Stable descent • runway assigned • landing metrics armed",
        FlightPhase.Takeoff => "Rotation and climb safety limits monitored in real time",
        FlightPhase.Arrival => "Arrival sequence checks: taxi lights, parking brake, engines off",
        _ => telemetry is null
            ? "Waiting for telemetry"
            : $"IAS {telemetry.IndicatedAirspeedKnots:0} • VS {telemetry.VerticalSpeedFpm:0} • HDG {telemetry.HeadingMagneticDegrees:0}",
    };

    private static string BuildLandingMetrics(FlightSessionRuntimeState? activeState)
    {
        var landing = activeState?.ScoreInput.Landing;
        if (landing is null)
        {
            return "VS --   G --   Bank --   Pitch --   TZ Excess --";
        }

        return $"VS {landing.TouchdownVerticalSpeedFpm:0}   G {landing.TouchdownGForce:0.00}   Bank {landing.TouchdownBankAngleDegrees:0.#}   Pitch {landing.TouchdownPitchAngleDegrees:0.#}   TZ Excess {landing.TouchdownZoneExcessDistanceFeet:0}";
    }

    private static string BuildHeaderFlightText(
        FlightSessionRuntimeState? activeState,
        ActiveFlightResponse? activeFlight = null)
    {
        // Active session in progress — use the live runtime context.
        if (activeState is not null)
        {
            var departure = string.IsNullOrWhiteSpace(activeState.Context.DepartureAirportIcao)
                ? "----"
                : activeState.Context.DepartureAirportIcao!.ToUpperInvariant();
            var arrival = string.IsNullOrWhiteSpace(activeState.Context.ArrivalAirportIcao)
                ? "----"
                : activeState.Context.ArrivalAirportIcao!.ToUpperInvariant();
            return $"{departure} to {arrival} • {BuildModeDisplay(activeState)} • {BuildProfileDisplay(activeState)}";
        }

        // No active session yet — show the assignment loaded from the web app if available.
        if (activeFlight is not null)
        {
            var dep = (activeFlight.Departure ?? "----").ToUpperInvariant();
            var arr = (activeFlight.Arrival ?? "----").ToUpperInvariant();
            var fn  = string.IsNullOrWhiteSpace(activeFlight.FlightNumber) ? "" : $" • {activeFlight.FlightNumber.ToUpperInvariant()}";
            var ac  = string.IsNullOrWhiteSpace(activeFlight.AircraftType)  ? "" : $" • {activeFlight.AircraftType}";
            return $"{dep} to {arr}{fn}{ac} • Career Mode";
        }

        return "Waiting for flight context • MSFS telemetry will populate here";
    }

    private static string BuildBidDisplay(
        FlightSessionRuntimeState? activeState,
        ActiveFlightResponse? activeFlight = null)
    {
        if (!string.IsNullOrWhiteSpace(activeState?.Context.BidId))
            return $"Bid #{activeState!.Context.BidId}";

        if (!string.IsNullOrWhiteSpace(activeFlight?.FlightNumber))
            return activeFlight!.FlightNumber.ToUpperInvariant();

        return "Free Flight";
    }

    private static string BuildProfileDisplay(FlightSessionRuntimeState? activeState)
    {
        if (activeState is null)
        {
            return "Standard Profile";
        }

        var profile = activeState.Context.Profile;
        if (profile.HeavyFourEngineAircraft)
        {
            return "Heavy 4-Engine";
        }

        return profile.EngineCount > 0
            ? $"{profile.EngineCount}-Engine Profile"
            : "Standard Profile";
    }

    private static string BuildModeDisplay(FlightSessionRuntimeState? activeState)
    {
        var mode = activeState?.Context.FlightMode;
        return string.Equals(mode, "career", StringComparison.OrdinalIgnoreCase)
            ? "Career Mode"
            : "Free Flight";
    }

    private static string FormatTime(DateTimeOffset? value, string fallback) =>
        value?.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture) ?? fallback;

    private static string ToYesNo(bool value) => value ? "yes" : "no";

    private static string FormatTimeAgo(DateTimeOffset? value) =>
        value is null
            ? "never"
            : $"{Math.Max(0, (DateTimeOffset.UtcNow - value.Value).TotalSeconds):0.#}s ago";

    private static string BuildRawTelemetryDebug(SimConnectRawTelemetryFrame? rawFrame)
    {
        if (rawFrame is null)
        {
            return "No raw SimConnect frame received yet.";
        }

        var lightSource = rawFrame.LightSourceIsIndividual ? "individual SimVars" : "LIGHT STATES bitmask fallback";
        var bridgeStatus = rawFrame.LvarBridgeRequired
            ? rawFrame.LvarBridgeConnected ? "LVAR bridge: connected ✓" : "LVAR bridge: NOT connected — install MobiFlight WASM"
            : "LVAR bridge: not needed";
        return
            $"Aircraft profile: {rawFrame.ActiveProfileName}  •  {bridgeStatus}\n" +
            $"HDG MAG {rawFrame.HeadingMagneticDegrees:0.##} • HDG TRUE {rawFrame.HeadingTrueDegrees:0.##} • AGL {rawFrame.AltitudeAglFeet:0.##} • ALT {rawFrame.AltitudeFeet:0.##} • ON GND {rawFrame.OnGround:0} • PB {rawFrame.ParkingBrakePosition:0}\n" +
            $"GEAR HANDLE raw={rawFrame.GearPosition:0.000}  (0.000=up  1.000=down)  FLAPS idx={rawFrame.FlapsHandleIndex:0}\n" +
            $"LIGHT source: {lightSource}\n" +
            $"  individual  => BCN {rawFrame.LightBeaconRaw} • TAXI {rawFrame.LightTaxiRaw} • LDG {rawFrame.LightLandingRaw} • STB {rawFrame.LightStrobeRaw}\n" +
            $"  bitmask raw => 0x{rawFrame.LightStatesRaw:X4}  (BCN=0x0002 LAND=0x0004 TAXI=0x0008 STB=0x0010)\n" +
            $"  final used  => BCN {rawFrame.BeaconLightOn:0} • TAXI {rawFrame.TaxiLightsOn:0} • LDG {rawFrame.LandingLightsOn:0} • STB {rawFrame.StrobesOn:0}\n" +
            $"IAS {rawFrame.IndicatedAirspeedKnots:0.##} • GS {rawFrame.GroundSpeedKnots:0.##} • VS {rawFrame.VerticalSpeedFpm:0.##}";
    }

    private void UpdatePersistentInstruments(TelemetryFrame? telemetry)
    {
        if (telemetry is null)
        {
            LiveIas = "---";
            LiveGs = "---";
            LiveMach = "-.---";
            LiveAlt = "-----";
            LiveAgl = "-----";
            LiveVs = "----";
            LiveHdg = "---°";
            LiveGForce = "-.--";
            LiveVsAlert = false;
            LiveGForceAlert = false;
            LiveAglAlert = false;
            LedBeaconBrush = LedDimBrush();
            LedStrobeBrush = LedDimBrush();
            LedLandingBrush = LedDimBrush();
            LedTaxiBrush = LedDimBrush();
            LedGearBrush = LedDimBrush();
            FlapsLabel = "F--";
            LedEng1Brush = LedDimBrush();
            LedEng2Brush = LedDimBrush();
            LedParkingBrakeBrush = LedDimBrush();
            return;
        }

        LiveIas = $"{telemetry.IndicatedAirspeedKnots:0}";
        LiveGs = $"{telemetry.GroundSpeedKnots:0}";
        LiveMach = $"{telemetry.Mach:0.000}";
        LiveAlt = $"{telemetry.IndicatedAltitudeFeet:0}";
        LiveAgl = $"{telemetry.AltitudeAglFeet:0}";
        LiveVs = telemetry.VerticalSpeedFpm >= 0
            ? $"+{telemetry.VerticalSpeedFpm:0}"
            : $"{telemetry.VerticalSpeedFpm:0}";
        LiveHdg = $"{telemetry.HeadingMagneticDegrees:0}°";
        LiveGForce = $"{telemetry.GForce:0.00}";

        LiveVsAlert = telemetry.VerticalSpeedFpm < -1500;
        LiveGForceAlert = telemetry.GForce > 2.0 || telemetry.GForce < 0.5;
        LiveAglAlert = !telemetry.OnGround && telemetry.AltitudeAglFeet < 50 && telemetry.VerticalSpeedFpm < -200;

        LedBeaconBrush = LedOnBrush(telemetry.BeaconLightOn);
        LedStrobeBrush = LedOnBrush(telemetry.StrobesOn);
        LedLandingBrush = LedOnBrush(telemetry.LandingLightsOn);
        LedTaxiBrush = LedOnBrush(telemetry.TaxiLightsOn);
        // Gear LED: green = handle down (≥ 0.5), dim = handle up (< 0.5).
        // Source is GEAR HANDLE POSITION (bool 0/1), so no true in-transit state.
        // Dim clearly means "gear is stowed" — previously both states were lit which was confusing.
        LedGearBrush = telemetry.GearPosition >= 0.5
            ? new SolidColorBrush(MediaColor.FromRgb(98, 245, 176)) // green: handle down
            : LedDimBrush();                                         // dim: handle up
        FlapsLabel = $"F{telemetry.FlapsHandleIndex}";
        LedEng1Brush = telemetry.Engine1Running
            ? new SolidColorBrush(MediaColor.FromRgb(98, 245, 176))
            : new SolidColorBrush(MediaColor.FromRgb(212, 98, 90));
        LedEng2Brush = telemetry.Engine2Running
            ? new SolidColorBrush(MediaColor.FromRgb(98, 245, 176))
            : new SolidColorBrush(MediaColor.FromRgb(212, 98, 90));
        LedParkingBrakeBrush = LedOnBrush(telemetry.ParkingBrakeSet, MediaColor.FromRgb(243, 169, 106));
    }

    private static Brush LedDimBrush() => new SolidColorBrush(MediaColor.FromRgb(42, 64, 80));
    private static Brush LedOnBrush(bool on) => on
        ? new SolidColorBrush(MediaColor.FromRgb(98, 245, 176))
        : LedDimBrush();
    private static Brush LedOnBrush(bool on, MediaColor onColor) => on
        ? new SolidColorBrush(onColor)
        : LedDimBrush();
}
