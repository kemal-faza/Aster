using Aster.Core.Models;
using Xunit;

namespace Aster.Core.Tests;

public class SpectrumFrameTests
{
    [Fact]
    public void EnergyUsesTheDocumentedWeights()
    {
        var frame = new SpectrumFrame(1f, 0f, 0f, 0f, 0f);
        Assert.Equal(0.30, frame.Energy, 3);

        frame = new SpectrumFrame(0f, 1f, 0f, 0f, 0f);
        Assert.Equal(0.25, frame.Energy, 3);

        frame = new SpectrumFrame(0f, 0f, 1f, 0f, 0f);
        Assert.Equal(0.20, frame.Energy, 3);

        frame = new SpectrumFrame(0f, 0f, 0f, 1f, 0f);
        Assert.Equal(0.15, frame.Energy, 3);

        frame = new SpectrumFrame(0f, 0f, 0f, 0f, 1f);
        Assert.Equal(0.10, frame.Energy, 3);
    }

    [Fact]
    public void SilenceHasZeroEnergy()
    {
        Assert.Equal(0f, SpectrumFrame.Silence.Energy);
        Assert.Equal(SpectrumFrame.Silence, default(SpectrumFrame));
    }
}
