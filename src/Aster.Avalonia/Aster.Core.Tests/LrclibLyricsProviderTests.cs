using System.Net;
using System.Text;
using Aster.Core.Lyrics;

namespace Aster.Core.Tests;

public class LrclibLyricsProviderTests
{
    [Fact]
    public void ScorePrefersTitleArtistAndSynced()
    {
        var query = new LyricsQuery("Song", "Artist", "Album", 180);
        var exact = new LrclibResponse
        {
            TrackName = "Song", ArtistName = "Artist", Duration = 181,
            SyncedLyrics = "[00:01.00]Line"
        };
        var other = new LrclibResponse
        {
            TrackName = "Different", ArtistName = "Other", Duration = 180,
            SyncedLyrics = "[00:01.00]Line"
        };

        Assert.True(LrclibLyricsProvider.Score(exact, query) <
                    LrclibLyricsProvider.Score(other, query));
    }

    [Fact]
    public void ToDocumentPrefersSyncedThenPlainThenInstrumental()
    {
        var synced = LrclibLyricsProvider.ToDocument(new LrclibResponse
        {
            SyncedLyrics = "[00:01.00]Line", PlainLyrics = "Plain", Instrumental = true
        });
        Assert.True(synced!.IsSynced);

        var plain = LrclibLyricsProvider.ToDocument(new LrclibResponse
        {
            PlainLyrics = "Plain", Instrumental = true
        });
        Assert.False(plain!.IsSynced);
        Assert.Equal("Plain", plain.PlainLyrics);

        var instrumental = LrclibLyricsProvider.ToDocument(new LrclibResponse
        {
            Instrumental = true
        });
        Assert.True(instrumental!.IsInstrumental);
    }

    [Fact]
    public async Task SearchFallbackReturnsBestCandidate()
    {
        using var http = new HttpClient(new StubHandler(request =>
            request.RequestUri!.AbsolutePath.Contains("/api/get", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "[{\"trackName\":\"Song\",\"artistName\":\"Artist\",\"duration\":180,\"syncedLyrics\":\"[00:01.00]Line\"}]",
                        Encoding.UTF8, "application/json")
                }));

        var provider = new LrclibLyricsProvider(http);
        var document = await provider.GetAsync(
            new LyricsQuery("Song", "Artist", "Album", 180), default);

        Assert.NotNull(document);
        Assert.True(document!.IsSynced);
    }

    [Fact]
    public async Task NotFoundReturnsNull()
    {
        using var http = new HttpClient(new StubHandler(
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)));
        var provider = new LrclibLyricsProvider(http);

        Assert.Null(await provider.GetAsync(
            new LyricsQuery("Song", "Artist", "Album", 180), default));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) =>
            _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_handler(request));
    }
}
