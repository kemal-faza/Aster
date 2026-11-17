using System.IO;
using System.Security.Cryptography;
using Aster.UI.Controls;
using SkiaSharp;
using Xunit;

namespace Aster.UI.Tests;

public class GlyphMapTests
{
    [Fact]
    public void EveryGlyphExistsInTheBundledFont()
    {
        var path = Path.Combine(
            System.AppContext.BaseDirectory,
            "Assets", "Fonts", "FluentSystemIcons-Regular.ttf");
        Assert.True(File.Exists(path), path);

        var digest = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))
            .ToLowerInvariant();
        Assert.Equal(GlyphMap.FontSha256, digest);

        using var typeface = SKTypeface.FromFile(path);
        Assert.NotNull(typeface);

        foreach (var kind in Enum.GetValues<Glyph.Kind>())
        {
            var codepoint = Glyph.Get(kind)[0];
            Assert.NotEqual(0, typeface.GetGlyph(codepoint));
        }

        var codepoints = GlyphMap.Codepoints.Values.ToList();
        Assert.Equal(codepoints.Count, codepoints.Distinct().Count());
    }
}
