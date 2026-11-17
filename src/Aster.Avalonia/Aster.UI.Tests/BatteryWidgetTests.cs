using Aster.UI.Controls;
using Avalonia;
using Xunit;

namespace Aster.UI.Tests;

public class BatteryWidgetTests
{
    [Fact]
    public void PropertiesAndDesiredSize()
    {
        var widget = new BatteryWidget { Percent = 150, HasBattery = true };

        widget.Measure(new Size(200, 200));

        Assert.Equal(150, widget.Percent);
        Assert.True(widget.HasBattery);
        Assert.Equal(new Size(26, 12), widget.DesiredSize);
    }
}
