using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
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
    /// <summary>
    /// Assembly InformationalVersion baked in at build time (e.g. "2.0.0-alpha.42+abc1234").
    /// Falls back to AssemblyVersion if the attribute is absent (local/dev builds).
    /// </summary>
    public static string AppVersion { get; } = ResolveAppVersion();

    private static string ResolveAppVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(MainWindowViewModel).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            // Strip the +commitsha suffix baked in by the CI (keep e.g. "2.1.0-beta.134")
            var plusIdx = informational.IndexOf('+');
            return plusIdx >= 0 ? informational[..plusIdx] : informational;
        }
        return assembly.GetName().Version?.ToString(3) ?? "2.0.0-dev";
    }

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
    private string _assignedFlightText = string.Empty;
    private string _scheduledBlockText = string.Empty;
    private string _phaseTitle = "PREFLIGHT";
    private string _phaseSubtitle = "The live ops board stays blank until the tracker receives real telemetry.";
    private string _phaseStatusLine = "Waiting for telemetry";
    private string _careerTier = "Standard Profile";
    private string _bidDisplay = "Free Flight";
    private string _reputationDisplay = "Free Flight";
    private string _outTime = "--:--z";
    private string _offTime = "--:--z";
    private string _onTime = "--:--z";
    private string _inTime = "--:--z";
    private string _blockTime = "--:--";
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
    private string _reviewHeaderRoute = "POST-FLIGHT DEBRIEF";
    private string _reviewLandingMetrics = "VS --   G --   Bank --   Pitch --   TZ Excess --";

    // ── Landing card ─────────────────────────────────────────────────────
    private string _gradeLetter = "--";
    private Brush _gradeBrush = new SolidColorBrush(MediaColor.FromRgb(56, 91, 105));
    private string _landingFpm = "--";
    private Brush _landingFpmBrush = new SolidColorBrush(MediaColor.FromRgb(125, 133, 144));
    private string _landingGForce = "--";
    private Brush _landingGForceBrush = new SolidColorBrush(MediaColor.FromRgb(125, 133, 144));
    private string _landingIas = "--";
    private string _landingBounces = "--";
    private Brush _landingBouncesBrush = new SolidColorBrush(MediaColor.FromRgb(125, 133, 144));
    private string _landingBankAngle = "--";
    private string _landingPitchAngle = "--";
    private string _landingTzExcess = "--";
    private bool _autoFailBannerVisible;
    private Brush _runwayMarkerBrush = new SolidColorBrush(MediaColor.FromRgb(63, 185, 80));
    private Thickness _runwayMarkerMargin = new Thickness(40, 2, 0, 2);
    private double _runwayTdzWidth = 60.0;
    private double _runwayMarkerLeft = 40.0;
    private string _diagnosticsSimState = "Waiting for simulator process";
    private string _diagnosticsActiveFlight = "No flight assigned";
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
        StatusChips = new ObservableCollection<SecondaryMetricModel>();
        PrimaryScoreRows = new ObservableCollection<ScoreRowModel>();
        SecondaryScoreRows = new ObservableCollection<ScoreRowModel>();
        ReviewScoreRows = new ObservableCollection<ScoreRowModel>();
        PhasePills = new ObservableCollection<PhasePillModel>();
        PlaneMarkers = new ObservableCollection<PlaneMarkerModel>();
        AcarsMessages = new ObservableCollection<AcarsMessageModel>();

        ShowDashboardCommand = new RelayCommand(() => SelectedPage = NavPage.Dashboard);
        ShowAcarsCommand     = new RelayCommand(() => SelectedPage = NavPage.Acars);
        ShowReviewCommand = new RelayCommand(() => SelectedPage = NavPage.Review);
        ShowDiagnosticsCommand = new RelayCommand(() => SelectedPage = NavPage.Diagnostics);
        ShowSettingsCommand = new RelayCommand(() => SelectedPage = NavPage.Settings);

        RetrySyncCommand        = new RelayCommand(() => _ = RetrySyncAsync());
        RefreshFlightCommand    = new RelayCommand(() => _ = RefreshFlightAsync());
        SaveSettingsCommand     = new RelayCommand(() => _ = SaveSettingsAsync());

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
    public RelayCommand ShowAcarsCommand     { get; }
    public RelayCommand ShowReviewCommand { get; }
    public RelayCommand ShowDiagnosticsCommand { get; }
    public RelayCommand ShowSettingsCommand { get; }

    /// <summary>Live-updated collection of aircraft markers for the map canvas.</summary>
    public ObservableCollection<PlaneMarkerModel> PlaneMarkers { get; }

    /// <summary>ACARS / Comms message thread (inbound dispatch + outbound pilot messages).</summary>
    public ObservableCollection<AcarsMessageModel> AcarsMessages { get; }

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
    public RelayCommand RefreshFlightCommand { get; }
    public RelayCommand SaveSettingsCommand { get; }
    public RelayCommand OpenAccountSettingsCommand { get; } = new RelayCommand(() =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://simcrewops.com/settings",
            UseShellExecute = true,
        }));

    public RelayCommand OpenLogbookCommand { get; } = new RelayCommand(() =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://www.simcrewops.com/my-flights",
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

    /// <summary>
    /// Always shows the web-fetched assignment (departure → arrival, flight number, aircraft).
    /// Empty string when no flight is assigned. Visible independent of live session state.
    /// </summary>
    public string AssignedFlightText
    {
        get => _assignedFlightText;
        private set => SetProperty(ref _assignedFlightText, value);
    }

    /// <summary>
    /// Scheduled block time from the active assignment, e.g. "Scheduled 2.5h block".
    /// Empty when no assignment or no block time is known.
    /// </summary>
    public string ScheduledBlockText
    {
        get => _scheduledBlockText;
        private set => SetProperty(ref _scheduledBlockText, value);
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

    public string BlockTime
    {
        get => _blockTime;
        private set => SetProperty(ref _blockTime, value);
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

    public string ReviewHeaderRoute
    {
        get => _reviewHeaderRoute;
        private set => SetProperty(ref _reviewHeaderRoute, value);
    }

    public string ReviewLandingMetrics
    {
        get => _reviewLandingMetrics;
        private set => SetProperty(ref _reviewLandingMetrics, value);
    }

    // ── Landing card properties ───────────────────────────────────────────
    public string GradeLetter
    {
        get => _gradeLetter;
        private set => SetProperty(ref _gradeLetter, value);
    }

    public Brush GradeBrush
    {
        get => _gradeBrush;
        private set => SetProperty(ref _gradeBrush, value);
    }

    public string LandingFpm
    {
        get => _landingFpm;
        private set => SetProperty(ref _landingFpm, value);
    }

    public Brush LandingFpmBrush
    {
        get => _landingFpmBrush;
        private set => SetProperty(ref _landingFpmBrush, value);
    }

    public string LandingGForce
    {
        get => _landingGForce;
        private set => SetProperty(ref _landingGForce, value);
    }

    public Brush LandingGForceBrush
    {
        get => _landingGForceBrush;
        private set => SetProperty(ref _landingGForceBrush, value);
    }

    public string LandingIas
    {
        get => _landingIas;
        private set => SetProperty(ref _landingIas, value);
    }

    public string LandingBounces
    {
        get => _landingBounces;
        private set => SetProperty(ref _landingBounces, value);
    }

    public Brush LandingBouncesBrush
    {
        get => _landingBouncesBrush;
        private set => SetProperty(ref _landingBouncesBrush, value);
    }

    public string LandingBankAngle
    {
        get => _landingBankAngle;
        private set => SetProperty(ref _landingBankAngle, value);
    }

    public string LandingPitchAngle
    {
        get => _landingPitchAngle;
        private set => SetProperty(ref _landingPitchAngle, value);
    }

    public string LandingTzExcess
    {
        get => _landingTzExcess;
        private set => SetProperty(ref _landingTzExcess, value);
    }

    public bool AutoFailBannerVisible
    {
        get => _autoFailBannerVisible;
        private set => SetProperty(ref _autoFailBannerVisible, value);
    }

    public Brush RunwayMarkerBrush
    {
        get => _runwayMarkerBrush;
        private set => SetProperty(ref _runwayMarkerBrush, value);
    }

    public Thickness RunwayMarkerMargin
    {
        get => _runwayMarkerMargin;
        private set => SetProperty(ref _runwayMarkerMargin, value);
    }

    public double RunwayTdzWidth
    {
        get => _runwayTdzWidth;
        private set => SetProperty(ref _runwayTdzWidth, value);
    }

    /// <summary>Pixel left offset (0-237) for the touchdown marker on the 240-px runway canvas.</summary>
    public double RunwayMarkerLeft
    {
        get => _runwayMarkerLeft;
        private set => SetProperty(ref _runwayMarkerLeft, value);
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

    public string DiagnosticsActiveFlight
    {
        get => _diagnosticsActiveFlight;
        private set => SetProperty(ref _diagnosticsActiveFlight, value);
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
            RaisePropertyChanged(nameof(IsAcarsVisible));
            RaisePropertyChanged(nameof(IsReviewVisible));
            RaisePropertyChanged(nameof(IsDiagnosticsVisible));
            RaisePropertyChanged(nameof(IsSettingsVisible));
            RaisePropertyChanged(nameof(DashboardNavUnderlineVisibility));
            RaisePropertyChanged(nameof(AcarsNavUnderlineVisibility));
            RaisePropertyChanged(nameof(ReviewNavUnderlineVisibility));
            RaisePropertyChanged(nameof(DiagnosticsNavUnderlineVisibility));
            RaisePropertyChanged(nameof(SettingsNavUnderlineVisibility));

        }
    }

    public bool IsDashboardVisible => SelectedPage == NavPage.Dashboard;
    public bool IsAcarsVisible     => SelectedPage == NavPage.Acars;
    public bool IsReviewVisible => SelectedPage == NavPage.Review;
    public bool IsDiagnosticsVisible => SelectedPage == NavPage.Diagnostics;
    public bool IsSettingsVisible => SelectedPage == NavPage.Settings;

    public Visibility DashboardNavUnderlineVisibility => IsDashboardVisible ? Visibility.Visible : Visibility.Hidden;
    public Visibility AcarsNavUnderlineVisibility     => IsAcarsVisible     ? Visibility.Visible : Visibility.Hidden;
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
        SyncStatusText = "SYNCING…";
        SyncStatusBrush = new SolidColorBrush(MediaColor.FromRgb(56, 91, 105));
        try
        {
            await _shellHost.SyncNowAsync();
        }
        catch (Exception ex)
        {
            SyncStatusText  = "SYNC ERROR";
            SyncStatusBrush = new SolidColorBrush(MediaColor.FromRgb(212, 98, 90));
            DiagnosticsSyncState = $"Retry failed: {ex.Message}";
            return;
        }

        if (_latestSnapshot is not null)
        {
            ApplySnapshot(await _shellHost.PollAsync());
        }
    }

    private async Task RefreshFlightAsync()
    {
        try
        {
            await _shellHost.ForceRefreshActiveFlightAsync();
            if (_latestSnapshot is not null)
            {
                ApplySnapshot(await _shellHost.PollAsync());
            }
        }
        catch
        {
            // Swallow — refresh is best-effort, UI will show whatever is available.
        }
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            var updatedSettings = SettingsEditor.ToSettings();
            var hasToken = !string.IsNullOrWhiteSpace(updatedSettings.Api.PilotApiToken);

            if (!hasToken)
            {
                await _shellHost.SaveSettingsAsync(updatedSettings);
                SettingsSaveStatus = "Settings saved. Enter your Pilot API Token to enable live position sync.";
                DiagnosticsStoragePath = updatedSettings.Storage.RootDirectory;
                return;
            }

            SettingsSaveStatus = "Saving and verifying connection…";
            await _shellHost.SaveSettingsAsync(updatedSettings);
            DiagnosticsStoragePath = updatedSettings.Storage.RootDirectory;

            // Immediately refresh the UI from the snapshot that SaveSettingsAsync just built
            // (includes ActiveFlight result from the fresh fetch).
            var snapshot = _shellHost.GetSnapshot();
            ApplySnapshot(snapshot);

            if (snapshot.ActiveFlight is not null)
            {
                var dep = snapshot.ActiveFlight.Departure ?? "----";
                var arr = snapshot.ActiveFlight.Arrival ?? "----";
                SettingsSaveStatus = $"Connected ✓  Next flight: {dep} → {arr}";
            }
            else
            {
                SettingsSaveStatus = "Connected ✓  No scheduled flight found — check My Flights on www.simcrewops.com/my-flights.";
            }
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
            SimConnectConnectionState.Connected    => new SolidColorBrush(MediaColor.FromRgb(52, 211, 153)),  // bright green
            SimConnectConnectionState.Connecting   => new SolidColorBrush(MediaColor.FromRgb(251, 191, 36)),  // amber
            SimConnectConnectionState.Faulted      => new SolidColorBrush(MediaColor.FromRgb(248, 113, 113)), // bright red
            _                                      => new SolidColorBrush(MediaColor.FromRgb(148, 163, 184)), // light grey
        };

        var syncEnabled = snapshot.BackgroundSyncStatus?.Enabled == true;
        var syncError   = snapshot.BackgroundSyncStatus?.LastErrorMessage;
        SyncStatusText = syncEnabled
            ? (syncError is null ? "SYNCED" : "SYNC RETRY")
            : "SYNC READY";
        SyncStatusBrush = syncEnabled && syncError is null
            ? new SolidColorBrush(MediaColor.FromRgb(52, 211, 153))   // bright green — synced
            : syncError is not null
                ? new SolidColorBrush(MediaColor.FromRgb(248, 113, 113)) // bright red — error
                : new SolidColorBrush(MediaColor.FromRgb(148, 163, 184)); // light grey — idle

        // Show actual last-upload time rather than just "token is configured".
        // Stale = no successful upload in the last 30 seconds while token is set.
        var lastUpload = snapshot.LivePositionLastUploadUtc;
        var sinceLast  = lastUpload is null ? (TimeSpan?)null : DateTimeOffset.UtcNow - lastUpload.Value;
        var uploadStale = snapshot.LivePositionEnabled && lastUpload is not null && sinceLast > TimeSpan.FromSeconds(30);

        LiveSyncStatusText = lastUpload is null
            ? (snapshot.LivePositionEnabled ? "LIVE SYNC READY" : "NOT CONNECTED")
            : $"LIVE SYNC  {(int)sinceLast!.Value.TotalSeconds}s ago";
        LiveSyncStatusBrush = uploadStale
            ? new SolidColorBrush(MediaColor.FromRgb(212, 98, 90))   // red — uploads stalled
            : lastUpload is not null
                ? new SolidColorBrush(MediaColor.FromRgb(44, 122, 125))  // teal — actively uploading
                : new SolidColorBrush(MediaColor.FromRgb(90, 106, 112)); // grey — no upload yet

        PhaseTitle = phase.ToString().ToUpperInvariant();
        HeaderFlightText = BuildHeaderFlightText(activeState, snapshot.ActiveFlight);
        AssignedFlightText = BuildAssignedFlightText(snapshot.ActiveFlight);
        ScheduledBlockText = snapshot.ActiveFlight?.ScheduledBlockHours is { } h
            ? $"Scheduled {h:0.0}h block"
            : string.Empty;
        PhaseSubtitle = BuildPhaseSubtitle(phase);
        PhaseStatusLine = BuildPhaseStatusLine(telemetry, phase);

        CareerTier = BuildProfileDisplay(activeState);
        BidDisplay = BuildBidDisplay(activeState, snapshot.ActiveFlight);
        ReputationDisplay = BuildModeDisplay(activeState);

        DepartureIcao = activeState?.Context.DepartureAirportIcao?.ToUpperInvariant() ?? "----";
        DepartureName = string.Empty;
        ArrivalIcao = activeState?.Context.ArrivalAirportIcao?.ToUpperInvariant() ?? "----";
        ArrivalName = string.Empty;
        // Prefer the aircraft title detected from MSFS (what's actually loaded in the sim).
        // Fall back to the scheduled bid aircraft type if SimConnect hasn't reported one yet.
        AircraftType = snapshot.SimConnectStatus.DetectedAircraftTitle
            ?? activeState?.Context.AircraftType
            ?? "—";

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
        var postLanding = activeState?.CurrentPhase is FlightPhase.Landing
            or FlightPhase.TaxiIn or FlightPhase.Arrival;
        LandingRunway = runway is not null
            ? $"{runway.AirportIcao} {runway.Runway.RunwayIdentifier}"
            : postLanding ? "No arrival airport set" : string.Empty;
        TdzDistanceFt   = runway is not null ? $"{runway.Projection.DistanceFromThresholdFeet:0}" : "--";
        TdzCrossTrackFt = runway is not null ? $"{Math.Abs(runway.Projection.CrossTrackDistanceFeet):0}" : "--";
        TdzExcessFt     = runway is not null && runway.Projection.TouchdownZoneExcessDistanceFeet > 0
            ? $"{runway.Projection.TouchdownZoneExcessDistanceFeet:0}"
            : runway is not null ? "0" : "--";

        var score    = activeState?.ScoreResult.FinalScore    ?? 88;
        var maxScore = activeState?.ScoreResult.MaximumScore  ?? 120.0;
        // Normalize to a 0–100 percentage so the bar width is independent of the raw maximum.
        ScoreBarWidth = Math.Clamp(score / maxScore * 200.0, 0, 200);

        AlertType = phase switch
        {
            FlightPhase.Approach => "APPROACH CHECK",
            FlightPhase.Landing => "LANDING",
            FlightPhase.TaxiIn or FlightPhase.Arrival => "TAXI / ARRIVAL",
            _ => "PHASE MONITOR",
        };

        OutTime = FormatTimeUtc(activeState?.BlockTimes.BlocksOffUtc, "--:--z");
        OffTime = FormatTimeUtc(activeState?.BlockTimes.WheelsOffUtc, "--:--z");
        OnTime  = FormatTimeUtc(activeState?.BlockTimes.WheelsOnUtc,  "--:--z");
        InTime  = FormatTimeUtc(activeState?.BlockTimes.BlocksOnUtc,  "--:--z");
        BlockTime = FormatBlockElapsed(activeState?.BlockTimes.BlocksOffUtc, activeState?.BlockTimes.BlocksOnUtc);

        UpdatePersistentInstruments(telemetry);
        PopulatePhasePills(phase);
        PopulateMetricTiles(phase, telemetry, activeState);
        PopulateStatusChips(phase, telemetry);
        PopulateScoreRows(activeState?.ScoreResult);

        // Display score as a normalised 0–100 integer so the number is always in the familiar range
        // regardless of whether the session had runway data (max=120) or not (max=100).
        ScoreText = activeState?.ScoreResult is { } sr
            ? Math.Round(sr.FinalScore / sr.MaximumScore * 100.0).ToString("0", CultureInfo.InvariantCulture)
            : "--";
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

        // Top bar route string for the review panel header
        if (activeState is not null)
        {
            var dep = string.IsNullOrWhiteSpace(activeState.Context.DepartureAirportIcao) ? "----" : activeState.Context.DepartureAirportIcao.ToUpperInvariant();
            var arr = string.IsNullOrWhiteSpace(activeState.Context.ArrivalAirportIcao)   ? "----" : activeState.Context.ArrivalAirportIcao.ToUpperInvariant();
            ReviewHeaderRoute = $"POST-FLIGHT DEBRIEF  ·  {dep} → {arr}";
        }
        else if (snapshot.ActiveFlight is not null)
        {
            var dep = (snapshot.ActiveFlight.Departure ?? "----").ToUpperInvariant();
            var arr = (snapshot.ActiveFlight.Arrival   ?? "----").ToUpperInvariant();
            ReviewHeaderRoute = $"POST-FLIGHT DEBRIEF  ·  {dep} → {arr}";
        }
        else
        {
            ReviewHeaderRoute = "POST-FLIGHT DEBRIEF";
        }
        ReviewLandingMetrics = BuildLandingMetrics(activeState);
        ApplyLandingCard(activeState);

        var simAircraft = snapshot.SimConnectStatus.DetectedAircraftTitle is { Length: > 0 } t ? $" • {t}" : string.Empty;
        DiagnosticsSimState = $"{snapshot.SimConnectStatus.ConnectionState}{simAircraft} {(snapshot.SimConnectStatus.LastErrorMessage is { Length: > 0 } err ? $"• {err}" : string.Empty)}".Trim();
        DiagnosticsSyncState = snapshot.BackgroundSyncStatus is null
            ? "Background sync disabled"
            : $"{snapshot.BackgroundSyncStatus.LastTrigger ?? "idle"} • failures {snapshot.BackgroundSyncStatus.ConsecutiveFailureCount}";
        var pendingCount = snapshot.RecoverySnapshot.PendingCompletedSessions.Count;
        DiagnosticsRecoveryState = activeState is not null && snapshot.RecoverySnapshot.HasRecoverableCurrentSession
            ? $"Resumed session active • autosave {snapshot.RecoverySnapshot.CurrentSession!.SavedUtc.ToLocalTime():HH:mm:ss}{(pendingCount > 0 ? $" • {pendingCount} pending upload" : string.Empty)}"
            : snapshot.RecoverySnapshot.HasRecoverableCurrentSession
            ? $"Recoverable session saved {snapshot.RecoverySnapshot.CurrentSession!.SavedUtc.ToLocalTime():HH:mm:ss}{(pendingCount > 0 ? $" • {pendingCount} pending upload" : string.Empty)}"
            : pendingCount > 0 ? $"{pendingCount} session(s) pending upload"
            : "No recoverable session";
        DiagnosticsSettingsPath = snapshot.SettingsFilePath;
        DiagnosticsStoragePath = snapshot.Settings.Storage.RootDirectory;
        DiagnosticsActiveFlight = snapshot.ActiveFlight is { } af
            ? BuildAssignedFlightText(af)
            : "None assigned — departure/arrival airports will not auto-populate";

        DiagnosticsLastTelemetry = telemetry is null
            ? "No telemetry received yet"
            : $"{telemetry.Phase} • IAS {telemetry.IndicatedAirspeedKnots:0} kts • VS {telemetry.VerticalSpeedFpm:0} fpm • AGL {telemetry.AltitudeAglFeet:0} ft • HDG {telemetry.HeadingMagneticDegrees:0}° • {telemetry.Latitude:F4},{telemetry.Longitude:F4}";

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
        PopulateMetricTiles(FlightPhase.Preflight, null, null);
        PopulateStatusChips(FlightPhase.Preflight, null);
        PopulateScoreRows(null);
        PopulateSampleAcarsMessages();
    }

    private void PopulateSampleAcarsMessages()
    {
        AcarsMessages.Clear();
        AcarsMessages.Add(new AcarsMessageModel("DISPATCH", "Clearance delivered. Squawk 2341. Initial climb FL180, expect FL360 10 min after departure.", "22:31 UTC", IsOutbound: false));
        AcarsMessages.Add(new AcarsMessageModel("YOU", "CLRD FL360 SQUAWK 2341. WILCO.", "22:32 UTC", IsOutbound: true));
        AcarsMessages.Add(new AcarsMessageModel("DISPATCH", "Winds updated: 270/18G24 at destination. Expect ILS 28R arrival. ATIS Kilo.", "22:43 UTC", IsOutbound: false));
    }

    private void PopulateMetricTiles(FlightPhase phase, TelemetryFrame? telemetry, FlightSessionRuntimeState? activeState)
    {
        MetricTiles.Clear();

        foreach (var tile in BuildMetricTiles(phase, telemetry, activeState))
        {
            MetricTiles.Add(tile);
        }
    }

    private IReadOnlyList<MetricTileModel> BuildMetricTiles(FlightPhase phase, TelemetryFrame? telemetry, FlightSessionRuntimeState? activeState) => phase switch
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
            new MetricTileModel("G", telemetry is null || telemetry.OnGround ? "--" : $"{telemetry.GForce:0.00}"),
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
            new MetricTileModel("G", telemetry is null || telemetry.OnGround ? "--" : $"{telemetry.GForce:0.00}", telemetry is { OnGround: false, GForce: > 1.5 }),
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
        FlightPhase.Approach => new[]
        {
            new MetricTileModel("IAS", telemetry is null ? "-- kt" : $"{telemetry.IndicatedAirspeedKnots:0} kt"),
            new MetricTileModel("VS",  telemetry is null ? "-- fpm" : $"{telemetry.VerticalSpeedFpm:0} fpm", telemetry is { VerticalSpeedFpm: < -1000 }),
            new MetricTileModel("ALT AGL", telemetry is null ? "-- ft" : $"{telemetry.AltitudeAglFeet:0} ft"),
            // G/S: ILS glideslope deviation in CDI dots from NAV1 (positive = above, negative = below).
            // Alert when deviation exceeds 1 dot — well outside the stable approach envelope.
            new MetricTileModel("G/S",
                telemetry is null ? "-- dot" : $"{telemetry.Nav1GlideslopeErrorDots:+0.0;-0.0;0.0} dot",
                telemetry is not null && Math.Abs(telemetry.Nav1GlideslopeErrorDots) > 1.0),
            // DIST THR: haversine distance to the pre-resolved runway threshold.
            // Populated once an arrival airport and heading-matched runway are known.
            new MetricTileModel("DIST THR",
                activeState?.ApproachDistanceNm is { } dist ? $"{dist:0.0} nm" : "-- nm"),
            // LOC: ILS localizer deviation in CDI dots from NAV1 (positive = right, negative = left).
            new MetricTileModel("LOC",
                telemetry is null ? "-- dot" : $"{telemetry.Nav1LocalizerErrorDots:+0.0;-0.0;0.0} dot",
                telemetry is not null && Math.Abs(telemetry.Nav1LocalizerErrorDots) > 1.0),
            // G/PATH: geometric 3° glidepath deviation in feet (positive = above path, negative = below).
            // Independent of ILS — computed from AGL vs ideal altitude at this distance from threshold.
            // Alert when ±200 ft off the ideal 3° path.
            new MetricTileModel("G/PATH",
                activeState?.GlidepathDeviationFeet is { } gDev ? $"{gDev:+0;-0;0} ft" : "-- ft",
                activeState?.GlidepathDeviationFeet is { } gDevAlert && Math.Abs(gDevAlert) > 200),
        },
        _ => new[]
        {
            new MetricTileModel("IAS", telemetry is null ? "-- kt" : $"{telemetry.IndicatedAirspeedKnots:0} kt"),
            new MetricTileModel("VS",  telemetry is null ? "-- fpm" : $"{telemetry.VerticalSpeedFpm:0} fpm"),
            new MetricTileModel("ALT AGL", telemetry is null ? "-- ft" : $"{telemetry.AltitudeAglFeet:0} ft"),
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
            new SecondaryMetricModel("RUNWAY", "--"),
            new SecondaryMetricModel("GEAR", telemetry is null ? "--" : telemetry.GearDown ? "DOWN" : "UP"),
            new SecondaryMetricModel("FLAPS", telemetry is null ? "--" : $"{telemetry.FlapsHandleIndex}"),
            new SecondaryMetricModel("500 AGL", telemetry is null ? "--" : "OK"),
        },
        FlightPhase.Landing => new[]
        {
            new SecondaryMetricModel("STATE", telemetry is null ? "--" : "TOUCHDOWN"),
            new SecondaryMetricModel("GEAR", telemetry is null ? "--" : telemetry.GearDown ? "DOWN" : "UP"),
            new SecondaryMetricModel("ROLLOUT", telemetry is null ? "--" : "ACTIVE"),
        },
        FlightPhase.Arrival => new[]
        {
            new SecondaryMetricModel("PARK BRAKE", telemetry is null ? "--" : telemetry.ParkingBrakeSet ? "SET" : "OFF"),
            new SecondaryMetricModel("TAXI LT", telemetry is null ? "--" : telemetry.TaxiLightsOn ? "ON" : "OFF"),
            new SecondaryMetricModel("ENGINES", telemetry is null ? "--" : !telemetry.Engine1Running && !telemetry.Engine2Running ? "OFF" : "RUN"),
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
        var pct = phaseScore.MaxPoints <= 0
            ? 1.0
            : phaseScore.AwardedPoints / phaseScore.MaxPoints;

        // Colour-grade the bar: green ≥ 90 %, blue ≥ 70 %, amber ≥ 50 %, red < 50 %
        var fillBrush = pct >= 0.90
            ? new SolidColorBrush(MediaColor.FromRgb(63,  185, 80))   // green
            : pct >= 0.70
            ? new SolidColorBrush(MediaColor.FromRgb(88,  166, 255))  // blue
            : pct >= 0.50
            ? new SolidColorBrush(MediaColor.FromRgb(210, 153, 34))   // amber
            : new SolidColorBrush(MediaColor.FromRgb(248, 81,  73));  // red

        // Bar fills a 200 px track (used in both the phase-bar column and the review right panel)
        var fillWidth = phaseScore.MaxPoints <= 0 ? 0.0 : 200.0 * pct;

        // Build the full findings list for the review page.
        // Automatic-fail findings are shown as-is; point deductions include the amount.
        var findings = phaseScore.Findings
            .Where(f => f.PointsDeducted > 0 || f.IsAutomaticFail)
            .Select(f => new FindingRowModel(
                f.IsAutomaticFail
                    ? $"↳ {f.Description}"
                    : $"↳ {f.Description}  −{f.PointsDeducted:0.#} pts",
                f.IsAutomaticFail))
            .ToList();

        return new ScoreRowModel(
            phaseScore.Phase switch
            {
                FlightPhase.TaxiOut  => "Taxi Out",
                FlightPhase.TaxiIn   => "Taxi In",
                _                    => phaseScore.Phase.ToString(),
            },
            phaseScore.MaxPoints <= 0
                ? "--"
                : $"{phaseScore.AwardedPoints:0.#} / {phaseScore.MaxPoints:0.#}",
            fillWidth,
            fillBrush,
            findings);
    }

    private static List<ScoreRowModel> CreateSampleScoreRows()
    {
        var muted = new SolidColorBrush(MediaColor.FromRgb(56, 91, 105));
        return new List<ScoreRowModel>
        {
            new("Preflight", "--", 0, muted, []),
            new("Taxi Out",  "--", 0, muted, []),
            new("Takeoff",   "--", 0, muted, []),
            new("Approach",  "--", 0, muted, []),
            new("Landing",   "--", 0, muted, []),
            new("Taxi In",   "--", 0, muted, []),
            new("Arrival",   "--", 0, muted, []),
        };
    }

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

    /// <summary>
    /// Populates all landing card ViewModel properties from the current active state.
    /// Called on every ApplySnapshot cycle so the UI stays live during approach/landing.
    /// </summary>
    private void ApplyLandingCard(FlightSessionRuntimeState? activeState)
    {
        var scoreResult = activeState?.ScoreResult;
        var landing     = activeState?.ScoreInput.Landing;

        // ── Grade badge ──────────────────────────────────────────────────
        var grade = scoreResult?.Grade ?? "--";
        GradeLetter = grade;
        GradeBrush  = GradeToColor(grade, scoreResult?.AutomaticFail ?? false);
        AutoFailBannerVisible = scoreResult?.AutomaticFail == true;

        // ── Landing metrics ──────────────────────────────────────────────
        // Treat zero g-force as "no data yet" — MSFS always reports some g
        // after a real touchdown, so this only fires pre-landing.
        var hasData = landing is not null && landing.TouchdownGForce > 0.01;

        if (!hasData)
        {
            var muted = new SolidColorBrush(MediaColor.FromRgb(125, 133, 144));
            LandingFpm        = "--";
            LandingFpmBrush   = muted;
            LandingGForce     = "--";
            LandingGForceBrush = muted;
            LandingIas        = "--";
            LandingBounces    = "--";
            LandingBouncesBrush = muted;
            LandingBankAngle  = "--";
            LandingPitchAngle = "--";
            LandingTzExcess   = "--";
            RunwayMarkerBrush  = new SolidColorBrush(MediaColor.FromRgb(63, 185, 80));
            RunwayMarkerMargin = new Thickness(40, 2, 0, 2);
            RunwayMarkerLeft   = 40.0;
            RunwayTdzWidth     = 60.0;
            return;
        }

        // FPM — thresholds: ≥ -600 good, ≥ -800 firm/amber, < -800 hard/red
        var fpm = landing!.TouchdownVerticalSpeedFpm;
        LandingFpm      = $"{fpm:0}";
        LandingFpmBrush = fpm >= -600
            ? new SolidColorBrush(MediaColor.FromRgb(63, 185, 80))
            : fpm >= -800
            ? new SolidColorBrush(MediaColor.FromRgb(210, 153, 34))
            : new SolidColorBrush(MediaColor.FromRgb(248, 81, 73));

        // G-force — thresholds: ≤ 1.5 good, ≤ 2.0 firm, > 2.0 hard
        var g = landing.TouchdownGForce;
        LandingGForce      = $"{g:0.00}";
        LandingGForceBrush = g <= 1.5
            ? new SolidColorBrush(MediaColor.FromRgb(63, 185, 80))
            : g <= 2.0
            ? new SolidColorBrush(MediaColor.FromRgb(210, 153, 34))
            : new SolidColorBrush(MediaColor.FromRgb(248, 81, 73));

        LandingIas = $"{landing.TouchdownIndicatedAirspeedKnots:0}";

        // Bounces
        var bounces = landing.BounceCount;
        LandingBounces      = bounces.ToString(CultureInfo.InvariantCulture);
        LandingBouncesBrush = bounces == 0
            ? new SolidColorBrush(MediaColor.FromRgb(63, 185, 80))
            : bounces == 1
            ? new SolidColorBrush(MediaColor.FromRgb(210, 153, 34))
            : new SolidColorBrush(MediaColor.FromRgb(248, 81, 73));

        LandingBankAngle  = $"{landing.TouchdownBankAngleDegrees:0.#}°";
        LandingPitchAngle = $"{landing.TouchdownPitchAngleDegrees:0.#}°";

        // Runway TDZ diagram — canvas is 240 px wide; TDZ zone = first 60 px
        var tzExcess = landing.TouchdownZoneExcessDistanceFeet;
        RunwayTdzWidth = 60.0;
        if (tzExcess <= 0)
        {
            LandingTzExcess    = "IN ZONE";
            RunwayMarkerBrush  = new SolidColorBrush(MediaColor.FromRgb(63, 185, 80));
            RunwayMarkerMargin = new Thickness(40, 2, 0, 2);
            RunwayMarkerLeft   = 40.0;
        }
        else
        {
            LandingTzExcess    = $"+{tzExcess:0} ft past TDZ";
            RunwayMarkerBrush  = tzExcess > 500
                ? new SolidColorBrush(MediaColor.FromRgb(248, 81, 73))
                : new SolidColorBrush(MediaColor.FromRgb(210, 153, 34));
            // Each 25 ft of excess = 1 pixel beyond the 60-px TDZ block; max 178 px
            var markerLeft = Math.Min(178, 60.0 + tzExcess / 25.0);
            RunwayMarkerMargin = new Thickness(markerLeft, 2, 0, 2);
            RunwayMarkerLeft   = markerLeft;
        }
    }

    private static Brush GradeToColor(string grade, bool autoFail)
    {
        if (autoFail) return new SolidColorBrush(MediaColor.FromRgb(248, 81, 73));
        return grade switch
        {
            "A" => new SolidColorBrush(MediaColor.FromRgb(63, 185, 80)),
            "B" => new SolidColorBrush(MediaColor.FromRgb(88, 166, 255)),
            "C" => new SolidColorBrush(MediaColor.FromRgb(210, 153, 34)),
            "D" => new SolidColorBrush(MediaColor.FromRgb(227, 135, 75)),
            "F" => new SolidColorBrush(MediaColor.FromRgb(248, 81, 73)),
            _   => new SolidColorBrush(MediaColor.FromRgb(56, 91, 105)),
        };
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

    /// <summary>
    /// Always returns the web-fetched assignment as a single line, e.g.
    /// "KDFW → KMIA • AA123 • B738  (career)"
    /// Empty string when nothing is fetched — hides the row via Visibility binding.
    /// </summary>
    private static string BuildAssignedFlightText(ActiveFlightResponse? activeFlight)
    {
        if (activeFlight is null) return string.Empty;
        var airline = string.IsNullOrWhiteSpace(activeFlight.Airline) ? "" : $"{activeFlight.Airline} ";
        var dep = (activeFlight.Departure ?? "----").ToUpperInvariant();
        var arr = (activeFlight.Arrival ?? "----").ToUpperInvariant();
        var fn  = string.IsNullOrWhiteSpace(activeFlight.FlightNumber) ? "" : $" • {activeFlight.FlightNumber.ToUpperInvariant()}";
        var ac  = string.IsNullOrWhiteSpace(activeFlight.AircraftType)  ? "" : $" • {activeFlight.AircraftType}";
        var src = activeFlight.Source switch
        {
            "bid_packet"   => "bid",
            "open_time"    => "open time",
            "roster"       => "schedule",
            "dispatch"     => "dispatch",
            "premium_time" => "premium",
            _              => "booked",
        };
        return $"{airline}{dep} → {arr}{fn}{ac}  ({src})";
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

    // Legacy local-time formatter — kept for non-block-time uses.
    private static string FormatTime(DateTimeOffset? value, string fallback) =>
        value?.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture) ?? fallback;

    // Block times are shown as Zulu/UTC (HH:mmz) to match real-world OOOI reporting.
    // The sim's Zulu clock matches real-world UTC in real-time mode.
    private static string FormatTimeUtc(DateTimeOffset? value, string fallback) =>
        value is null ? fallback : value.Value.UtcDateTime.ToString("HH:mm", CultureInfo.InvariantCulture) + "z";

    // Block elapsed time: formatted as H:MM (e.g. "5:23" or "0:47").
    private static string FormatBlockElapsed(DateTimeOffset? blocksOff, DateTimeOffset? blocksOn)
    {
        if (blocksOff is null) return "--:--";
        var elapsed = (blocksOn ?? DateTimeOffset.UtcNow) - blocksOff.Value;
        if (elapsed < TimeSpan.Zero) return "--:--";
        return $"{(int)elapsed.TotalHours}:{elapsed.Minutes:D2}";
    }

    private static string BuildSyncDiagnosticLine(SimCrewOps.Hosting.Models.BackgroundSyncStatus? sync)
    {
        if (sync is null) return "Background sync disabled";
        if (sync.IsRunning) return "Running…";

        var parts = new System.Collections.Generic.List<string>();

        parts.Add(sync.LastRunCompletedUtc is { } completed
            ? $"Last run {completed.ToLocalTime():HH:mm:ss}"
            : "Never run");

        if (sync.LastSummary is { SucceededCount: > 0 } s)
            parts.Add($"{s.SucceededCount} uploaded");

        if (sync.ConsecutiveFailureCount > 0)
            parts.Add($"{sync.ConsecutiveFailureCount} consecutive failure(s)");

        if (sync.LastErrorMessage is { Length: > 0 } err)
            parts.Add($"Error: {err}");

        return string.Join(" • ", parts);
    }

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
            $"HDG MAG {rawFrame.HeadingMagneticDegrees:0.##} • HDG TRUE {rawFrame.HeadingTrueDegrees:0.##} • AGL {rawFrame.AltitudeAglFeet:0.##} • ALT {rawFrame.AltitudeFeet:0.##} • ON GND {rawFrame.OnGround:0} • PB pos={rawFrame.ParkingBrakePosition:0}%\n" +
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
        // Suppress G-force while on the ground — MSFS always reports ~1 G due to gravity,
        // which is physically correct but misleading on the instruments panel.
        LiveGForce      = telemetry.OnGround ? "--" : $"{telemetry.GForce:0.00}";
        LiveGForceAlert = !telemetry.OnGround && (telemetry.GForce > 2.0 || telemetry.GForce < 0.5);
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
