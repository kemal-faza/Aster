namespace Aster.Platform.Linux.Media;

public static class LinuxArtworkReader
{
    private const int MaxBytes = 5 * 1024 * 1024;

    public static async Task<byte[]?> ReadAsync(string? url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        try
        {
            if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var comma = url.IndexOf(',');
                if (comma < 0)
                {
                    return null;
                }

                var bytes = Convert.FromBase64String(url[(comma + 1)..]);
                return bytes.Length <= MaxBytes ? bytes : null;
            }

            if (url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                var path = new Uri(url).LocalPath;
                return File.Exists(path) ? await File.ReadAllBytesAsync(path, ct) : null;
            }

            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                using var response = await http.GetAsync(
                    url, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                if (response.Content.Headers.ContentLength is > MaxBytes)
                {
                    return null;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var buffer = new MemoryStream();
                var chunk = new byte[8192];
                int read;
                while ((read = await stream.ReadAsync(chunk, ct)) > 0)
                {
                    if (buffer.Length + read > MaxBytes)
                    {
                        return null;
                    }

                    buffer.Write(chunk, 0, read);
                }

                return buffer.ToArray();
            }
        }
        catch
        {
            return null;
        }

        return null;
    }
}
