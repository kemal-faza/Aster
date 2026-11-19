using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Aster.UI.Controls;

public sealed class WaveformScrubber : Control
{
    private const double BarWidth = 2.5;
    private const double Gap = 2.0;
    private const double Step = BarWidth + Gap;

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

    static WaveformScrubber()
    {
        AffectsRender<WaveformScrubber>(
            ProgressProperty, SeedProperty, PlayedBrushProperty, TrackBrushProperty);
    }

    public WaveformScrubber()
    {
        Height = 16;
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

        var count = BarCount(width);
        var total = count * Step - Gap;
        var left = (width - total) / 2;
        var progress = Math.Clamp(Progress, 0, 1);
        var playedEdge = progress * width;

        for (var i = 0; i < count; i++)
        {
            var noise = Hash(Seed, i) / (double)int.MaxValue;
            var wave = 0.5 + 0.5 * Math.Sin(i * 0.63 + (Seed % 29) * 0.37);
            var mix = 0.58 * noise + 0.42 * wave;
            var barHeight = 3.5 + (height - 4.0) * (0.12 + 0.88 * mix);
            var x = left + i * Step;
            var y = (height - barHeight) / 2;
            var brush = x + BarWidth / 2 <= playedEdge ? PlayedBrush : TrackBrush;

            context.DrawRectangle(brush, null, new Rect(x, y, BarWidth, barHeight),
                BarWidth / 2, BarWidth / 2);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var ratio = RatioForX(e.GetPosition(this).X, Bounds.Width);
        SeekRequested?.Invoke(this, ratio);
        e.Handled = true;
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

        SetCurrentValue(ProgressProperty, Math.Clamp(target, 0, 1));
        SeekRequested?.Invoke(this, Progress);
        e.Handled = true;
    }

    internal static int BarCount(double width) =>
        width < 8 ? 1 : Math.Max(1, (int)Math.Floor((width + Gap) / Step));

    internal static double RatioForX(double x, double width) =>
        width <= 0 ? 0 : Math.Clamp(x / width, 0, 1);

    private static int Hash(int seed, int index)
    {
        unchecked
        {
            var value = seed * 374761393 + index * 668265263;
            value = (value ^ (value >> 13)) * 1274126177;
            return (value ^ (value >> 16)) & 0x7FFFFFFF;
        }
    }
}
