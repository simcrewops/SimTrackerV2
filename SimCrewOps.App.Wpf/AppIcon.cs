using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SimCrewOps.App.Wpf;

/// <summary>
/// Loads the SimCrewOps logo badge (embedded PNG) and exposes it as both a
/// native GDI <see cref="Icon"/> (system-tray <see cref="System.Windows.Forms.NotifyIcon"/>)
/// and a WPF <see cref="ImageSource"/> (window title bar / taskbar button).
/// </summary>
internal static class AppIcon
{
    // Manifest resource name: <AssemblyName>.<FolderPath>.<FileName>
    private const string ResourceName =
        "SimTrackerV2.Resources.logo-badge.png";

    /// <summary>
    /// Creates a native GDI icon for <see cref="System.Windows.Forms.NotifyIcon.Icon"/>.
    /// Returns a fallback icon if the embedded resource cannot be loaded.
    /// </summary>
    public static Icon CreateNativeIcon(int size = 32)
    {
        try
        {
            using var bmp = LoadAndResize(size);
            // GetHicon borrows the HICON resource; the handle lives for the
            // application lifetime so we never explicitly destroy it.
            return Icon.FromHandle(bmp.GetHicon());
        }
        catch
        {
            return SystemIcons.Application;
        }
    }

    /// <summary>
    /// Creates a frozen WPF <see cref="ImageSource"/> for
    /// <see cref="System.Windows.Window.Icon"/> (title bar + taskbar button).
    /// Returns null if the embedded resource cannot be loaded.
    /// </summary>
    public static ImageSource? CreateWpfIcon(int size = 32)
    {
        try
        {
            using var bmp = LoadAndResize(size);
            var src = Imaging.CreateBitmapSourceFromHIcon(
                bmp.GetHicon(),
                System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            return src;
        }
        catch
        {
            return null;
        }
    }

    // ── Internals ────────────────────────────────────────────────────────────

    private static Bitmap LoadAndResize(int size)
    {
        var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{ResourceName}' not found.");

        using var src = new Bitmap(stream);

        // Resize to the requested pixel size with high-quality resampling so
        // the globe+plane detail holds up at 32×32 (tray) and looks sharp at
        // 256×256 (modern taskbar / high-DPI).
        var dst = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(dst);
        g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
        g.CompositingMode    = CompositingMode.SourceCopy;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.PixelOffsetMode    = PixelOffsetMode.HighQuality;
        g.DrawImage(src, 0, 0, size, size);
        return dst;
    }
}
