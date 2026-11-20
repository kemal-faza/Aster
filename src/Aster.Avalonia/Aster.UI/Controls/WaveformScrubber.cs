using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Aster.UI.Controls;

public sealed class WaveformScrubber : Control
{
    private const double ThumbRadius = 7.0;
    private const double ThumbStroke = 2.6;
    private const double TrackThickness = 4.0;
    private const double WaveStep = 3.0;
    private const double TopMargin = 4.0;
    private const double UnplayedOpacity = 0.25;

    // The mockup's layers: 70/50/35% height and 60/40/20% opacity. The tallest
    // layer sits behind, so the bright ridge stays visible above the shorter
    // layers; the vertical gradient fades the body toward the track.
    private static readonly LayerSpec[] Layers =
    {
        new(0.70, 0.60, 0.000),
        new(0.50, 0.40, 0.200),
        new(0.35, 0.20, 0.400)
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

        // The tallest layer defines the available height; the others scale
        // down from it. A short top margin keeps the ridge inside the control.
        var waveHeight = Math.Max(1, trackY - TopMargin);
        var maxAmplitude = waveHeight / Layers[0].Scale;

        if (progress < 0.9995)
        {
            using (context.PushClip(new Rect(thumbX, 0, width - thumbX, height)))
            using (context.PushOpacity(UnplayedOpacity))
            {
                DrawLayers(
                    context, trackLeft, trackRight, trackY, maxAmplitude,
                    Seed, Energy, Phase, accent);
            }
        }

        if (progress > 0.0005)
        {
            using (context.PushClip(new Rect(0, 0, thumbX, height)))
            {
                DrawLayers(
                    context, trackLeft, trackRight, trackY, maxAmplitude,
                    Seed, Energy, Phase, accent);
            }
        }

        var trackPen = new Pen(TrackBrush, TrackThickness) { LineCap = PenLineCap.Round };
        context.DrawLine(trackPen, new Point(trackLeft, trackY), new Point(trackRight, trackY));

        if (progress > 0.0005)
        {
            var playedPen = new Pen(PlayedBrush, TrackThickness) { LineCap = PenLineCap.Round };
            context.DrawLine(playedPen, new Point(trackLeft, trackY), new Point(thumbX, trackY));
        }

        var thumbCenter = new Point(thumbX, trackY);
        var glow = new RadialGradientBrush
        {
            GradientStops = new GradientStops
            {
                new(Color.FromArgb(0x80, accent.R, accent.G, accent.B), 0),
                new(Color.FromArgb(0x2A, accent.R, accent.G, accent.B), 0.55),
                new(Color.FromArgb(0x00, accent.R, accent.G, accent.B), 1)
            }
        };
        context.DrawEllipse(
            glow, null, thumbCenter, ThumbRadius * 3.0, ThumbRadius * 3.0);

        context.DrawEllipse(
            new SolidColorBrush(Color.Parse("#E6100E12")),
            null,
            thumbCenter,
            ThumbRadius,
            ThumbRadius);

        context.DrawEllipse(
            null,
            new Pen(Brushes.White, ThumbStroke),
            thumbCenter,
            ThumbRadius,
            ThumbRadius);
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
    /// Normalized 0..1 contour for one layer. The shape is a short sum of
    /// spatial sines whose amplitudes and phases drift slowly with playback
    /// time. The path is deterministic from <paramref name="seed"/> and is
    /// continuous in time; the playback position freezes it on pause.
    /// </summary>
    internal static double EnvelopeAt(
        double t,
        int seed,
        double energy,
        double phase,
        double layerPhase = 0)
    {
        t = Math.Clamp(t, 0, 1);
        var seedPhase = (seed % 97) * 0.37;
        var turn = t * Math.PI * 2;
        var drift = phase * 0.21;
        var lp = layerPhase * Math.PI * 2;

        // Incommensurate slow modulation gives a long, non-repeating path
        // without the acceleration jumps of a sampled noise field.
        var w1 = 0.28 + 0.05 * Math.Sin(drift * 0.53 + lp * 1.3 + 0.6);
        var w2 = 0.16 + 0.04 * Math.Sin(drift * 0.71 + lp * 2.1 + 1.7);
        var w3 = 0.08 + 0.02 * Math.Sin(drift * 0.97 + lp * 3.3 + 3.1);

        // The linear terms make the hills flow across the track; the slow
        // sines add sway so the contour keeps changing shape. The layers use
        // opposite directions and different speeds, so they cross smoothly.
        var p1 = seedPhase + 1.20 * drift + 0.35 * Math.Sin(drift * 0.45 + lp * 0.9 + 0.2);
        var p2 = seedPhase * 1.7 - 1.80 * drift + 0.45 * Math.Sin(drift * 0.33 + lp * 1.7 + 1.1);
        var p3 = seedPhase * 0.6 + 2.40 * drift + 0.55 * Math.Sin(drift * 0.27 + lp * 2.7 + 2.3);

        var wave = 0.48
            + w1 * Math.Sin(turn * 3.1 + p1)
            + w2 * Math.Sin(turn * 6.7 + p2)
            + w3 * Math.Sin(turn * 11.3 + p3);

        var shape = Math.Clamp(wave, 0.12, 1.0);
        var window = 0.90 + 0.10 * Math.Sin(Math.PI * t);

        // Energy only adds a tiny lift. The morph speed is constant, so the
        // animation never locks to the beat.
        var lift = 1 + 0.03 * Math.Clamp(energy, 0, 1);

        return Math.Clamp(shape * window * lift, 0, 1);
    }

    internal static double AmplitudeAt(
        double t,
        double height,
        int seed,
        double energy,
        double phase)
    {
        var max = Math.Max(1, height / 2 - 3);
        return max * EnvelopeAt(t, seed, energy, phase);
    }

    private static void DrawLayers(
        DrawingContext context,
        double left,
        double right,
        double baselineY,
        double maxAmplitude,
        int seed,
        double energy,
        double phase,
        Color accent)
    {
        foreach (var layer in Layers)
        {
            var geometry = BuildLayer(
                left, right, baselineY, maxAmplitude * layer.Scale,
                seed, energy, phase, layer.Phase);
            context.DrawGeometry(
                CreateLayerBrush(accent, layer.Alpha), null, geometry);
        }
    }

    private static StreamGeometry BuildLayer(
        double left,
        double right,
        double baselineY,
        double amplitude,
        int seed,
        double energy,
        double phase,
        double layerPhase)
    {
        var span = right - left;
        var count = Math.Max(2, (int)Math.Ceiling(span / WaveStep));
        var maxAmp = Math.Max(0.01, amplitude);
        var points = new List<Point>(count + 1);

        for (var i = 0; i <= count; i++)
        {
            var x = left + Math.Min(span, i * WaveStep);
            var t = span <= 0 ? 0 : (x - left) / span;
            var amp = Math.Clamp(
                EnvelopeAt(t, seed, energy, phase, layerPhase) * maxAmp,
                0,
                maxAmp);
            points.Add(new Point(x, baselineY - amp));
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(points[0], true);

            // Catmull-Rom through the samples. Control points are clamped to
            // the layer's band so the curve cannot dip under the track or rise
            // above its own ridge.
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

            ctx.LineTo(new Point(right, baselineY));
            ctx.LineTo(new Point(left, baselineY));
            ctx.EndFigure(true);
        }

        return geometry;
    }

    private static IBrush CreateLayerBrush(Color accent, double alpha)
    {
        static byte Alpha(double value) =>
            (byte)Math.Round(Math.Clamp(value, 0, 1) * 255);

        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new(Color.FromArgb(Alpha(alpha), accent.R, accent.G, accent.B), 0),
                new(Color.FromArgb(Alpha(alpha), accent.R, accent.G, accent.B), 0.72),
                new(Color.FromArgb(Alpha(alpha * 0.30), accent.R, accent.G, accent.B), 1)
            }
        };
    }

    internal readonly record struct LayerSpec(double Scale, double Alpha, double Phase);
}
