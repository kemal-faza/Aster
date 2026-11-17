using System.Globalization;

namespace Aster.Core.Logic;

public static class ColorHex
{
    public static string? Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var value = text.Trim();
        if (value.StartsWith('#'))
        {
            value = value[1..];
        }

        return value.Length == 6 &&
               int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _)
            ? $"#{value.ToUpperInvariant()}"
            : null;
    }
}
