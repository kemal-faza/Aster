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
        Assert.Equal(7.0, WaveformScrubber.ThumbCenter(0, 100), 3);
        Assert.Equal(50, WaveformScrubber.ThumbCenter(0.5, 100), 3);
        Assert.Equal(93.0, WaveformScrubber.ThumbCenter(1, 100), 3);
        Assert.Equal(7.0, WaveformScrubber.ThumbCenter(-1, 100), 3);
        Assert.Equal(93.0, WaveformScrubber.ThumbCenter(2, 100), 3);
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
        Assert.Equal(36, control.Height);
    }

    [Fact]
    public void LayerSpecsMatchTheFinalDesign()
    {
        var layers = WaveformScrubber.LayerSpecs;

        Assert.Equal(5, layers.Count);
        Assert.Equal(new[] { 1.00, 0.86, 0.72, 0.58, 0.46 },
            new[] { layers[0].Scale, layers[1].Scale, layers[2].Scale, layers[3].Scale, layers[4].Scale });
        Assert.Equal(new[] { 0.65, 0.50, 0.38, 0.28, 0.20 },
            new[] { layers[0].Opacity, layers[1].Opacity, layers[2].Opacity, layers[3].Opacity, layers[4].Opacity });
        Assert.Equal(new[] { 0.0, 0.6, 1.2, 1.8, 2.4 },
            new[] { layers[0].Delay, layers[1].Delay, layers[2].Delay, layers[3].Delay, layers[4].Delay });
        Assert.Equal(new[] { 0.50, 0.53, 0.56, 0.60, 0.64 },
            new[] { layers[0].Peak, layers[1].Peak, layers[2].Peak, layers[3].Peak, layers[4].Peak });
    }

    [Fact]
    public void LayerPeaksShiftRight()
    {
        var layers = WaveformScrubber.LayerSpecs;

        for (var i = 0; i < layers.Count - 1; i++)
        {
            Assert.True(layers[i].Peak < layers[i + 1].Peak);
        }
    }

    [Fact]
    public void OceanEnvelopeStaysNormalized()
    {
        foreach (var peak in new[] { 0.50, 0.53, 0.56, 0.60, 0.64 })
        {
            for (var i = 0; i <= 40; i++)
            {
                var envelope = WaveformScrubber.OceanEnvelopeAt(i / 40.0, 1234, 0.42, peak);
                Assert.InRange(envelope, 0, 1);
            }
        }
    }

    [Fact]
    public void OceanEnvelopePeaksNearTheCenter()
    {
        var center = Envelope(0.5, 0.42);
        var start = Envelope(0.02, 0.42);
        var end = Envelope(0.98, 0.42);

        Assert.True(center > 0.55);
        Assert.True(center > start);
        Assert.True(center > end);
        Assert.InRange(start, 0.01, 0.45);
        Assert.InRange(end, 0, 0.35);
    }

    [Fact]
    public void OceanEnvelopeFadesAtTheEdges()
    {
        Assert.True(Envelope(0.01, 0.42) < Envelope(0.5, 0.42));
        Assert.True(Envelope(0.99, 0.42) < Envelope(0.5, 0.42));
    }

    [Fact]
    public void OceanEnvelopeIsGentleAcrossTheWidth()
    {
        for (var i = 0; i < 50; i++)
        {
            var first = Envelope(i / 50.0, 0.42);
            var second = Envelope((i + 1) / 50.0, 0.42);
            Assert.True(Math.Abs(first - second) < 0.08);
        }
    }

    [Fact]
    public void OceanEnvelopeChangesOverTime()
    {
        var maxDifference = 0.0;
        for (var i = 0; i <= 40; i++)
        {
            var u = i / 40.0;
            var first = Envelope(u, 0);
            var second = Envelope(u, 2.0);
            maxDifference = Math.Max(maxDifference, Math.Abs(first - second));
        }

        Assert.True(maxDifference > 0.02);
    }

    [Fact]
    public void OceanEnvelopeIsSmoothOverTime()
    {
        for (var i = 0; i <= 40; i++)
        {
            var u = i / 40.0;
            var first = Envelope(u, 1.0);
            var second = Envelope(u, 1.033);

            Assert.True(Math.Abs(first - second) < 0.05);
        }
    }

    [Fact]
    public void LayerDelayShiftsTheWave()
    {
        var maxDifference = 0.0;
        for (var i = 0; i <= 40; i++)
        {
            var u = i / 40.0;
            var first = Envelope(u, 10.0);
            var second = Envelope(u, 10.0 - 1.2);
            maxDifference = Math.Max(maxDifference, Math.Abs(first - second));
        }

        Assert.True(maxDifference > 0.02);
    }

    [Fact]
    public void AmplitudeIsDeterministicForTheSameSeed()
    {
        var first = WaveformScrubber.AmplitudeAt(0.4, 18, 1234, 0.42, 0.50);
        var second = WaveformScrubber.AmplitudeAt(0.4, 18, 1234, 0.42, 0.50);

        Assert.Equal(first, second, 3);
    }

    [Fact]
    public void PhaseModulatesTheAmplitude()
    {
        var low = WaveformScrubber.AmplitudeAt(0.4, 18, 1234, 0, 0.50);
        var high = WaveformScrubber.AmplitudeAt(0.4, 18, 1234, 2.0, 0.50);

        Assert.True(Math.Abs(low - high) > 0.01);
    }

    [Fact]
    public void AmplitudeStaysInsideTheControl()
    {
        for (var i = 0; i <= 40; i++)
        {
            var amplitude = WaveformScrubber.AmplitudeAt(
                i / 40.0, 18, 1234, 0.42, 0.50);

            Assert.InRange(amplitude, 0, 18);
        }
    }

    private static double Envelope(double u, double phase, double peak = 0.50)
    {
        return WaveformScrubber.OceanEnvelopeAt(u, 1234, phase, peak);
    }
}
