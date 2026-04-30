using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SimCrewOps.App.Wpf;

internal static class AppIcon
{
    private static readonly Uri PackUri = new(
        "pack://application:,,,/SimTrackerV2;component/Resources/logo-badge.png");

    public static Icon CreateNativeIcon(int size = 32)
    {
        try
        {
            var info = System.Windows.Application.GetResourceStream(PackUri)
                ?? throw new InvalidOperationException("Logo resource not found.");

            using var source = new Bitmap(info.Stream);
            using var resized = Resize(source, size);
            return Icon.FromHandle(resized.GetHicon());
        }
        catch
        {
            return SystemIcons.Application;
        }
    }

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

    private static Bitmap Resize(Bitmap source, int size)
    {
        var destination = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(destination);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.DrawImage(source, 0, 0, size, size);
        return destination;
    }
}
