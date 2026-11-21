using System.Globalization;
using System.Text.RegularExpressions;

namespace Aster.Core.Lyrics;

public static partial class LrcParser
{
    [GeneratedRegex(@"\[(\d{1,2}):(\d{2})(?:[.:](\d{1,3}))?\]")]
    private static partial Regex TimestampRegex();

    [GeneratedRegex(@"\[offset:([+-]?\d+)\]")]
    private static partial Regex OffsetRegex();

    public static IReadOnlyList<LyricsLine> Parse(string lrc)
    {
        var lines = new List<LyricsLine>();
        var offset = TimeSpan.Zero;

        var offsetMatch = OffsetRegex().Match(lrc);
        if (offsetMatch.Success &&
            int.TryParse(offsetMatch.Groups[1].Value, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var offsetMs))
        {
            offset = TimeSpan.FromMilliseconds(offsetMs);
        }

        foreach (var raw in lrc.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var matches = TimestampRegex().Matches(raw);
            if (matches.Count == 0)
            {
                continue;
            }

            var text = TimestampRegex().Replace(raw, string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            foreach (Match match in matches)
            {
                var minutes = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                var seconds = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                var milliseconds = 0;

                if (match.Groups[3].Success)
                {
                    var fraction = match.Groups[3].Value;
                    milliseconds = fraction.Length switch
                    {
                        1 => int.Parse(fraction, CultureInfo.InvariantCulture) * 100,
                        2 => int.Parse(fraction, CultureInfo.InvariantCulture) * 10,
                        _ => int.Parse(fraction, CultureInfo.InvariantCulture)
                    };
                }

                var time = TimeSpan.FromMinutes(minutes) +
                           TimeSpan.FromSeconds(seconds) +
                           TimeSpan.FromMilliseconds(milliseconds) +
                           offset;
                if (time < TimeSpan.Zero)
                {
                    time = TimeSpan.Zero;
                }

                lines.Add(new LyricsLine
                {
                    TimeSeconds = time.TotalSeconds,
                    Text = text
                });
            }
        }

        return lines.OrderBy(line => line.TimeSeconds).ToList();
    }
}
