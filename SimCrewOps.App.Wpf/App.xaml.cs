using System.ComponentModel;
using System.IO;
using System.Windows;
using SimCrewOps.App.Wpf.Services;
using SimCrewOps.App.Wpf.ViewModels;
using WpfApplication = System.Windows.Application;

namespace SimCrewOps.App.Wpf;

public partial class App : WpfApplication
{
    private TrackerShellHost? _shellHost;
    private MainWindow? _mainWindow;
    private MainWindowViewModel? _mainWindowViewModel;
    private TrayIconService? _trayIconService;
    private bool _isExiting;

    protected override async void OnStartup(StartupEventArgs e)
    {
        // Catch anything that escapes — async void swallows exceptions otherwise.
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException     += OnUnobservedTaskException;

        try
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var bootstrap = await TrackerShellBootstrapper.BootstrapAsync();
            _shellHost = bootstrap.ShellHost;

            _mainWindowViewModel = new MainWindowViewModel(bootstrap);
            _mainWindow = new MainWindow
            {
                DataContext = _mainWindowViewModel,
                Icon = AppIcon.CreateWpfIcon(),
            };

            _trayIconService = new TrayIconService();
            _trayIconService.OpenRequested += TrayIconService_OpenRequested;
            _trayIconService.SyncRequested += TrayIconService_SyncRequested;
            _trayIconService.ExitRequested += TrayIconService_ExitRequested;

            _mainWindow.Closing += MainWindow_Closing;
            _mainWindowViewModel.PropertyChanged += MainWindowViewModel_PropertyChanged;

            _mainWindow.Show();
            await _mainWindowViewModel.InitializeAsync(_mainWindow);
            UpdateTrayTooltip();
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex);
            System.Windows.MessageBox.Show(
                $"SimCrewOps Tracker failed to start.\n\n{ex.GetType().Name}: {ex.Message}\n\nA crash log has been written to:\n{CrashLogPath}",
                "SimCrewOps Tracker — Startup Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_mainWindowViewModel is not null)
        {
            _mainWindowViewModel.PropertyChanged -= MainWindowViewModel_PropertyChanged;
        }

        if (_mainWindow is not null)
        {
            _mainWindow.Closing -= MainWindow_Closing;
        }

        _trayIconService?.Dispose();

        if (_shellHost is not null)
        {
            await _shellHost.DisposeAsync();
        }

        base.OnExit(e);
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_isExiting)
        {
            return;
        }

        e.Cancel = true;
        HideToTray();
    }

    private void TrayIconService_OpenRequested(object? sender, EventArgs e) => RestoreFromTray();

    private async void TrayIconService_SyncRequested(object? sender, EventArgs e)
    {
        if (_mainWindowViewModel is null)
        {
            return;
        }

        await _mainWindowViewModel.RetrySyncFromTrayAsync();
        UpdateTrayTooltip();
    }

    private void TrayIconService_ExitRequested(object? sender, EventArgs e)
    {
        _isExiting = true;
        if (_mainWindow is not null)
        {
            _mainWindow.ShowInTaskbar = true;
            _mainWindow.Close();
        }

        Shutdown();
    }

    private void MainWindowViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.PhaseTitle)
            or nameof(MainWindowViewModel.MsfsStatusText)
            or nameof(MainWindowViewModel.SyncStatusText))
        {
            UpdateTrayTooltip();
        }
    }

    private void HideToTray()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.ShowInTaskbar = false;
        _mainWindow.Hide();
        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }

        _trayIconService?.ShowBackgroundNotification();
    }

    private void RestoreFromTray()
    {
        if (_mainWindow is null)
        {
            return;
        }

        if (!_mainWindow.IsVisible)
        {
            _mainWindow.Show();
        }

        _mainWindow.ShowInTaskbar = true;
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void UpdateTrayTooltip()
    {
        if (_trayIconService is null || _mainWindowViewModel is null)
        {
            return;
        }

        _trayIconService.UpdateTooltip(_mainWindowViewModel.BuildTrayTooltip());
    }

    // ── Crash logging ────────────────────────────────────────────────────

    private static string CrashLogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SimCrewOps", "SimTrackerV2", "crash.log");

    private static void WriteCrashLog(Exception ex)
    {
        try
        {
            var dir = Path.GetDirectoryName(CrashLogPath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(CrashLogPath,
                $"[{DateTime.UtcNow:u}] SimTrackerV2 crash\n" +
                $"{ex}\n");
        }
        catch
        {
            // If we can't write the log, don't compound the problem.
        }
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            WriteCrashLog(ex);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WriteCrashLog(e.Exception);
        e.SetObserved();
    }
}
