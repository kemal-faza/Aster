using System.Text.Json.Serialization;

namespace Aster.Core.Lyrics;

public sealed class LyricsCacheEntry
{
    public DateTime SavedAt { get; set; }
    public LyricsDocument? Document { get; set; }
}

public sealed class LrclibResponse
{
    [JsonPropertyName("trackName")] public string? TrackName { get; set; }
    [JsonPropertyName("artistName")] public string? ArtistName { get; set; }
    [JsonPropertyName("albumName")] public string? AlbumName { get; set; }
    [JsonPropertyName("duration")] public double Duration { get; set; }
    [JsonPropertyName("instrumental")] public bool Instrumental { get; set; }
    [JsonPropertyName("plainLyrics")] public string? PlainLyrics { get; set; }
    [JsonPropertyName("syncedLyrics")] public string? SyncedLyrics { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(LyricsCacheEntry))]
[JsonSerializable(typeof(LrclibResponse))]
[JsonSerializable(typeof(List<LrclibResponse>))]
public partial class LyricsJsonContext : JsonSerializerContext
{
}
