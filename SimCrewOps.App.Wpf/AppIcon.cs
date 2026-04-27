using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SimCrewOps.App.Wpf;

/// <summary>
/// Loads the SimCrewOps logo badge and exposes it as both a native GDI
/// <see cref="Icon"/> (system-tray <see cref="System.Windows.Forms.NotifyIcon"/>)
/// and a WPF <see cref="ImageSource"/> (window title bar / taskbar button).
///
/// The PNG is stored as a WPF Resource (<c>Resources\logo-badge.png</c>) and
/// addressed via a pack:// URI so the name never conflicts with the project's
/// root namespace or assembly name.
/// </summary>
internal static class AppIcon
{
    // pack:// URI — uses AssemblyName (SimTrackerV2), not RootNamespace.
    private static readonly Uri PackUri = new(
        "pack://application:,,,/SimTrackerV2;component/Resources/logo-badge.png");

    /// <summary>
    /// Creates a native GDI icon for <see cref="System.Windows.Forms.NotifyIcon.Icon"/>.
    /// Falls back to <see cref="SystemIcons.Application"/> if the resource cannot be loaded.
    /// </summary>
    public static Icon CreateNativeIcon(int size = 32)
    {
        try
        {
            var info = Application.GetResourceStream(PackUri)
                ?? throw new InvalidOperationException("Logo resource not found.");

            using var src = new Bitmap(info.Stream);
            using var dst = Resize(src, size);
            // GetHicon borrows an HICON that lives for the application lifetime.
            return Icon.FromHandle(dst.GetHicon());
        }
        catch
        {
            return SystemIcons.Application;
        }
    }

    /// <summary>
    /// Creates a frozen WPF <see cref="ImageSource"/> for
    /// <see cref="System.Windows.Window.Icon"/> (title bar + taskbar button).
    /// Returns <c>null</c> if the resource cannot be loaded (WPF uses its default icon).
    /// </summary>
    public static ImageSource? CreateWpfIcon()
    {
        try
        {
            var image = new BitmapImage(PackUri);
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Bitmap Resize(Bitmap src, int size)
    {
        var dst = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(dst);
        g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.PixelOffsetMode    = PixelOffsetMode.HighQuality;
        g.DrawImage(src, 0, 0, size, size);
        return dst;
    }
}
