using Aster.UI.Media;
using Avalonia.Media;

namespace Aster.UI.Tests;

public class ArtworkAccentTests
{
    [Fact]
    public void SaturatedPixelsProduceNonFallbackColor()
    {
        var pixels = Enumerable.Repeat(new byte[] { 0x40, 0x40, 0xF0, 0xFF }, 16)
            .SelectMany(x => x).ToArray();

        var color = ArtworkAccent.SamplePixels(pixels, 4, 4, 16);

        Assert.NotEqual(ArtworkAccent.Fallback, color);
        Assert.True(color.R > color.B);
    }

    [Fact]
    public void GrayPixelsFallBack()
    {
        var pixels = Enumerable.Repeat(new byte[] { 0x80, 0x80, 0x80, 0xFF }, 16)
            .SelectMany(x => x).ToArray();

        Assert.Equal(ArtworkAccent.Fallback, ArtworkAccent.SamplePixels(pixels, 4, 4, 16));
    }

    [Fact]
    public void EmptyBytesFallBack()
    {
        Assert.Equal(ArtworkAccent.Fallback, ArtworkAccent.Extract(null));
    }

    [Fact]
    public void PaddedStrideDoesNotSkewRows()
    {
        var pixels = new byte[3 * 24];
        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 5; x++)
            {
                var i = y * 24 + x * 4;
                pixels[i] = 0x40;
                pixels[i + 1] = 0x40;
                pixels[i + 2] = 0xF0;
                pixels[i + 3] = 0xFF;
            }
        }

        var color = ArtworkAccent.SamplePixels(pixels, 5, 3, 24);

        Assert.True(color.R > color.B);
    }

    [Fact]
    public void DecodesARealPng()
    {
        const string redPng =
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAAECAYAAACp8Z5+AAAAFUlEQVR4nGP84ODwnwEJMDGgAcICAKCkAnc6MX7LAAAAAElFTkSuQmCC";
        const string grayPng =
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAAECAYAAACp8Z5+AAAAFUlEQVR4nGNsaGj4z4AEmBjQAGEBAKQUAocB81ijAAAAAElFTkSuQmCC";

        Assert.NotEqual(ArtworkAccent.Fallback,
            ArtworkAccent.Extract(Convert.FromBase64String(redPng)));
        Assert.Equal(ArtworkAccent.Fallback,
            ArtworkAccent.Extract(Convert.FromBase64String(grayPng)));
    }
}
