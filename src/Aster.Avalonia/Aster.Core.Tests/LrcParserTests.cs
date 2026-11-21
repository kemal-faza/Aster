using Aster.Core.Lyrics;

namespace Aster.Core.Tests;

public class LrcParserTests
{
    [Fact]
    public void ParsesTimestampsAndSorts()
    {
        var lines = LrcParser.Parse("""
            [00:12.50]Second
            [00:03.00]First
            [00:20.00]Third
            """);

        Assert.Equal(new[] { "First", "Second", "Third" }, lines.Select(l => l.Text));
        Assert.Equal(3.0, lines[0].TimeSeconds, 3);
        Assert.Equal(12.5, lines[1].TimeSeconds, 3);
    }

    [Fact]
    public void AppliesOffsetAndClampsNegative()
    {
        var lines = LrcParser.Parse("""
            [offset:-500]
            [00:00.20]Early
            """);

        Assert.Single(lines);
        Assert.Equal(0.0, lines[0].TimeSeconds, 3);
    }

    [Fact]
    public void ExpandsMultipleTimestampsPerLine()
    {
        var lines = LrcParser.Parse("[00:01.00][00:03.00]Chorus");

        Assert.Equal(2, lines.Count);
        Assert.All(lines, line => Assert.Equal("Chorus", line.Text));
    }

    [Fact]
    public void HandlesOneTwoAndThreeDigitFractions()
    {
        var lines = LrcParser.Parse("""
            [00:01.1]One
            [00:02.12]Two
            [00:03.123]Three
            """);

        Assert.Equal(1.1, lines[0].TimeSeconds, 3);
        Assert.Equal(2.12, lines[1].TimeSeconds, 3);
        Assert.Equal(3.123, lines[2].TimeSeconds, 3);
    }

    [Fact]
    public void IgnoresMetadataAndEmptyText()
    {
        var lines = LrcParser.Parse("""
            [ti:Song]
            [00:01.00]
            [00:02.00]Real
            """);

        Assert.Single(lines);
        Assert.Equal("Real", lines[0].Text);
    }
}
