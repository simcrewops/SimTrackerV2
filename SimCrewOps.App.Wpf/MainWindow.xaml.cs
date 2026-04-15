using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using SimCrewOps.App.Wpf.ViewModels;

namespace SimCrewOps.App.Wpf;

public partial class MainWindow : Window
{
    private bool _webViewInitialized;
    private bool _webViewFailed;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    // ── DataContext wiring ───────────────────────────────────────────────

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainWindowViewModel old)
            old.PropertyChanged -= ViewModel_PropertyChanged;

        if (e.NewValue is MainWindowViewModel vm)
            vm.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsLiveMapVisible)
            && sender is MainWindowViewModel vm
            && vm.IsLiveMapVisible)
        {
            EnsureLiveMapLoaded(vm);
        }
    }

    // ── WebView2 lazy initialisation ─────────────────────────────────────

    private void EnsureLiveMapLoaded(MainWindowViewModel vm)
    {
        // Already initialised (success or failure) — nothing to do.
        if (_webViewInitialized || _webViewFailed)
        {
            // If it succeeded before but the URI changed (e.g. user saved new API token),
            // navigate again.
            if (_webViewInitialized && vm.LiveMapUri is { } uri)
                _ = NavigateAsync(uri);

            return;
        }

        _ = InitWebViewAsync(vm);
    }

    private async Task InitWebViewAsync(MainWindowViewModel vm)
    {
        try
        {
            // This will throw WebView2RuntimeNotFoundException if the runtime is absent.
            await LiveMapWebView.EnsureCoreWebView2Async();
            _webViewInitialized = true;

            if (vm.LiveMapUri is { } uri)
                LiveMapWebView.Source = uri;
        }
        catch (Exception ex)
        {
            _webViewFailed = true;
            ShowWebViewFallback(ex);
        }
    }

    private async Task NavigateAsync(Uri uri)
    {
        try
        {
            await LiveMapWebView.EnsureCoreWebView2Async();
            LiveMapWebView.Source = uri;
        }
        catch
        {
            // Already in failure state — ignore.
        }
    }

    private void ShowWebViewFallback(Exception ex)
    {
        LiveMapWebView.Visibility = Visibility.Collapsed;
        LiveMapFallback.Visibility = Visibility.Visible;

        LiveMapFallbackDetail.Text = ex is WebView2RuntimeNotFoundException
            ? "The Microsoft Edge WebView2 Runtime is not installed on this machine.\n\n" +
              "Download it from microsoft.com/edge/webview2 then relaunch the tracker."
            : $"WebView2 failed to initialise: {ex.Message}";
    }
}
