using Aster.UI.Views;
using Xunit;

namespace Aster.UI.Tests;

public class EqualizerViewTests
{
    [Fact]
    public void ScaleCoversTheFullBarRange()
    {
        Assert.Equal(0.12, EqualizerView.ScaleFor(0), 3);
        Assert.Equal(1.0, EqualizerView.ScaleFor(1), 3);
        Assert.Equal(0.12, EqualizerView.ScaleFor(-1), 3);
        Assert.Equal(1.0, EqualizerView.ScaleFor(2), 3);
    }

    [Fact]
    public void ScaleWidensTheTypicalMusicRange()
    {
        var quiet = EqualizerView.ScaleFor(0.7);
        var loud = EqualizerView.ScaleFor(0.9);

        Assert.InRange(quiet, 0.57, 0.60);
        Assert.InRange(loud, 0.83, 0.86);
        Assert.True(loud - quiet > 0.25);
    }

    [Fact]
    public void ScaleIsMonotonic()
    {
        var previous = EqualizerView.ScaleFor(0);

        for (var i = 1; i <= 20; i++)
        {
            var current = EqualizerView.ScaleFor(i / 20.0);
            Assert.True(current >= previous);
            previous = current;
        }
    }

    [Fact]
    public void SpringOvershootsThenSettles()
    {
        var scale = 0.3;
        var velocity = 0.0;
        var target = 0.85;
        var peak = scale;

        for (var i = 0; i < 120; i++)
        {
            (scale, velocity) = EqualizerView.SpringStep(scale, velocity, target);
            peak = Math.Max(peak, scale);
        }

        Assert.True(peak > target + 0.2, $"peak={peak}");
        Assert.InRange(scale, target - 0.01, target + 0.01);
    }

    [Fact]
    public void SpringStaysInsideTheSafeRange()
    {
        var scale = 0.3;
        var velocity = 0.0;

        for (var i = 0; i < 120; i++)
        {
            (scale, velocity) = EqualizerView.SpringStep(scale, velocity, 1.0);
            Assert.InRange(scale, 0.05, 1.3);
        }

        for (var i = 0; i < 120; i++)
        {
            (scale, velocity) = EqualizerView.SpringStep(scale, velocity, 0.0);
            Assert.InRange(scale, 0.05, 1.3);
        }
    }
}
