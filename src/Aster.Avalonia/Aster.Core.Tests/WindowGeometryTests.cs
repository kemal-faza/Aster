using Aster.Core.Logic;

namespace Aster.Core.Tests;

public class WindowGeometryTests
{
    [Theory]
    [InlineData(0, 1000, 300, 1.0, 350)]
    [InlineData(0, 1000, 300, 1.25, 312)]
    [InlineData(0, 1000, 300, 1.5, 275)]
    public void CentersLeft(int screenX, int screenWidth, double logicalWidth, double scaling, int expected)
    {
        Assert.Equal(expected, WindowGeometry.CenteredLeft(screenX, screenWidth, logicalWidth, scaling));
    }

    [Fact]
    public void MinimalCollapsedSizesAreUsed()
    {
        Assert.Equal(160, WindowGeometry.CollapsedWidthFor(minimal: true));
        Assert.Equal(12, WindowGeometry.CollapsedHeightFor(minimal: true));
        Assert.Equal(300, WindowGeometry.CollapsedWidthFor(minimal: false));
        Assert.Equal(38, WindowGeometry.CollapsedHeightFor(minimal: false));
    }

    [Theory]
    [InlineData(100, 50, 0, 0, true)]
    [InlineData(100, 50, 99, 49, true)]
    [InlineData(100, 50, 100, 49, false)]
    [InlineData(100, 50, 99, 50, false)]
    [InlineData(100, 50, -1, 10, false)]
    [InlineData(100, 50, 10, -1, false)]
    public void ContainsUsesHalfOpenBounds(
        int width, int height, int x, int y, bool expected)
    {
        Assert.Equal(expected, WindowGeometry.Contains(0, 0, width, height, x, y));
    }
}
