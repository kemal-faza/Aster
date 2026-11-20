using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Aster.UI.Controls;

public sealed class WaveformScrubber : Control
{
    private const double ThumbRadius = 7.0;
    private const double ThumbStroke = 2.2;
    private const double TrackThickness = 3.0;
    private const double PlayedTrackThickness = 4.5;
    private const double WaveStep = 3.0;
    private const double WaveAmplitude = 18.0;
    private const double PhaseSpeed = 0.35;
    private const double GlowRadius = 14.0;

    // Final design: five copies of the same ocean swell. The largest layer
    // leads, the rest start later, sit lower, and stay quieter. Each layer's
    // envelope peak moves right with its delay so the copies stay readable.
    private static readonly LayerSpec[] Layers =
    {
        new(1.00, 0.65, 0.0, 0.50),
        new(0.86, 0.50, 0.6, 0.53),
        new(0.72, 0.38, 1.2, 0.56),
        new(0.58, 0.28, 1.8, 0.60),
        new(0.46, 0.20, 2.4, 0.64)
    };

    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<WaveformScrubber, double>(nameof(Progress));

    public static readonly StyledProperty<int> SeedProperty =
        AvaloniaProperty.Register<WaveformScrubber, int>(nameof(Seed));

    public static readonly StyledProperty<IBrush> PlayedBrushProperty =
        AvaloniaProperty.Register<WaveformScrubber, IBrush>(
            nameof(PlayedBrush), new SolidColorBrush(Color.Parse("#F6A6C8")));

    public static readonly StyledProperty<IBrush> TrackBrushProperty =
        AvaloniaProperty.Register<WaveformScrubber, IBrush>(
            nameof(TrackBrush), new SolidColorBrush(Color.Parse("#4DFFFFFF")));

    public static readonly StyledProperty<double> DurationSecondsProperty =
        AvaloniaProperty.Register<WaveformScrubber, double>(nameof(DurationSeconds));

    public static readonly StyledProperty<double> EnergyProperty =
        AvaloniaProperty.Register<WaveformScrubber, double>(nameof(Energy));

    public static readonly StyledProperty<double> PhaseProperty =
        AvaloniaProperty.Register<WaveformScrubber, double>(nameof(Phase));

    private bool _dragging;
    private double _dragRatio;

    static WaveformScrubber()
    {
        AffectsRender<WaveformScrubber>(
            ProgressProperty, SeedProperty, PlayedBrushProperty, TrackBrushProperty,
            EnergyProperty, PhaseProperty);
    }

