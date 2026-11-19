using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aster.Core.Models;

namespace Aster.Core.Lyrics;

public sealed class LyricsCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromDays(30);
    private readonly string _directory;
    private readonly TimeProvider _time;

    public LyricsCache(string directory, TimeProvider time)
    {
        _directory = directory;
        _time = time;
    }

    public string DirectoryPath => _directory;

    public static string KeyFor(MediaSnapshot snapshot)
    {
        static string N(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
        var duration = (int)Math.Round(snapshot.Duration.TotalSeconds);
        return $"{N(snapshot.Title)}|{N(snapshot.Artist)}|{N(snapshot.Album)}|{duration}";
    }

    public string PathFor(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        var name = Convert.ToHexString(hash)[..24].ToLowerInvariant();
        return Path.Combine(_directory, name + ".json");
    }

    public async Task<LyricsDocument?> ReadAsync(string key, CancellationToken ct)
    {
        try
        {
            var path = PathFor(key);
            if (!File.Exists(path))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(path, ct);
            var entry = JsonSerializer.Deserialize(json, LyricsJsonContext.Default.LyricsCacheEntry);
            if (entry?.Document is null ||
                entry.SavedAt < _time.GetUtcNow().UtcDateTime.AddDays(-Ttl.TotalDays))
            {
                return null;
            }

            return entry.Document;
        }
        catch
        {
            return null;
        }
    }

    public async Task WriteAsync(string key, LyricsDocument document, CancellationToken ct)
    {
        try
        {
            Directory.CreateDirectory(_directory);
            var entry = new LyricsCacheEntry
            {
                SavedAt = _time.GetUtcNow().UtcDateTime,
                Document = document
            };
            var json = JsonSerializer.Serialize(entry, LyricsJsonContext.Default.LyricsCacheEntry);
            var path = PathFor(key);
            var temp = path + ".tmp";
            await File.WriteAllTextAsync(temp, json, ct);
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            // Cache failures must not break playback.
        }
    }

    public void Clear()
    {
        try
        {
            if (!Directory.Exists(_directory))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(_directory, "*.json"))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Ignore locked files.
                }
            }
        }
        catch
        {
            // Ignore.
        }
    }
}
