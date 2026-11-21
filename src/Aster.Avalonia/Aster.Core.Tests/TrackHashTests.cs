using Aster.Core.Logic;

namespace Aster.Core.Tests;

public class TrackHashTests
{
    [Fact]
    public void MatchesWpfFnv1a()
    {
        Assert.Equal(-1662132553, TrackHash.Of("Tone|Aster|Phase 2"));
        Assert.Equal(-2128831035, TrackHash.Of(string.Empty));
    }

    [Fact]
    public void IsStableAcrossCalls()
    {
        Assert.Equal(TrackHash.Of("Aurora Nights|Capture Fixture|Capture Fixture"),
            TrackHash.Of("Aurora Nights|Capture Fixture|Capture Fixture"));
    }
}
