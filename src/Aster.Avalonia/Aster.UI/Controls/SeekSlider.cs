using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Aster.UI.Controls;

public sealed class SeekSlider : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<SeekSlider, double>(nameof(Value));

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<SeekSlider, double>(nameof(Maximum), 1);

    static SeekSlider()
    {
        AffectsRender<SeekSlider>(ValueProperty, MaximumProperty);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public event EventHandler? SeekStarted;
    public event EventHandler<double>? SeekCompleted;

    private bool _dragging;

    public SeekSlider()
    {
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var y = Bounds.Height / 2;
        var ratio = Maximum <= 0 ? 0 : Math.Clamp(Value / Maximum, 0, 1);

        context.DrawRectangle(new SolidColorBrush(Color.Parse("#26FFFFFF")), null,
            new Rect(0, y - 1, width, 2), 1, 1);
        context.DrawRectangle(new SolidColorBrush(Color.Parse("#7FB8FF")), null,
            new Rect(0, y - 1, width * ratio, 2), 1, 1);
        context.DrawEllipse(new SolidColorBrush(Color.Parse("#F5F5F7")), null,
            new Point(width * ratio, y), 5, 5);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        _dragging = true;
        e.Pointer.Capture(this);
        SeekStarted?.Invoke(this, EventArgs.Empty);
        UpdateValue(e.GetPosition(this).X);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (_dragging)
        {
            UpdateValue(e.GetPosition(this).X);
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        UpdateValue(e.GetPosition(this).X);
        e.Pointer.Capture(null);
        SeekCompleted?.Invoke(this, Value);
        e.Handled = true;
    }

    private void UpdateValue(double x)
    {
        if (Maximum <= 0 || Bounds.Width <= 0)
        {
            return;
        }

        Value = Math.Clamp(x / Bounds.Width, 0, 1) * Maximum;
        InvalidateVisual();
    }
}
