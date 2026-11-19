using System.Text.Json.Serialization;

namespace Aster.Core.Lyrics;

public sealed class LyricsLine
{
    public double TimeSeconds { get; set; }
    public string Text { get; set; } = string.Empty;
    [JsonIgnore] public TimeSpan Time => TimeSpan.FromSeconds(TimeSeconds);
}

public sealed class LyricsDocument
{
    public bool IsSynced { get; set; }
    public bool IsInstrumental { get; set; }
    public string? PlainLyrics { get; set; }
    public List<LyricsLine> Lines { get; set; } = new();
    public string? TrackTitle { get; set; }
    public string? ArtistName { get; set; }
    public string Source { get; set; } = "LRCLIB";
}
