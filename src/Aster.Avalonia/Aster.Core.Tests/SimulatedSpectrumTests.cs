using Aster.Core.Logic;
using Xunit;

namespace Aster.Core.Tests;

public class SimulatedSpectrumTests
{
    [Fact]
    public void IsDeterministicForTheSamePositionAndSeed()
    {
        var first = SimulatedSpectrum.Next(12.5, 1234);
        var second = SimulatedSpectrum.Next(12.5, 1234);

        Assert.Equal(first, second);
    }

    [Fact]
    public void LevelsStayInRangeAndChangeOverTime()
    {
        var first = SimulatedSpectrum.Next(1.0, 7);
        var second = SimulatedSpectrum.Next(2.0, 7);

        Assert.InRange(first.Bass, 0f, 1f);
        Assert.InRange(first.Treble, 0f, 1f);
        Assert.NotEqual(first, second);
    }
}
