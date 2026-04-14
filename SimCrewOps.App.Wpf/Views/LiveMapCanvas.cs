using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using SimCrewOps.App.Wpf.Infrastructure;
using SimCrewOps.Hosting.Models;

// Disambiguate WPF types from System.Drawing (pulled in by UseWindowsForms)
using Brush = System.Windows.Media.Brush;
using Pen   = System.Windows.Media.Pen;
using Color = System.Windows.Media.Color;

namespace SimCrewOps.App.Wpf.Views;

/// <summary>
/// A custom FrameworkElement that renders the live-map fleet overlay.
///
/// Binds to <see cref="LiveFlights"/> (an IReadOnlyList&lt;LiveFlight&gt;).
/// Whenever the property changes, the element invalidates its visual and
/// redraws all aircraft icons onto the canvas using equirectangular projection.
/// </summary>
public sealed class LiveMapCanvas : FrameworkElement
{
    // ── Dependency property ──────────────────────────────────────────────────

    public static readonly DependencyProperty LiveFlightsProperty =
        DependencyProperty.Register(
            nameof(LiveFlights),
            typeof(IReadOnlyList<LiveFlight>),
            typeof(LiveMapCanvas),
            new FrameworkPropertyMetadata(
                defaultValue: null,
                flags: FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<LiveFlight>? LiveFlights
    {
        get => (IReadOnlyList<LiveFlight>?)GetValue(LiveFlightsProperty);
        set => SetValue(LiveFlightsProperty, value);
    }

    // ── Brushes and pens ─────────────────────────────────────────────────────

    // Grid lines
    private static readonly Pen GridPen = new(new SolidColorBrush(Color.FromArgb(30, 90, 160, 200)), 0.5);

    // "My flight" icon fill
    private static readonly Brush MyFlightBrush  = new SolidColorBrush(Color.FromRgb(0xFF, 0x75, 0x1F)); // #ff751f orange
    private static readonly Brush OtherBrush      = new SolidColorBrush(Color.FromRgb(0x5C, 0xA4, 0xC0)); // steel blue
    private static readonly Brush LabelBrush      = new SolidColorBrush(Color.FromRgb(0xCC, 0xDD, 0xE6));
    private static readonly Brush LabelBgBrush    = new SolidColorBrush(Color.FromArgb(180, 11, 17, 26));
    private static readonly Pen   LabelBorderPen  = new(new SolidColorBrush(Color.FromArgb(60, 92, 164, 192)), 1);

    private static readonly Typeface LabelTypeface = new("Segoe UI");
    private static readonly double   LabelFontSize = 11.0;

    // ── Plane geometry (SVG path scaled to a 24×24 icon) ─────────────────────

    // The original SVG uses viewBox 577.125 545 344.875 410.
    // We scale that down to a 20px icon centered at (0,0) so we can
    // apply a TranslateTransform + RotateTransform at draw time.
    private static readonly Geometry PlaneGeometry = BuildPlaneGeometry();

    private static Geometry BuildPlaneGeometry()
    {
        // Target icon: 20 wide, 24 tall; origin at center.
        const double srcX = 577.125, srcY = 545.0, srcW = 344.875, srcH = 410.0;
        const double iconW = 20.0, iconH = 24.0;

        double sx = iconW / srcW;
        double sy = iconH / srcH;

        // Use the shorter SVG path data (simplified airplane shape).
        // We'll build a simple arrow/chevron shape to avoid embedding 10 KB of path data.
        // Replace with full path data from Untitled design.svg if desired.
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            // A clean stylised airplane shape, pointing UP (north = heading 0).
            // All coords in icon space, centered at (0,0), pointing toward negative-Y (up on screen).
            double w = iconW / 2.0;  // half-width = 10
            double h = iconH / 2.0;  // half-height = 12

            // Fuselage nose (top center)
            ctx.BeginFigure(new Point(0, -h), isFilled: true, isClosed: true);
            // Right wing tip
            ctx.LineTo(new Point(w, 2), isStroked: false, isSmoothJoin: true);
            // Right wing inner
            ctx.LineTo(new Point(w * 0.35, 0), isStroked: false, isSmoothJoin: true);
            // Right tail
            ctx.LineTo(new Point(w * 0.55, h), isStroked: false, isSmoothJoin: true);
            // Center tail notch
            ctx.LineTo(new Point(0, h * 0.6), isStroked: false, isSmoothJoin: true);
            // Left tail
            ctx.LineTo(new Point(-w * 0.55, h), isStroked: false, isSmoothJoin: true);
            // Left wing inner
            ctx.LineTo(new Point(-w * 0.35, 0), isStroked: false, isSmoothJoin: true);
            // Left wing tip
            ctx.LineTo(new Point(-w, 2), isStroked: false, isSmoothJoin: true);
        }

        geo.Freeze();
        return geo;
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth;
        double h = ActualHeight;

        if (w <= 0 || h <= 0)
            return;

        // Background fill
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x0D, 0x15, 0x20)), null, new Rect(0, 0, w, h));

        DrawGrid(dc, w, h);
        DrawCoastlineHint(dc, w, h);

        var flights = LiveFlights;
        if (flights is null || flights.Count == 0)
        {
            DrawEmptyState(dc, w, h);
            return;
        }

        foreach (var flight in flights)
        {
            DrawPlane(dc, flight, w, h);
        }
    }

    private static void DrawGrid(DrawingContext dc, double w, double h)
    {
        // Longitude lines every 30°
        for (int lon = -180; lon <= 180; lon += 30)
        {
            var (x, _) = MapProjection.LatLonToCanvas(0, lon, w, h);
            dc.DrawLine(GridPen, new Point(x, 0), new Point(x, h));
        }

        // Latitude lines every 30°
        for (int lat = -60; lat <= 60; lat += 30)
        {
            var (_, y) = MapProjection.LatLonToCanvas(lat, 0, w, h);
            dc.DrawLine(GridPen, new Point(0, y), new Point(w, y));
        }

        // Equator slightly brighter
        var eqPen = new Pen(new SolidColorBrush(Color.FromArgb(50, 90, 160, 200)), 1.0);
        var (_, eqY) = MapProjection.LatLonToCanvas(0, 0, w, h);
        dc.DrawLine(eqPen, new Point(0, eqY), new Point(w, eqY));
    }

    private static void DrawCoastlineHint(DrawingContext dc, double w, double h)
    {
        // Minimal continent silhouettes drawn as filled rectangles.
        // These are rough approximations — good enough for a tracker map.
        var landBrush = new SolidColorBrush(Color.FromArgb(25, 90, 140, 180));

        DrawLandRect(dc, landBrush, w, h, latMin: 24, latMax: 70, lonMin: -10, lonMax: 40);   // Europe
        DrawLandRect(dc, landBrush, w, h, latMin: -36, latMax: 36, lonMin: -20, lonMax: 52);  // Africa
        DrawLandRect(dc, landBrush, w, h, latMin: 24, latMax: 78, lonMin: 60, lonMax: 145);   // Asia
        DrawLandRect(dc, landBrush, w, h, latMin: 14, latMax: 72, lonMin: -170, lonMax: -50); // N. America
        DrawLandRect(dc, landBrush, w, h, latMin: -56, latMax: 14, lonMin: -82, lonMax: -34); // S. America
        DrawLandRect(dc, landBrush, w, h, latMin: -46, latMax: -10, lonMin: 112, lonMax: 155);// Australia
    }

    private static void DrawLandRect(
        DrawingContext dc, Brush brush, double w, double h,
        double latMin, double latMax, double lonMin, double lonMax)
    {
        var (x1, y1) = MapProjection.LatLonToCanvasClamped(latMax, lonMin, w, h);
        var (x2, y2) = MapProjection.LatLonToCanvasClamped(latMin, lonMax, w, h);
        if (x2 > x1 && y2 > y1)
            dc.DrawRoundedRectangle(brush, null, new Rect(x1, y1, x2 - x1, y2 - y1), 4, 4);
    }

    private static void DrawPlane(DrawingContext dc, LiveFlight flight, double w, double h)
    {
        var (cx, cy) = MapProjection.LatLonToCanvasClamped(
            flight.Latitude, flight.Longitude, w, h);

        var fill = flight.IsMyFlight ? MyFlightBrush : OtherBrush;

        // Rotate the icon to match heading; WPF RotateTransform is clockwise in degrees.
        dc.PushTransform(new TranslateTransform(cx, cy));
        dc.PushTransform(new RotateTransform(flight.Heading));

        dc.DrawGeometry(fill, null, PlaneGeometry);

        dc.Pop(); // RotateTransform
        dc.Pop(); // TranslateTransform

        // Label
        DrawLabel(dc, flight, cx, cy);
    }

    private static void DrawLabel(DrawingContext dc, LiveFlight flight, double cx, double cy)
    {
        var text = new FormattedText(
            $"{flight.Callsign}  FL{(int)(flight.Altitude / 100):000}",
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            LabelTypeface,
            LabelFontSize,
            LabelBrush,
            pixelsPerDip: 1.0);

        double lx = cx + 16;
        double ly = cy - text.Height / 2;

        var bgRect = new Rect(lx - 4, ly - 2, text.Width + 8, text.Height + 4);
        dc.DrawRoundedRectangle(LabelBgBrush, LabelBorderPen, bgRect, 4, 4);
        dc.DrawText(text, new Point(lx, ly));
    }

    private static void DrawEmptyState(DrawingContext dc, double w, double h)
    {
        var text = new FormattedText(
            "No aircraft online — connect your API token in Settings to see live fleet positions",
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            LabelTypeface,
            13,
            new SolidColorBrush(Color.FromRgb(0x3A, 0x50, 0x60)),
            pixelsPerDip: 1.0);

        dc.DrawText(text, new Point((w - text.Width) / 2, (h - text.Height) / 2));
    }
}
