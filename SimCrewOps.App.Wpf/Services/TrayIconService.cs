using System.Drawing;
using WinForms = System.Windows.Forms;

namespace SimCrewOps.App.Wpf.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly WinForms.ContextMenuStrip _contextMenu;
    private readonly WinForms.NotifyIcon _notifyIcon;
    private bool _backgroundTipShown;
    private bool _disposed;

    public TrayIconService()
    {
        _contextMenu = new WinForms.ContextMenuStrip();

        var openItem = new WinForms.ToolStripMenuItem("Open Tracker");
        openItem.Click += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);

        var syncItem = new WinForms.ToolStripMenuItem("Retry Sync");
        syncItem.Click += (_, _) => SyncRequested?.Invoke(this, EventArgs.Empty);

        var exitItem = new WinForms.ToolStripMenuItem("Quit");
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        _contextMenu.Items.Add(openItem);
        _contextMenu.Items.Add(syncItem);
        _contextMenu.Items.Add(new WinForms.ToolStripSeparator());
        _contextMenu.Items.Add(exitItem);

        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = _contextMenu,
            Text = "SimCrewOps Tracker",
        };
        _notifyIcon.DoubleClick += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? OpenRequested;
    public event EventHandler? SyncRequested;
    public event EventHandler? ExitRequested;

    public void UpdateTooltip(string tooltip)
    {
        if (_disposed)
        {
            return;
        }

        _notifyIcon.Text = NormalizeTooltip(tooltip);
    }

    public void ShowBackgroundNotification()
    {
        if (_disposed || _backgroundTipShown)
        {
            return;
        }

        _notifyIcon.BalloonTipTitle = "SimCrewOps Tracker";
        _notifyIcon.BalloonTipText = "Tracker is still running in the system tray.";
        _notifyIcon.ShowBalloonTip(2500);
        _backgroundTipShown = true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();
    }

    private static string NormalizeTooltip(string tooltip)
    {
        var normalized = string.IsNullOrWhiteSpace(tooltip)
            ? "SimCrewOps Tracker"
            : tooltip.Replace('\r', ' ').Replace('\n', ' ').Trim();

        return normalized.Length <= 63
            ? normalized
            : normalized[..63];
    }
}