    public WaveformScrubber()
    {
        Height = 36;
        Focusable = true;
        FocusAdorner = null;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Cursor is null)
        {
            Cursor = new Cursor(StandardCursorType.Hand);
        }
    }

    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public int Seed
    {
        get => GetValue(SeedProperty);
        set => SetValue(SeedProperty, value);
    }

    public IBrush PlayedBrush
    {
        get => GetValue(PlayedBrushProperty);
        set => SetValue(PlayedBrushProperty, value);
    }

    public IBrush TrackBrush
    {
        get => GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public double DurationSeconds
    {
        get => GetValue(DurationSecondsProperty);
        set => SetValue(DurationSecondsProperty, value);
    }

    public double Energy
    {
        get => GetValue(EnergyProperty);
        set => SetValue(EnergyProperty, value);
    }

    public double Phase
    {
        get => GetValue(PhaseProperty);
        set => SetValue(PhaseProperty, value);
    }

    public event EventHandler<double>? SeekRequested;

    internal static IReadOnlyList<LayerSpec> LayerSpecs => Layers;

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width < 8 || height < 8)
        {
            return;
        }

        context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, width, height));

        var trackLeft = ThumbRadius;
        var trackRight = width - ThumbRadius;
        if (trackRight - trackLeft < 1)
        {
            return;
        }

        var trackY = height - 8;
        var progress = Math.Clamp(_dragging ? _dragRatio : Progress, 0, 1);
        var thumbX = trackLeft + progress * (trackRight - trackLeft);
        var accent = PlayedBrush is ISolidColorBrush solid
            ? solid.Color
            : Color.Parse("#F6A6C8");

        var trackPen = new Pen(TrackBrush, TrackThickness) { LineCap = PenLineCap.Round };
        context.DrawLine(trackPen, new Point(trackLeft, trackY), new Point(trackRight, trackY));

        if (progress > 0.0005 && thumbX - trackLeft >= 1)
        {
            var playedBrush = CreateWaveBrush(accent, 1.0);
            var playedPen = new Pen(playedBrush, PlayedTrackThickness) { LineCap = PenLineCap.Round };
            context.DrawLine(playedPen, new Point(trackLeft, trackY), new Point(thumbX, trackY));

            // The wave lives only on the played side. Its height is the final
            // 18 px, capped so a short control still keeps the ridge on screen.
            var amplitude = Math.Min(WaveAmplitude, Math.Max(1, trackY - 2));
            DrawOceanLayers(context, trackLeft, thumbX, trackY, amplitude, Seed, Phase, accent);
        }

        DrawThumb(context, thumbX, trackY, accent);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        BeginPointerDrag(RatioForX(e.GetPosition(this).X, Bounds.Width));
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging)
        {
            return;
        }

        UpdatePointerDrag(RatioForX(e.GetPosition(this).X, Bounds.Width));
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging)
        {
            return;
        }

        EndPointerDrag(RatioForX(e.GetPosition(this).X, Bounds.Width));
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragging = false;
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var delta = DurationSeconds > 0 ? 5 / DurationSeconds : 0;
        var target = e.Key switch
        {
            Key.Left => Progress - delta,
            Key.Right => Progress + delta,
            Key.Home => 0,
            Key.End => 1,
            _ => double.NaN
        };

        if (double.IsNaN(target))
        {
            return;
        }

        ApplyPointerSeek(target);
        e.Handled = true;
    }

    internal bool IsDragging => _dragging;

    internal double DragRatio => _dragRatio;

    internal void BeginPointerDrag(double ratio)
    {
        _dragging = true;
        _dragRatio = Math.Clamp(ratio, 0, 1);
        InvalidateVisual();
    }

    internal void UpdatePointerDrag(double ratio)
    {
        if (!_dragging)
        {
            return;
        }

        _dragRatio = Math.Clamp(ratio, 0, 1);
        InvalidateVisual();
    }

    internal void EndPointerDrag(double ratio)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        _dragRatio = Math.Clamp(ratio, 0, 1);
        ApplyPointerSeek(_dragRatio);
        InvalidateVisual();
    }

    internal void ApplyPointerSeek(double ratio)
    {
        var clamped = Math.Clamp(ratio, 0, 1);
        SetCurrentValue(ProgressProperty, clamped);
        SeekRequested?.Invoke(this, clamped);
    }

    internal static double RatioForX(double x, double width)
    {
        var trackLeft = ThumbRadius;
        var trackRight = width - ThumbRadius;
        if (trackRight <= trackLeft)
        {
            return 0;
        }

        return Math.Clamp((x - trackLeft) / (trackRight - trackLeft), 0, 1);
    }

    internal static double ThumbCenter(double progress, double width)
    {
        var trackLeft = ThumbRadius;
        var trackRight = width - ThumbRadius;
        if (trackRight <= trackLeft)
        {
            return width / 2;
        }

        return trackLeft + Math.Clamp(progress, 0, 1) * (trackRight - trackLeft);
    }

    /// <summary>
    /// Normalized 0..1 height of the ocean swell at <paramref name="u"/> across
    /// the played width. The bell-shaped window is fixed to the played width;
    /// the crest travels through it with playback position, so pausing freezes
    /// the crest. Each layer supplies its own window peak.
    /// </summary>
    internal static double OceanEnvelopeAt(
        double u,
        int seed,
        double phase,
        double peak = 0.50)
    {
        u = Math.Clamp(u, 0, 1);
        peak = Math.Clamp(peak, 0.01, 0.99);

        var sigma = u < peak ? 0.32 : 0.26;
        var window = Math.Exp(-0.5 * Math.Pow((u - peak) / sigma, 2));

        var seedPhase = (seed % 97) * 0.37;
        var travel = 2 * Math.PI * (0.80 * u - PhaseSpeed * phase) + seedPhase;
        var primary = Math.Sin(travel);
        var secondary = 0.06 * Math.Sin(2 * travel + 1.3);
        var shape = 0.5 + 0.5 * (primary + secondary) / 1.06;

        // The window dominates; the ripple only varies the height by 38%, so
        // the crest drifts gently instead of pumping with every cycle.
        return Math.Clamp(window * (0.62 + 0.38 * shape), 0, 1);
    }

    internal static double AmplitudeAt(
        double u,
        double height,
        int seed,
        double phase,
        double peak = 0.50)
    {
        return Math.Max(1, height) * OceanEnvelopeAt(u, seed, phase, peak);
    }

    private static void DrawOceanLayers(
        DrawingContext context,
        double left,
        double right,
        double baselineY,
        double amplitude,
        int seed,
        double phase,
        Color accent)
    {
        var contour = ShiftHue(accent, 0, -0.42);

        for (var i = 0; i < Layers.Length; i++)
        {
            var layer = Layers[i];
            var maxAmplitude = Math.Max(0.01, amplitude * layer.Scale);
            var (fill, top) = BuildOceanLayer(
                left, right, baselineY, maxAmplitude,
                seed, phase - layer.Delay, layer.Peak);

            context.DrawGeometry(CreateWaveBrush(accent, layer.Opacity), null, fill);

            if (i == 0)
            {
                var under = new Pen(
                    new SolidColorBrush(Color.FromArgb(90, contour.R, contour.G, contour.B)), 3.0);
                context.DrawGeometry(null, under, top);
                context.DrawGeometry(null, new Pen(Brushes.White, 1.6), top);
            }
            else
            {
                var alpha = (byte)Math.Round(
                    Math.Clamp(0.55 * layer.Opacity, 0, 1) * 255);
                var width = 0.9 + 0.5 * layer.Opacity;
                var pen = new Pen(
                    new SolidColorBrush(Color.FromArgb(alpha, contour.R, contour.G, contour.B)), width);
                context.DrawGeometry(null, pen, top);
            }
        }
    }

    private static (StreamGeometry Fill, StreamGeometry Top) BuildOceanLayer(
        double left,
        double right,
        double baselineY,
        double amplitude,
        int seed,
        double phase,
        double peak)
    {
        var span = right - left;
        var count = Math.Max(2, (int)Math.Ceiling(span / WaveStep));
        var maxAmp = Math.Max(0.01, amplitude);
        var points = new List<Point>(count + 1);

        for (var i = 0; i <= count; i++)
        {
            var x = left + Math.Min(span, i * WaveStep);
            var u = span <= 0 ? 0 : (x - left) / span;
            var amp = Math.Clamp(
                OceanEnvelopeAt(u, seed, phase, peak) * maxAmp,
                0,
                maxAmp);
            points.Add(new Point(x, baselineY - amp));
        }

        var top = new StreamGeometry();
        using (var ctx = top.Open())
        {
            ctx.BeginFigure(points[0], false);
            AddCurves(ctx, points, baselineY, maxAmp);
            ctx.EndFigure(false);
        }

        var fill = new StreamGeometry();
        using (var ctx = fill.Open())
        {
            ctx.BeginFigure(points[0], true);
            AddCurves(ctx, points, baselineY, maxAmp);
            ctx.LineTo(new Point(right, baselineY));
            ctx.LineTo(new Point(left, baselineY));
            ctx.EndFigure(true);
        }

        return (fill, top);
    }

    private static void AddCurves(
        StreamGeometryContext ctx,
        List<Point> points,
        double baselineY,
        double maxAmp)
    {
        // Catmull-Rom through the samples. Control points stay inside the
        // layer's band so the curve cannot dip below the track or overshoot.
        for (var i = 0; i < points.Count - 1; i++)
        {
            var p0 = points[Math.Max(0, i - 1)];
            var p1 = points[i];
            var p2 = points[i + 1];
            var p3 = points[Math.Min(points.Count - 1, i + 2)];
            var c1 = new Point(
                p1.X + (p2.X - p0.X) / 6,
                Math.Clamp(p1.Y + (p2.Y - p0.Y) / 6, baselineY - maxAmp, baselineY));
            var c2 = new Point(
                p2.X - (p3.X - p1.X) / 6,
                Math.Clamp(p2.Y - (p3.Y - p1.Y) / 6, baselineY - maxAmp, baselineY));
            ctx.CubicBezierTo(c1, c2, p2);
        }
    }

    private static void DrawThumb(
        DrawingContext context,
        double thumbX,
        double trackY,
        Color accent)
    {
        var center = new Point(thumbX, trackY);
        var glow = new RadialGradientBrush
        {
            GradientStops = new GradientStops
            {
                new(Color.FromArgb(0x5C, accent.R, accent.G, accent.B), 0),
                new(Color.FromArgb(0x17, accent.R, accent.G, accent.B), 0.6),
                new(Color.FromArgb(0x00, accent.R, accent.G, accent.B), 1)
            }
        };

        context.DrawEllipse(glow, null, center, GlowRadius, GlowRadius);
        context.DrawEllipse(
            new SolidColorBrush(accent), null, center, ThumbRadius, ThumbRadius);
        context.DrawEllipse(
            null, new Pen(Brushes.White, ThumbStroke), center, ThumbRadius, ThumbRadius);
    }

    private static IBrush CreateWaveBrush(Color accent, double alpha)
    {
        var start = ShiftHue(accent, -22, 0.00);
        var end = ShiftHue(accent, 26, 0.06);

        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new(WithAlpha(start, alpha), 0),
                new(WithAlpha(accent, alpha), 0.45),
                new(WithAlpha(end, alpha), 1)
            }
        };
    }

    private static Color WithAlpha(Color color, double alpha)
    {
        var value = (byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255);
        return Color.FromArgb(value, color.R, color.G, color.B);
    }

    private static Color ShiftHue(Color color, double degrees, double lightnessDelta)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        var lightness = (max + min) / 2;
        var saturation = 0.0;
        var hue = 0.0;

        if (delta > 0.00001)
        {
            saturation = delta / (1 - Math.Abs(2 * lightness - 1));
            hue = max == r
                ? 60 * (((g - b) / delta) % 6)
                : max == g
                    ? 60 * ((b - r) / delta + 2)
                    : 60 * ((r - g) / delta + 4);
            if (hue < 0)
            {
                hue += 360;
            }
        }

        hue = (hue + degrees + 360) % 360;
        var targetLightness = Math.Clamp(lightness + lightnessDelta, 0, 1);
        var chroma = (1 - Math.Abs(2 * targetLightness - 1)) * saturation;
        var x = chroma * (1 - Math.Abs(hue / 60 % 2 - 1));
        var m = targetLightness - chroma / 2;
        var (rr, gg, bb) = hue switch
        {
            < 60 => (chroma, x, 0.0),
            < 120 => (x, chroma, 0.0),
            < 180 => (0.0, chroma, x),
            < 240 => (0.0, x, chroma),
            < 300 => (x, 0.0, chroma),
            _ => (chroma, 0.0, x)
        };

        return Color.FromRgb(
            (byte)Math.Clamp(Math.Round((rr + m) * 255), 0, 255),
            (byte)Math.Clamp(Math.Round((gg + m) * 255), 0, 255),
            (byte)Math.Clamp(Math.Round((bb + m) * 255), 0, 255));
    }

    internal readonly record struct LayerSpec(
        double Scale,
        double Opacity,
        double Delay,
        double Peak);
}
