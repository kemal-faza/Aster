using Aster.UI.Controls;
using Avalonia;
using Xunit;

namespace Aster.UI.Tests;

public class WaveformScrubberTests
{
    [Fact]
    public void BarCountMatchesWpfSpacing()
    {
        Assert.Equal(22, WaveformScrubber.BarCount(100));
        Assert.Equal(1, WaveformScrubber.BarCount(0));
    }

    [Fact]
    public void RatioClampsToUnitInterval()
    {
        Assert.Equal(0.5, WaveformScrubber.RatioForX(50, 100));
        Assert.Equal(0, WaveformScrubber.RatioForX(-10, 100));
        Assert.Equal(1, WaveformScrubber.RatioForX(120, 100));
        Assert.Equal(0, WaveformScrubber.RatioForX(50, 0));
    }

    [Fact]
    public void HasWpfDefaultBrushes()
    {
        var control = new WaveformScrubber();

        Assert.NotNull(control.PlayedBrush);
        Assert.NotNull(control.TrackBrush);
        Assert.Equal(16, control.Height);
    }
}
