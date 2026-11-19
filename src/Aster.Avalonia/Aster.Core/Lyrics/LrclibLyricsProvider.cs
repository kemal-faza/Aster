using System.Net.Http.Json;
using Aster.Core.Models;

namespace Aster.Core.Lyrics;

public sealed record LyricsQuery(string Title, string Artist, string Album, int DurationSeconds);

public interface ILyricsProvider
{
    Task<LyricsDocument?> GetAsync(LyricsQuery query, CancellationToken ct);
}

public sealed class LrclibLyricsProvider : ILyricsProvider, IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;

    public LrclibLyricsProvider(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Aster/0.12.0 (Linux; Avalonia)");
        _ownsHttp = http is null;
    }

    public async Task<LyricsDocument?> GetAsync(LyricsQuery query, CancellationToken ct)
    {
        var getUrl = "https://lrclib.net/api/get" +
            $"?artist_name={Uri.EscapeDataString(query.Artist)}" +
            $"&track_name={Uri.EscapeDataString(query.Title)}" +
            $"&album_name={Uri.EscapeDataString(query.Album)}" +
            $"&duration={query.DurationSeconds}";

        using (var response = await _http.GetAsync(getUrl, ct))
        {
            if (response.IsSuccessStatusCode)
            {
                var dto = await response.Content.ReadFromJsonAsync(
                    LyricsJsonContext.Default.LrclibResponse, ct);
                var document = ToDocument(dto);
                if (document is not null)
                {
                    return document;
                }
            }
        }

        var search = string.IsNullOrWhiteSpace(query.Artist)
            ? query.Title
            : $"{query.Artist} {query.Title}";
        var searchUrl = $"https://lrclib.net/api/search?q={Uri.EscapeDataString(search)}";
        using (var response = await _http.GetAsync(searchUrl, ct))
        {
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var results = await response.Content.ReadFromJsonAsync(
                LyricsJsonContext.Default.ListLrclibResponse, ct);
            if (results is null || results.Count == 0)
            {
                return null;
            }

            var candidates = results
                .Where(r => !string.IsNullOrWhiteSpace(r.SyncedLyrics) ||
                            !string.IsNullOrWhiteSpace(r.PlainLyrics) ||
                            r.Instrumental)
                .ToList();
            if (candidates.Count == 0)
            {
                return null;
            }

            var best = candidates.OrderBy(r => Score(r, query)).First();
            return ToDocument(best);
        }
    }

    public static int Score(LrclibResponse candidate, LyricsQuery query)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(candidate.SyncedLyrics))
        {
            score -= 5;
        }

        if (!string.IsNullOrWhiteSpace(query.Title) &&
            candidate.TrackName?.Contains(query.Title, StringComparison.OrdinalIgnoreCase) == true)
        {
            score -= 20;
        }

        if (!string.IsNullOrWhiteSpace(query.Artist) &&
            candidate.ArtistName?.Contains(query.Artist, StringComparison.OrdinalIgnoreCase) == true)
        {
            score -= 20;
        }

        var responseDuration = candidate.Duration > 0
            ? candidate.Duration
            : query.DurationSeconds;
        score += (int)Math.Min(Math.Abs(responseDuration - query.DurationSeconds), 60);
        return score;
    }

    public static LyricsDocument? ToDocument(LrclibResponse? response)
    {
        if (response is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(response.SyncedLyrics))
        {
            var lines = LrcParser.Parse(response.SyncedLyrics);
            if (lines.Count > 0)
            {
                return new LyricsDocument
                {
                    IsSynced = true,
                    Lines = lines.ToList(),
                    PlainLyrics = response.PlainLyrics,
                    TrackTitle = response.TrackName,
                    ArtistName = response.ArtistName
                };
            }
        }

        if (!string.IsNullOrWhiteSpace(response.PlainLyrics))
        {
            return new LyricsDocument
            {
                IsSynced = false,
                PlainLyrics = response.PlainLyrics,
                TrackTitle = response.TrackName,
                ArtistName = response.ArtistName
            };
        }

        return response.Instrumental
            ? new LyricsDocument
            {
                IsInstrumental = true,
                TrackTitle = response.TrackName,
                ArtistName = response.ArtistName
            }
            : null;
    }

    public void Dispose()
    {
        if (_ownsHttp)
        {
            _http.Dispose();
        }
    }
}
