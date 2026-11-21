using Aster.Core.Logic;

namespace Aster.Core.Tests;

public class SettingsNumbersTests
{
    [Theory]
    [InlineData(null, 5, 5)]
    [InlineData("", 5, 5)]
    [InlineData("abc", 5, 5)]
    [InlineData("-3", 5, 1)]
    [InlineData("0", 5, 1)]
    [InlineData("7", 5, 7)]
    [InlineData("999", 5, 180)]
    public void ParseRangeClampsOrFallsBack(string? text, int fallback, int expected)
    {
        Assert.Equal(expected, SettingsNumbers.ParseRange(text, fallback, 1, 180));
    }
}
