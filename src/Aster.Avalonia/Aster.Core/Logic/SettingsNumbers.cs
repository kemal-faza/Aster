using System.Globalization;

namespace Aster.Core.Logic;

public static class SettingsNumbers
{
    public static int ParseRange(string? text, int fallback, int min, int max)
    {
        if (!int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var value))
        {
            return fallback;
        }

        return Math.Clamp(value, min, max);
    }
}
