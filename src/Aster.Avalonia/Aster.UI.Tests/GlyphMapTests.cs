using System.IO;
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

        using var typeface = SKTypeface.FromFile(path);
        Assert.NotNull(typeface);

        foreach (var kind in Enum.GetValues<Glyph.Kind>())
        {
            var codepoint = Glyph.Get(kind)[0];
            Assert.NotEqual(0, typeface.GetGlyph(codepoint));
        }
    }
}
