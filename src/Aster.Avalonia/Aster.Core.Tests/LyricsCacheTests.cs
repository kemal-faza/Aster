using Aster.Core.Lyrics;
using Aster.Core.Models;

namespace Aster.Core.Tests;

public class LyricsCacheTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "aster-lyrics-" + Guid.NewGuid());
    private readonly LyricsCache _cache;

    public LyricsCacheTests()
    {
        _cache = new LyricsCache(_dir, TimeProvider.System);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    [Fact]
    public async Task RoundTripsAndExpires()
    {
        var document = new LyricsDocument
        {
            IsSynced = true,
            Lines = new List<LyricsLine> { new() { TimeSeconds = 1, Text = "Line" } }
        };
        var key = LyricsCache.KeyFor(Snapshot());

        await _cache.WriteAsync(key, document, default);
        var read = await _cache.ReadAsync(key, default);

        Assert.NotNull(read);
        Assert.Equal("Line", read!.Lines[0].Text);
    }

    [Fact]
    public async Task ExpiredEntryIsIgnored()
    {
        var time = new FakeTime { Now = DateTimeOffset.UtcNow };
        var cache = new LyricsCache(_dir, time);
        var key = LyricsCache.KeyFor(Snapshot());
        await cache.WriteAsync(key, new LyricsDocument
        {
            IsSynced = true,
            Lines = new List<LyricsLine> { new() { TimeSeconds = 1, Text = "Line" } }
        }, default);

        time.Now = time.Now.AddDays(31);

        Assert.Null(await cache.ReadAsync(key, default));
    }

    [Fact]
    public async Task ClearRemovesEntries()
    {
        var key = LyricsCache.KeyFor(Snapshot());
        await _cache.WriteAsync(key, new LyricsDocument { PlainLyrics = "Line" }, default);

        _cache.Clear();

        Assert.Null(await _cache.ReadAsync(key, default));
    }

    private sealed class FakeTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task CorruptFileIsIgnored()
    {
        var key = LyricsCache.KeyFor(Snapshot());
        Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(_cache.PathFor(key), "{not json");

        Assert.Null(await _cache.ReadAsync(key, default));
    }

    [Fact]
    public void KeyIsNormalizedAndStable()
    {
        var a = LyricsCache.KeyFor(Snapshot("Song", "Artist", "Album", 180));
        var b = LyricsCache.KeyFor(Snapshot(" song ", "ARTIST", "album", 180.4));

        Assert.Equal(a, b);
    }

    private static MediaSnapshot Snapshot(
        string title = "Song", string artist = "Artist",
        string album = "Album", double duration = 180) =>
        new(title, artist, album, null, "k", PlaybackState.Playing,
            TimeSpan.Zero, TimeSpan.FromSeconds(duration), "Spotify",
            "track", true, true, true, DateTimeOffset.UtcNow);
}
