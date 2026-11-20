using Aster.UI.Controls;
using Avalonia;
using Xunit;

namespace Aster.UI.Tests;

public class WaveformScrubberTests
{
    [Fact]
    public void RatioClampsToUnitInterval()
    {
        Assert.Equal(0.5, WaveformScrubber.RatioForX(50, 100));
        Assert.Equal(0, WaveformScrubber.RatioForX(-10, 100));
        Assert.Equal(1, WaveformScrubber.RatioForX(120, 100));
        Assert.Equal(0, WaveformScrubber.RatioForX(50, 0));
    }

    [Fact]
    public void ThumbCenterStaysInsideTheTrack()
    {
        Assert.Equal(6, WaveformScrubber.ThumbCenter(0, 100), 3);
        Assert.Equal(50, WaveformScrubber.ThumbCenter(0.5, 100), 3);
        Assert.Equal(94, WaveformScrubber.ThumbCenter(1, 100), 3);
        Assert.Equal(6, WaveformScrubber.ThumbCenter(-1, 100), 3);
        Assert.Equal(94, WaveformScrubber.ThumbCenter(2, 100), 3);
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
    public void DragPreviewDoesNotSeekUntilRelease()
    {
        var control = new WaveformScrubber();
        double? requested = null;
        control.SeekRequested += (_, ratio) => requested = ratio;

        control.BeginPointerDrag(0.25);
        control.UpdatePointerDrag(0.75);

        Assert.True(control.IsDragging);
        Assert.Equal(0.75, control.DragRatio, 3);
        Assert.Null(requested);

        control.EndPointerDrag(0.75);

        Assert.False(control.IsDragging);
        Assert.Equal(0.75, requested!.Value, 3);
        Assert.Equal(0.75, control.Progress, 3);
    }

    [Fact]
    public void DragRatioClamps()
    {
        var control = new WaveformScrubber();

        control.BeginPointerDrag(-1);
        Assert.Equal(0, control.DragRatio);

        control.UpdatePointerDrag(2);
        Assert.Equal(1, control.DragRatio);

        control.EndPointerDrag(2);
        Assert.Equal(1, control.Progress);
    }

    [Fact]
    public void HasDefaultBrushesAndHeight()
    {
        var control = new WaveformScrubber();

        Assert.NotNull(control.PlayedBrush);
        Assert.NotNull(control.TrackBrush);
        Assert.Equal(28, control.Height);
    }

    [Fact]
    public void AmplitudeIsDeterministicForTheSameSeed()
    {
        var first = WaveformScrubber.AmplitudeAt(0.4, 28, 1234, 0, 0);
        var second = WaveformScrubber.AmplitudeAt(0.4, 28, 1234, 0, 0);

        Assert.Equal(first, second, 3);
    }

    [Fact]
    public void EnergyZeroIgnoresPhase()
    {
        var first = WaveformScrubber.AmplitudeAt(0.4, 28, 1234, 0, 0);
        var second = WaveformScrubber.AmplitudeAt(0.4, 28, 1234, 0, 0.17);

        Assert.Equal(first, second, 3);
    }

    [Fact]
    public void EnergyAndPhaseModulateTheAmplitude()
    {
        var low = WaveformScrubber.AmplitudeAt(0.4, 100, 1234, 0, 0);
        var high = WaveformScrubber.AmplitudeAt(0.4, 100, 1234, 1, 0.17);

        Assert.NotEqual(low, high, 3);
    }

    [Fact]
    public void AmplitudeStaysInsideTheControl()
    {
        for (var i = 0; i <= 20; i++)
        {
            var amplitude = WaveformScrubber.AmplitudeAt(i / 20.0, 28, 1234, 1, 0.42);

            Assert.InRange(amplitude, 0, 11);
        }
    }
}
