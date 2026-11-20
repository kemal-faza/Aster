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
    public void PlayedWidthClampsToTheControl()
    {
        Assert.Equal(0, WaveformScrubber.PlayedWidth(-0.2, 100));
        Assert.Equal(50, WaveformScrubber.PlayedWidth(0.5, 100));
        Assert.Equal(100, WaveformScrubber.PlayedWidth(1.2, 100));
    }

    [Fact]
    public void PointerSeekUpdatesProgressBeforeRaisingTheEvent()
    {
        var control = new WaveformScrubber();
        double? requested = null;
        control.SeekRequested += (_, ratio) => requested = ratio;

        control.ApplyPointerSeek(0.75);

        Assert.Equal(0.75, control.Progress);
        Assert.Equal(0.75, requested);
    }

    [Fact]
    public void HasWpfDefaultBrushes()
    {
        var control = new WaveformScrubber();

        Assert.NotNull(control.PlayedBrush);
        Assert.NotNull(control.TrackBrush);
        Assert.Equal(16, control.Height);
    }

    [Fact]
    public void EnergyZeroKeepsTheStaticHeight()
    {
        var baseHeight = WaveformScrubber.StaticBarHeightForTest(10, 16, 1234);
        var actual = WaveformScrubber.BarHeightForTest(10, 79, 16, 0.5, 0, 0, 1234);

        Assert.InRange(actual, 3.5, 15.5);
        Assert.Equal(baseHeight, actual, 3);
    }

    [Fact]
    public void EnergyModulatesButStaysInsideTheControl()
    {
        var low = WaveformScrubber.BarHeightForTest(39, 79, 16, 0.5, 0.2, 0.0, 1234);
        var high = WaveformScrubber.BarHeightForTest(39, 79, 16, 0.5, 1.0, 0.0, 1234);

        Assert.InRange(low, 3.5, 15.5);
        Assert.InRange(high, 3.5, 15.5);
        Assert.NotEqual(low, high);
    }

    [Fact]
    public void PhaseChangesTheModulation()
    {
        var phaseA = WaveformScrubber.BarHeightForTest(39, 79, 16, 0.5, 1.0, 0.0, 1234);
        var phaseB = WaveformScrubber.BarHeightForTest(39, 79, 16, 0.5, 1.0, 0.17, 1234);

        Assert.NotEqual(phaseA, phaseB);
    }
}
