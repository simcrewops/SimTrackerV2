using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SimCrewOps.App.Wpf;

/// <summary>
/// Generates the SimCrewOps Tracker icon programmatically.
/// Used for the system-tray <see cref="System.Windows.Forms.NotifyIcon"/> and the
/// WPF window's title bar / taskbar button (<see cref="System.Windows.Window.Icon"/>).
/// </summary>
internal static class AppIcon
{
    /// <summary>
    /// Creates a native GDI icon for use with
    /// <see cref="System.Windows.Forms.NotifyIcon.Icon"/>.
    /// </summary>
    public static Icon CreateNativeIcon(int size = 32)
    {
        using var bmp = Render(size);
        // GetHicon creates a Win32 HICON resource; FromHandle borrows it.
        // The icon lives for the application lifetime, so the handle is never
        // explicitly destroyed (the OS reclaims it on process exit).
        return Icon.FromHandle(bmp.GetHicon());
    }

    /// <summary>
    /// Creates a frozen WPF <see cref="ImageSource"/> for
    /// <see cref="System.Windows.Window.Icon"/> (title bar + taskbar button).
    /// </summary>
    public static ImageSource CreateWpfIcon(int size = 32)
    {
        using var bmp = Render(size);
        var src = Imaging.CreateBitmapSourceFromHIcon(
            bmp.GetHicon(),
            System.Windows.Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());
        src.Freeze();
        return src;
    }

    // ── Renderer ────────────────────────────────────────────────────────────────

    private static Bitmap Render(int size)
    {
        var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode      = SmoothingMode.AntiAlias;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.Clear(Color.Transparent);

        float s = size / 32f; // scale all coordinates from the 32×32 master grid

        // ── Background — rounded square, deep navy ───────────────────────────
        using (var bg = new SolidBrush(Color.FromArgb(255, 18, 36, 64)))
            FillRoundedRect(g, bg, 0, 0, size, size, 6 * s);

        // ── Airplane silhouette — top-down view, nose pointing upward ────────
        //    Designed on a 32×32 grid; scaled to the requested size.
        //
        //                    nose (16,2)
        //               ╱────────────────╲
        //   wing (2,17)◄                  ►wing (30,17)
        //               ╲   (12,20)  (20,20)  ╱
        //                │                │
        //          (10,30)▼  (14,26)(18,26) ▼(22,30)
        //                      ▲      ▲
        //                  tail fins + centre (16,28)
        //
        var planePoints = Scale(s, new PointF[]
        {
            new(16,  2),   // nose
            new(30, 17),   // right wing tip
            new(20, 20),   // right wing-body rear junction
            new(22, 30),   // right tail fin tip
            new(18, 26),   // right tail fin root
            new(16, 28),   // tail centre
            new(14, 26),   // left tail fin root
            new(10, 30),   // left tail fin tip
            new(12, 20),   // left wing-body rear junction
            new( 2, 17),   // left wing tip
        });

        using (var plane = new SolidBrush(Color.FromArgb(255, 210, 228, 255)))
            g.FillPolygon(plane, planePoints);

        return bmp;
    }

    private static PointF[] Scale(float s, PointF[] pts)
    {
        var scaled = new PointF[pts.Length];
        for (var i = 0; i < pts.Length; i++)
            scaled[i] = new PointF(pts[i].X * s, pts[i].Y * s);
        return scaled;
    }

    private static void FillRoundedRect(
        Graphics g, Brush brush,
        float x, float y, float w, float h, float r)
    {
        using var path = new GraphicsPath();
        path.AddArc(x,             y,             r * 2, r * 2, 180, 90); // top-left
        path.AddArc(x + w - r * 2, y,             r * 2, r * 2, 270, 90); // top-right
        path.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2,   0, 90); // bottom-right
        path.AddArc(x,             y + h - r * 2, r * 2, r * 2,  90, 90); // bottom-left
        path.CloseFigure();
        g.FillPath(brush, path);
    }
}
