using Aster.UI.Controls;
using Avalonia;
using Xunit;

namespace Aster.UI.Tests;

public class SeekSliderTests
{
    [Fact]
    public void CanBeConstructedWithoutPlatform()
    {
        var slider = new SeekSlider();

        Assert.Equal(1, slider.Maximum);
    }

    [Fact]
    public void HasIntrinsicHeight()
    {
        var slider = new SeekSlider();

        slider.Measure(new Size(200, 200));

        Assert.Equal(16, slider.DesiredSize.Height);
    }
}
