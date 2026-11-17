using System.IO;
using System.Security.Cryptography;
using Aster.UI.Controls;
using SkiaSharp;
using Xunit;

namespace Aster.UI.Tests;

public class GlyphMapTests
{
    [Fact]
    public void BundledFontMatchesCommittedDigest()
    {
        var digest = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(FontPath)))
            .ToLowerInvariant();

        Assert.Equal(GlyphMap.FontSha256, digest);
    }

    [Fact]
    public void EveryGlyphExistsInTheBundledFont()
    {
        Assert.True(File.Exists(FontPath), FontPath);

        using var typeface = SKTypeface.FromFile(FontPath);
        Assert.NotNull(typeface);

        foreach (var kind in Enum.GetValues<Glyph.Kind>())
        {
            Assert.True(GlyphMap.Codepoints.ContainsKey(kind), kind.ToString());
            var codepoint = Glyph.Get(kind)[0];
            Assert.NotEqual(0, typeface.GetGlyph(codepoint));
        }
    }

    [Fact]
    public void GlyphCodepointsAreUnique()
    {
        var codepoints = GlyphMap.Codepoints.Values.ToList();

        Assert.Equal(codepoints.Count, codepoints.Distinct().Count());
    }

    [Fact]
    public void EnumNamesAndCodepointsHaveNoDuplicatesOrAliases()
    {
        var names = Enum.GetNames<Glyph.Kind>();
        var values = Enum.GetValues<Glyph.Kind>();

        // A duplicate name is impossible in C#, but a duplicate numeric value
        // (an alias) would silently share one codepoint.
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(values.Length, values.Select(value => (int)value).Distinct().Count());
        Assert.Equal(names.Length, GlyphMap.Codepoints.Count);

        foreach (var name in names)
        {
            var kind = Enum.Parse<Glyph.Kind>(name);
            Assert.True(GlyphMap.Codepoints.ContainsKey(kind), name);
        }
    }

    private static string FontPath => Path.Combine(
        System.AppContext.BaseDirectory,
        "Assets", "Fonts", "FluentSystemIcons-Regular.ttf");
}
