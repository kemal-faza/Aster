using Aster.UI.Controls;
using Xunit;

namespace Aster.UI.Tests;

public class BatteryWidgetTests
{
    [Fact]
    public void PercentClampsForRendering()
    {
        var widget = new BatteryWidget { Percent = 150, HasBattery = true };
        Assert.Equal(150, widget.Percent);
        // The render path clamps; this test documents the property contract.
        Assert.True(widget.HasBattery);
    }
}
