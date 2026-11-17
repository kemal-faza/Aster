using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Aster.UI.Controls;

public sealed class BatteryWidget : Control
{
    public static readonly StyledProperty<int> PercentProperty =
        AvaloniaProperty.Register<BatteryWidget, int>(nameof(Percent));

    public static readonly StyledProperty<bool> IsChargingProperty =
        AvaloniaProperty.Register<BatteryWidget, bool>(nameof(IsCharging));

    public static readonly StyledProperty<bool> HasBatteryProperty =
        AvaloniaProperty.Register<BatteryWidget, bool>(nameof(HasBattery), true);

    static BatteryWidget()
    {
        AffectsRender<BatteryWidget>(PercentProperty, IsChargingProperty, HasBatteryProperty);
    }

    public int Percent
    {
        get => GetValue(PercentProperty);
        set => SetValue(PercentProperty, value);
    }

    public bool IsCharging
    {
        get => GetValue(IsChargingProperty);
        set => SetValue(IsChargingProperty, value);
    }

    public bool HasBattery
    {
        get => GetValue(HasBatteryProperty);
        set => SetValue(HasBatteryProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) => new(26, 12);

    public override void Render(DrawingContext context)
    {
        if (!HasBattery)
        {
            return;
        }

        var pen = new Pen(new SolidColorBrush(Color.Parse("#66FFFFFF")), 1);
        var body = new Rect(0, 0, 24, 12);
        context.DrawRectangle(null, pen, body, 3, 3);

        var fill = Math.Clamp(Percent, 0, 100) / 100.0 * 20;
        var color = IsCharging
            ? Color.Parse("#32D74B")
            : Percent <= 20 ? Color.Parse("#FF9F0A") : Color.Parse("#F5F5F7");
        context.DrawRectangle(new SolidColorBrush(color), null,
            new Rect(2, 2, fill, 8), 1.5, 1.5);

        context.DrawRectangle(new SolidColorBrush(Color.Parse("#66FFFFFF")), null,
            new Rect(24, 4, 2, 4), 1, 0);
    }
}
