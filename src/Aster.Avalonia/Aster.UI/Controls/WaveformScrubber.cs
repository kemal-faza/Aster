using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Aster.UI.Controls;

public sealed class WaveformScrubber : Control
{
    private const double ThumbRadius = 6.0;
    private const double ThumbStroke = 2.2;
    private const double TrackThickness = 2.0;
    private const double WaveStep = 2.0;
    private const double EndTaper = 10.0;

    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<WaveformScrubber, double>(nameof(Progress));

    public static readonly StyledProperty<int> SeedProperty =
        AvaloniaProperty.Register<WaveformScrubber, int>(nameof(Seed));

    public static readonly StyledProperty<IBrush> PlayedBrushProperty =
        AvaloniaProperty.Register<WaveformScrubber, IBrush>(
            nameof(PlayedBrush), new SolidColorBrush(Color.Parse("#F6A6C8")));

    public static readonly StyledProperty<IBrush> TrackBrushProperty =
        AvaloniaProperty.Register<WaveformScrubber, IBrush>(
            nameof(TrackBrush), new SolidColorBrush(Color.Parse("#26FFFFFF")));

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
        Height = 28;
        Focusable = true;
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

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width < 8 || height < 6)
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

        var centerY = height / 2;
        var progress = Math.Clamp(_dragging ? _dragRatio : Progress, 0, 1);
        var thumbX = trackLeft + progress * (trackRight - trackLeft);

        var trackPen = new Pen(TrackBrush, TrackThickness) { LineCap = PenLineCap.Round };
        context.DrawLine(trackPen, new Point(trackLeft, centerY), new Point(trackRight, centerY));

        if (progress > 0.001)
        {
            var (fill, top) = BuildWave(
                trackLeft, trackRight, thumbX, height, Seed, Energy, Phase);
            context.DrawGeometry(PlayedBrush, null, fill);

            using (context.PushOpacity(0.22))
            {
                context.DrawGeometry(null, new Pen(Brushes.White, 1.1), top);
            }
        }

        var thumbCenter = new Point(thumbX, centerY);
        var accent = PlayedBrush is ISolidColorBrush solid
            ? solid.Color
            : Color.Parse("#F6A6C8");
        var glow = new RadialGradientBrush
        {
            GradientStops = new GradientStops
            {
                new(Color.FromArgb(90, accent.R, accent.G, accent.B), 0),
                new(Color.FromArgb(0, accent.R, accent.G, accent.B), 1)
            }
        };
        context.DrawEllipse(
            glow, null, thumbCenter, ThumbRadius * 1.9, ThumbRadius * 1.9);

        context.DrawEllipse(
            Brushes.White,
            new Pen(PlayedBrush, ThumbStroke),
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

    internal static double AmplitudeAt(
        double t,
        double height,
        int seed,
        double energy,
        double phase)
    {
        t = Math.Clamp(t, 0, 1);
        var seedPhase = (seed % 97) * 0.37;
        var wave = 0.58
            + 0.24 * Math.Sin(t * Math.PI * 2 * 2.3 + seedPhase)
            + 0.15 * Math.Sin(t * Math.PI * 2 * 5.1 + seedPhase * 1.7)
            + 0.09 * Math.Sin(t * Math.PI * 2 * 9.7 + seedPhase * 0.6)
            + 0.05 * Math.Sin(t * Math.PI * 2 * 15.3 + seedPhase * 1.3);
        var shape = Math.Clamp(wave, 0.16, 1.0);
        var envelope = 0.70 + 0.30 * Math.Sin(Math.PI * t);
        var breath = 0.6 + 0.4 * Math.Sin(phase * Math.PI * 2 * 1.5);
        var gain = 1 + 0.18 * Math.Clamp(energy, 0, 1) * breath;
        var max = Math.Max(1, height / 2 - 3);
        return Math.Clamp(max * shape * envelope * gain, 0, max);
    }

    private static (StreamGeometry Fill, StreamGeometry Top) BuildWave(
        double trackLeft,
        double trackRight,
        double playedEdge,
        double height,
        int seed,
        double energy,
        double phase)
    {
        var span = trackRight - trackLeft;
        var played = Math.Clamp(playedEdge - trackLeft, 0, span);
        var count = Math.Max(2, (int)Math.Ceiling(played / WaveStep));
        var center = height / 2;
        var top = new List<Point>(count + 1);

        for (var i = 0; i <= count; i++)
        {
            var x = trackLeft + Math.Min(played, i * WaveStep);
            var t = span <= 0 ? 0 : (x - trackLeft) / span;
            var taper = Math.Clamp((playedEdge - x) / EndTaper, 0, 1);
            var amplitude = AmplitudeAt(t, height, seed, energy, phase) * Math.Sqrt(taper);
            top.Add(new Point(x, center - amplitude));
        }

        var fill = new StreamGeometry();
        using (var ctx = fill.Open())
        {
            ctx.BeginFigure(top[0], true);
            for (var i = 1; i < top.Count; i++)
            {
                ctx.LineTo(top[i]);
            }

            for (var i = top.Count - 1; i >= 0; i--)
            {
                ctx.LineTo(new Point(top[i].X, center + (center - top[i].Y)));
            }

            ctx.EndFigure(true);
        }

        var outline = new StreamGeometry();
        using (var ctx = outline.Open())
        {
            ctx.BeginFigure(top[0], false);
            for (var i = 1; i < top.Count; i++)
            {
                ctx.LineTo(top[i]);
            }

            ctx.EndFigure(false);
        }

        return (fill, outline);
    }
}
