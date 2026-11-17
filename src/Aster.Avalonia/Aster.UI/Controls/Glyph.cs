namespace Aster.UI.Controls;

public static partial class Glyph
{
    public enum Kind
    {
        Music, Camera, Shelf, Focus, Settings, ChevronUp, Previous, Next,
        Play, Pause, Lyrics, Wifi, Volume, Mute, Brightness, Charging,
        Refresh, Info, Appearance, Close
    }

    public static string Get(Kind kind) =>
        ((char)GlyphMap.Codepoints[kind]).ToString();
}
