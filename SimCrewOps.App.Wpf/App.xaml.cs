using System.Windows;
using SimCrewOps.App.Wpf.Services;
using SimCrewOps.App.Wpf.ViewModels;

namespace SimCrewOps.App.Wpf;

public partial class App : Application
{
    private TrackerShellHost? _shellHost;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var bootstrap = await TrackerShellBootstrapper.BootstrapAsync();
        _shellHost = bootstrap.ShellHost;

        var mainWindowViewModel = new MainWindowViewModel(bootstrap);
        var mainWindow = new MainWindow
        {
            DataContext = mainWindowViewModel,
        };

        mainWindow.Show();
        await mainWindowViewModel.InitializeAsync(mainWindow);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_shellHost is not null)
        {
            await _shellHost.DisposeAsync();
        }

        base.OnExit(e);
    }
}
