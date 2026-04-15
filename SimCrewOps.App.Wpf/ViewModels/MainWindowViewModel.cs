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
using SimCrewOps.Tracking.Models;
using Brush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;

namespace SimCrewOps.App.Wpf.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly TrackerShellHost _shellHost;
    private readonly DispatcherTimer _pollingTimer;

    private TrackerShellSnapshot? _latestSnapshot;
    private bool _isRefreshing;
    private NavPage _selectedPage = NavPage.Dashboard;
    private string _msfsStatusText = "WAITING FOR SIM";
    private Brush _msfsStatusBrush = new SolidColorBrush(MediaColor.FromRgb(56, 91, 105));
    private string _syncStatusText = "SYNC READY";
    private Brush _syncStatusBrush = new SolidColorBrush(MediaColor.FromRgb(56, 91, 105));
    private string _headerFlightText = "Waiting for flight context • MSFS telemetry will populate here";
    private string _phaseTitle = "APPROACH";
    private string _phaseSubtitle = "Fast, readable, and phase-specific. Diagram moved to post-flight web debrief.";
    private string _phaseStatusLine = "Stable descent • runway assigned • landing metrics armed";
    private string _careerTier = "Standard Profile";
    private string _bidDisplay = "Free Flight";
    private string _reputationDisplay = "Free Flight";
    private string _outTime = "19:45";
    private string _offTime = "19:58";
    private string _onTime = "--:--";
    private string _inTime = "--:--";
    private string _scoreText = "88";
    private string _gradeText = "Grade B";
    private string _scoreSummary = "Approach performance is good. Landing card unlocks after touchdown.";
    private string _alertPrimaryTitle = "Stable by 500 AGL";
    private string _alertPrimaryBody = "VS, gear, and flap state actively monitored.";
    private string _alertSecondaryTitle = "Overspeed x1";
    private string _alertSecondaryBody = "Captured in descent below FL100.";
    private string _dispatchTitle = "Expect ILS 27R";
    private string _dispatchBody = "22:43 Dispatch updated winds to 270/18G24.";
    private string _sessionHealthTitle = "Autosave OK";
    private string _sessionHealthLine1 = "API Queue 0";
    private string _sessionHealthLine2 = "Last sync 22:41";
    private string _sessionHealthLine3 = "Runtime healthy";
    private string _departureIcao = "----";
    private string _departureName = string.Empty;
    private string _arrivalIcao = "----";
    private string _arrivalName = string.Empty;
    private string _aircraftType = "—";
    private bool _beaconLightOn;
    private bool _taxiLightOn;
    private bool _strobeLightOn;
    private bool _landingLightOn;
    private bool _landingLightViolation;
    private bool _parkingBrakeSet;
    private bool _landingRecorded;
    private string _touchdownVS = "--";
    private string _touchdownBank = "--";
    private string _touchdownIAS = "--";
    private string _touchdownPitch = "--";
    private string _touchdownGForce = "--";
    private string _touchdownBounces = "--";
    private string _tdzDistanceFt = "--";
    private string _tdzCrossTrackFt = "--";
    private string _tdzExcessFt = "--";
    private string _landingRunway = string.Empty;
    private double _scoreBarWidth;
    private string _alertType = "APPROACH CHECK";
    private string _reviewSummary = "Landing metrics will populate after touchdown.";
    private string _reviewLandingMetrics = "VS --   G --   Bank --   Pitch --   TZ Excess --";
    private string _diagnosticsSimState = "Waiting for simulator process";
    private string _diagnosticsSyncState = "Background sync not yet initialized";
    private string _diagnosticsRecoveryState = "No recoverable session";
    private string _diagnosticsSettingsPath = string.Empty;
    private string _diagnosticsStoragePath = string.Empty;
    private string _diagnosticsLastTelemetry = "No telemetry received yet";
    private string _settingsSaveStatus = "Changes are saved for the next launch.";

    public MainWindowViewModel(TrackerShellBootstrapResult bootstrap)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);

        _shellHost = bootstrap.ShellHost;
        SettingsEditor = SettingsEditorViewModel.FromSettings(bootstrap.Settings);
        _diagnosticsSettingsPath = bootstrap.SettingsFilePath;
        _diagnosticsStoragePath = bootstrap.Settings.Storage.RootDirectory;

        MetricTiles = new ObservableCollection<MetricTileModel>();
        StatusChips = new ObservableCollection<SecondaryMetricModel>();
        PrimaryScoreRows = new ObservableCollection<ScoreRowModel>();
        SecondaryScoreRows = new ObservableCollection<ScoreRowModel>();
        ReviewScoreRows = new ObservableCollection<ScoreRowModel>();
        PhasePills = new ObservableCollection<PhasePillModel>();

        ShowDashboardCommand = new RelayCommand(() => SelectedPage = NavPage.Dashboard);
        ShowReviewCommand = new RelayCommand(() => SelectedPage = NavPage.Review);
        ShowDiagnosticsCommand = new RelayCommand(() => SelectedPage = NavPage.Diagnostics);
        ShowSettingsCommand = new RelayCommand(() => SelectedPage = NavPage.Settings);
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
    public ObservableCollection<SecondaryMetricModel> StatusChips { get; }
    public ObservableCollection<ScoreRowModel> PrimaryScoreRows { get; }
    public ObservableCollection<ScoreRowModel> SecondaryScoreRows { get; }
    public ObservableCollection<ScoreRowModel> ReviewScoreRows { get; }
    public ObservableCollection<PhasePillModel> PhasePills { get; }

    public RelayCommand ShowDashboardCommand { get; }
    public RelayCommand ShowReviewCommand { get; }
    public RelayCommand ShowDiagnosticsCommand { get; }
    public RelayCommand ShowSettingsCommand { get; }
    public RelayCommand RetrySyncCommand { get; }
    public RelayCommand SaveSettingsCommand { get; }

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

    public string SettingsSaveStatus
    {
        get => _settingsSaveStatus;
        private set => SetProperty(ref _settingsSaveStatus, value);
    }

    public string DepartureIcao
    {
        get => _departureIcao;
        private set => SetProperty(ref _departureIcao, value);
    }

    public string DepartureName
    {
        get => _departureName;
        private set => SetProperty(ref _departureName, value);
    }

    public string ArrivalIcao
    {
        get => _arrivalIcao;
        private set => SetProperty(ref _arrivalIcao, value);
    }

    public string ArrivalName
    {
        get => _arrivalName;
        private set => SetProperty(ref _arrivalName, value);
    }

    public string AircraftType
    {
        get => _aircraftType;
        private set => SetProperty(ref _aircraftType, value);
    }

    public bool BeaconLightOn
    {
        get => _beaconLightOn;
        private set => SetProperty(ref _beaconLightOn, value);
    }

    public bool TaxiLightOn
    {
        get => _taxiLightOn;
        private set => SetProperty(ref _taxiLightOn, value);
    }

    public bool StrobeLightOn
    {
        get => _strobeLightOn;
        private set => SetProperty(ref _strobeLightOn, value);
    }

    public bool LandingLightOn
    {
        get => _landingLightOn;
        private set => SetProperty(ref _landingLightOn, value);
    }

    public bool LandingLightViolation
    {
        get => _landingLightViolation;
        private set => SetProperty(ref _landingLightViolation, value);
    }

    public bool ParkingBrakeSet
    {
        get => _parkingBrakeSet;
        private set => SetProperty(ref _parkingBrakeSet, value);
    }

    public bool LandingRecorded
    {
        get => _landingRecorded;
        private set => SetProperty(ref _landingRecorded, value);
    }

    public string TouchdownVS
    {
        get => _touchdownVS;
        private set => SetProperty(ref _touchdownVS, value);
    }

    public string TouchdownBank
    {
        get => _touchdownBank;
        private set => SetProperty(ref _touchdownBank, value);
    }

    public string TouchdownIAS
    {
        get => _touchdownIAS;
        private set => SetProperty(ref _touchdownIAS, value);
    }

    public string TouchdownPitch
    {
        get => _touchdownPitch;
        private set => SetProperty(ref _touchdownPitch, value);
    }

    public string TouchdownGForce
    {
        get => _touchdownGForce;
        private set => SetProperty(ref _touchdownGForce, value);
    }

    public string TouchdownBounces
    {
        get => _touchdownBounces;
        private set => SetProperty(ref _touchdownBounces, value);
    }

    public string TdzDistanceFt
    {
        get => _tdzDistanceFt;
        private set => SetProperty(ref _tdzDistanceFt, value);
    }

    public string TdzCrossTrackFt
    {
        get => _tdzCrossTrackFt;
        private set => SetProperty(ref _tdzCrossTrackFt, value);
    }

    public string TdzExcessFt
    {
        get => _tdzExcessFt;
        private set => SetProperty(ref _tdzExcessFt, value);
    }

    public string LandingRunway
    {
        get => _landingRunway;
        private set => SetProperty(ref _landingRunway, value);
    }

    public double ScoreBarWidth
    {
        get => _scoreBarWidth;
        private set => SetProperty(ref _scoreBarWidth, value);
    }

    public string AlertType
    {
        get => _alertType;
        private set => SetProperty(ref _alertType, value);
    }

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
            RaisePropertyChanged(nameof(IsReviewVisible));
            RaisePropertyChanged(nameof(IsDiagnosticsVisible));
            RaisePropertyChanged(nameof(IsSettingsVisible));
            RaisePropertyChanged(nameof(DashboardNavUnderlineVisibility));
            RaisePropertyChanged(nameof(ReviewNavUnderlineVisibility));
            RaisePropertyChanged(nameof(DiagnosticsNavUnderlineVisibility));
            RaisePropertyChanged(nameof(SettingsNavUnderlineVisibility));
        }
    }

    public bool IsDashboardVisible => SelectedPage == NavPage.Dashboard;
    public bool IsReviewVisible => SelectedPage == NavPage.Review;
    public bool IsDiagnosticsVisible => SelectedPage == NavPage.Diagnostics;
    public bool IsSettingsVisible => SelectedPage == NavPage.Settings;

    public Visibility DashboardNavUnderlineVisibility => IsDashboardVisible ? Visibility.Visible : Visibility.Hidden;
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

    private async Task SaveSettingsAsync()
    {
        try
        {
            var updatedSettings = SettingsEditor.ToSettings();
            await _shellHost.SaveSettingsAsync(updatedSettings);
            SettingsSaveStatus = "Settings saved. Restart the tracker to apply connection changes.";
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
        var phase = activeState?.CurrentPhase ?? FlightPhase.Approach;

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

        PhaseTitle = phase.ToString().ToUpperInvariant();
        HeaderFlightText = BuildHeaderFlightText(activeState);
        PhaseSubtitle = BuildPhaseSubtitle(phase);
        PhaseStatusLine = BuildPhaseStatusLine(telemetry, phase);

        CareerTier = BuildProfileDisplay(activeState);
        BidDisplay = string.IsNullOrWhiteSpace(activeState?.Context.BidId) ? "Free Flight" : $"Bid #{activeState!.Context.BidId}";
        ReputationDisplay = BuildModeDisplay(activeState);

        DepartureIcao = activeState?.Context.DepartureAirportIcao?.ToUpperInvariant() ?? "----";
        DepartureName = string.Empty;
        ArrivalIcao = activeState?.Context.ArrivalAirportIcao?.ToUpperInvariant() ?? "----";
        ArrivalName = string.Empty;
        AircraftType = BuildAircraftTypeDisplay(activeState);

        BeaconLightOn = telemetry?.BeaconLightOn == true;
        TaxiLightOn = telemetry?.TaxiLightsOn == true;
        StrobeLightOn = telemetry?.StrobesOn == true;
        LandingLightOn = telemetry?.LandingLightsOn == true;
        LandingLightViolation = telemetry?.LandingLightsOn == true
            && (phase == FlightPhase.TaxiIn || phase == FlightPhase.Arrival);
        ParkingBrakeSet = telemetry?.ParkingBrakeSet == true;

        var landingMetrics = activeState?.ScoreInput.Landing;
        LandingRecorded = landingMetrics is { TouchdownVerticalSpeedFpm: not 0 };
        TouchdownVS = landingMetrics is not null ? $"{landingMetrics.TouchdownVerticalSpeedFpm:0}" : "--";
        TouchdownBank = landingMetrics is not null ? $"{landingMetrics.TouchdownBankAngleDegrees:0.#}" : "--";
        TouchdownIAS = landingMetrics is not null ? $"{landingMetrics.TouchdownIndicatedAirspeedKnots:0}" : "--";
        TouchdownPitch = landingMetrics is not null ? $"{landingMetrics.TouchdownPitchAngleDegrees:0.#}" : "--";
        TouchdownGForce = landingMetrics is not null ? $"{landingMetrics.TouchdownGForce:0.00}" : "--";
        TouchdownBounces = landingMetrics is not null ? $"{landingMetrics.BounceCount}" : "--";

        var runway = activeState?.LandingRunwayResolution;
        LandingRunway = runway is not null ? $"{runway.AirportIcao} {runway.Runway.RunwayIdentifier}" : string.Empty;
        TdzDistanceFt = runway is not null ? $"{runway.Projection.DistanceFromThresholdFeet:0}" : "--";
        TdzCrossTrackFt = runway is not null ? $"{Math.Abs(runway.Projection.CrossTrackDistanceFeet):0}" : "--";
        TdzExcessFt = runway is not null && runway.Projection.TouchdownZoneExcessDistanceFeet > 0
            ? $"{runway.Projection.TouchdownZoneExcessDistanceFeet:0}"
            : "0";

        var score = activeState?.ScoreResult.FinalScore ?? 88;
        ScoreBarWidth = Math.Clamp(score / 100.0 * 200.0, 0, 200);

        AlertType = phase switch
        {
            FlightPhase.Approach => "APPROACH CHECK",
            FlightPhase.Landing => "LANDING",
            FlightPhase.TaxiIn or FlightPhase.Arrival => "TAXI / ARRIVAL",
            _ => "PHASE MONITOR",
        };

        OutTime = FormatTime(activeState?.BlockTimes.BlocksOffUtc, "19:45");
        OffTime = FormatTime(activeState?.BlockTimes.WheelsOffUtc, "19:58");
        OnTime = FormatTime(activeState?.BlockTimes.WheelsOnUtc, "--:--");
        InTime = FormatTime(activeState?.BlockTimes.BlocksOnUtc, "--:--");

        PopulatePhasePills(phase);
        PopulateMetricTiles(phase, telemetry);
        PopulateStatusChips(phase, telemetry);
        PopulateScoreRows(activeState?.ScoreResult);

        ScoreText = activeState?.ScoreResult.FinalScore.ToString("0", CultureInfo.InvariantCulture) ?? "88";
        GradeText = activeState?.ScoreResult.Grade is { Length: > 0 } grade ? $"Grade {grade}" : "Grade B";
        ScoreSummary = activeState is null
            ? "Approach performance is good. Landing card unlocks after touchdown."
            : $"Phase subtotal {activeState.ScoreResult.PhaseSubtotal:0.#} with global deductions {activeState.ScoreResult.GlobalDeductions:0.#}.";

        AlertPrimaryTitle = phase == FlightPhase.Approach ? "Stable by 500 AGL" : "Live phase monitoring";
        AlertPrimaryBody = phase == FlightPhase.Approach
            ? "VS, gear, and flap state actively monitored."
            : "The shell switches the board content by phase as live data updates.";

        var overspeedFindings = activeState?.ScoreResult.GlobalFindings.Count(f => f.Code.Contains("overspeed", StringComparison.OrdinalIgnoreCase)) ?? 0;
        AlertSecondaryTitle = overspeedFindings > 0 ? $"Overspeed x{overspeedFindings}" : "No overspeed events";
        AlertSecondaryBody = overspeedFindings > 0
            ? "Captured in descent below FL100."
            : "No overspeed deductions are active in the current session.";

        DispatchTitle = phase == FlightPhase.Approach ? "Expect ILS 27R" : "Dispatch channel ready";
        DispatchBody = phase == FlightPhase.Approach
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
    }

    private void ApplySampleState()
    {
        PopulatePhasePills(FlightPhase.Approach);
        PopulateMetricTiles(FlightPhase.Approach, null);
        PopulateStatusChips(FlightPhase.Approach, null);
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
            new MetricTileModel("GS", $"{telemetry?.GroundSpeedKnots ?? 12:0} kt"),
            new MetricTileModel("HDG", $"{telemetry?.HeadingTrueDegrees ?? 224:0}°"),
            new MetricTileModel("TAXI LT", telemetry?.TaxiLightsOn == false ? "OFF" : "ON"),
            new MetricTileModel("PB", telemetry?.ParkingBrakeSet == true ? "SET" : "OFF"),
        },
        FlightPhase.Takeoff => new[]
        {
            new MetricTileModel("IAS", $"{telemetry?.IndicatedAirspeedKnots ?? 142:0} kt"),
            new MetricTileModel("VS", $"{telemetry?.VerticalSpeedFpm ?? 1650:0} fpm"),
            new MetricTileModel("PITCH", $"{telemetry?.PitchAngleDegrees ?? 11:0.#}°"),
            new MetricTileModel("BANK", $"{telemetry?.BankAngleDegrees ?? 2.1:0.#}°"),
            new MetricTileModel("G", $"{telemetry?.GForce ?? 1.2:0.00}"),
        },
        FlightPhase.Climb or FlightPhase.Cruise or FlightPhase.Descent => new[]
        {
            new MetricTileModel("IAS", $"{telemetry?.IndicatedAirspeedKnots ?? 278:0} kt"),
            new MetricTileModel("ALT", $"{telemetry?.IndicatedAltitudeFeet ?? 24000:0} ft"),
            new MetricTileModel("VS", $"{telemetry?.VerticalSpeedFpm ?? -680:0} fpm", telemetry is { VerticalSpeedFpm: < -1000 or > 1000 }),
            new MetricTileModel("HDG", $"{telemetry?.HeadingTrueDegrees ?? 222:0}°"),
        },
        FlightPhase.Landing => new[]
        {
            new MetricTileModel("TD VS", $"{telemetry?.VerticalSpeedFpm ?? -412:0} fpm", telemetry is { VerticalSpeedFpm: < -600 }),
            new MetricTileModel("G", $"{telemetry?.GForce ?? 1.31:0.00}", telemetry is { GForce: > 1.5 }),
            new MetricTileModel("BANK", $"{telemetry?.BankAngleDegrees ?? 1.5:0.#}°"),
            new MetricTileModel("PITCH", $"{telemetry?.PitchAngleDegrees ?? 2.0:0.#}°"),
        },
        FlightPhase.Arrival => new[]
        {
            new MetricTileModel("PB", telemetry?.ParkingBrakeSet == true ? "SET" : "OFF"),
            new MetricTileModel("ENG 1", telemetry?.Engine1Running == false ? "OFF" : "ON"),
            new MetricTileModel("ENG 2", telemetry?.Engine2Running == false ? "OFF" : "ON"),
            new MetricTileModel("TAXI LT", telemetry?.TaxiLightsOn == false ? "OFF" : "ON"),
        },
        _ => new[]
        {
            new MetricTileModel("IAS", $"{telemetry?.IndicatedAirspeedKnots ?? 142:0} kt"),
            new MetricTileModel("VS", $"{telemetry?.VerticalSpeedFpm ?? -680:0} fpm", telemetry is { VerticalSpeedFpm: < -1000 }),
            new MetricTileModel("ALT AGL", $"{telemetry?.AltitudeAglFeet ?? 1900:0} ft"),
            new MetricTileModel("G/S", "-0.2 dot"),
            new MetricTileModel("DIST THR", "4.2 nm"),
            new MetricTileModel("LOC", "+0.1 dot"),
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

    private IReadOnlyList<SecondaryMetricModel> BuildStatusChips(FlightPhase phase, TelemetryFrame? telemetry) => phase switch
    {
        FlightPhase.Approach => new[]
        {
            new SecondaryMetricModel("RUNWAY", "27R"),
            new SecondaryMetricModel("GEAR", telemetry?.GearDown == false ? "UP" : "DOWN"),
            new SecondaryMetricModel("FLAPS", $"{telemetry?.FlapsHandleIndex ?? 2}"),
            new SecondaryMetricModel("500 AGL", "OK"),
        },
        FlightPhase.Landing => new[]
        {
            new SecondaryMetricModel("STATE", "TOUCHDOWN"),
            new SecondaryMetricModel("GEAR", telemetry?.GearDown == false ? "UP" : "DOWN"),
            new SecondaryMetricModel("ROLLOUT", "ACTIVE"),
        },
        FlightPhase.Arrival => new[]
        {
            new SecondaryMetricModel("PARK BRAKE", telemetry?.ParkingBrakeSet == true ? "SET" : "OFF"),
            new SecondaryMetricModel("TAXI LT", telemetry?.TaxiLightsOn == false ? "OFF" : "ON"),
            new SecondaryMetricModel("ENGINES", telemetry?.Engine1Running == false && telemetry?.Engine2Running == false ? "OFF" : "RUN"),
        },
        _ => new[]
        {
            new SecondaryMetricModel("GS", $"{telemetry?.GroundSpeedKnots ?? 0:0} kt"),
            new SecondaryMetricModel("LDG LT", telemetry?.LandingLightsOn == true ? "ON" : "OFF"),
            new SecondaryMetricModel("STROBE", telemetry?.StrobesOn == true ? "ON" : "OFF"),
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
            new ScoreRowModel("Preflight", "5/5", 112, new SolidColorBrush(MediaColor.FromRgb(54, 130, 139))),
            new ScoreRowModel("Taxi Out", "8/8", 112, new SolidColorBrush(MediaColor.FromRgb(54, 130, 139))),
            new ScoreRowModel("Approach", "10/12", 96, new SolidColorBrush(MediaColor.FromRgb(243, 169, 106))),
            new ScoreRowModel("Landing", "--", 0, new SolidColorBrush(MediaColor.FromRgb(217, 223, 214))),
        };

    private void PopulatePhasePills(FlightPhase currentPhase)
    {
        PhasePills.Clear();
        foreach (var phase in Enum.GetValues<FlightPhase>())
        {
            var isActive = phase == currentPhase;
            var isDone = !isActive && phase < currentPhase;
            PhasePills.Add(new PhasePillModel(ToPhaseLabel(phase), isActive, isDone));
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
            : $"IAS {telemetry.IndicatedAirspeedKnots:0} • VS {telemetry.VerticalSpeedFpm:0} • HDG {telemetry.HeadingTrueDegrees:0}",
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

    private static string BuildHeaderFlightText(FlightSessionRuntimeState? activeState)
    {
        if (activeState is null)
        {
            return "Waiting for flight context • MSFS telemetry will populate here";
        }

        var departure = string.IsNullOrWhiteSpace(activeState.Context.DepartureAirportIcao)
            ? "----"
            : activeState.Context.DepartureAirportIcao!.ToUpperInvariant();
        var arrival = string.IsNullOrWhiteSpace(activeState.Context.ArrivalAirportIcao)
            ? "----"
            : activeState.Context.ArrivalAirportIcao!.ToUpperInvariant();

        return $"{departure} to {arrival} • {BuildModeDisplay(activeState)} • {BuildProfileDisplay(activeState)}";
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

    private static string BuildAircraftTypeDisplay(FlightSessionRuntimeState? activeState)
    {
        if (activeState is null)
        {
            return "—";
        }

        var profile = activeState.Context.Profile;
        if (profile.HeavyFourEngineAircraft)
        {
            return "Heavy / 4-Engine";
        }

        return profile.EngineCount > 0 ? $"{profile.EngineCount}-Engine" : "—";
    }

    private static string FormatTime(DateTimeOffset? value, string fallback) =>
        value?.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture) ?? fallback;
}
