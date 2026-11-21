using System.Globalization;

namespace Aster.Core.Water;

public static class WaterSchedule
{
    public const int HeaderBarCount = 8;

    public static bool IsWithinActiveHours(DateTimeOffset now, int startHour, int endHour)
    {
        var start = Math.Clamp(startHour, 0, 23);
        var end = Math.Clamp(endHour, 0, 23);
        if (start == end)
        {
            return true;
        }

        return start < end
            ? now.Hour >= start && now.Hour < end
            : now.Hour >= start || now.Hour < end;
    }

    public static DateTimeOffset NextActiveStart(DateTimeOffset now, int startHour, int endHour)
    {
        var start = Math.Clamp(startHour, 0, 23);
        var end = Math.Clamp(endHour, 0, 23);
        var candidate = new DateTimeOffset(now.Date.AddHours(start), now.Offset);

        if (start == end || IsWithinActiveHours(now, start, end))
        {
            return candidate;
        }

        if (start < end)
        {
            return now.Hour >= end ? candidate.AddDays(1) : candidate;
        }

        return now.Hour >= end && now.Hour < start
            ? candidate
            : candidate.AddDays(1);
    }

    public static DateTimeOffset? ComputeNextReminder(
        DateTimeOffset now, WaterConfig config, int glassesToday)
    {
        var goal = Math.Clamp(config.Goal, 1, 20);
        if (!config.Enabled || glassesToday >= goal)
        {
            return null;
        }

        var interval = TimeSpan.FromMinutes(Math.Clamp(config.IntervalMinutes, 1, 240));
        return IsWithinActiveHours(now, config.StartHour, config.EndHour)
            ? now + interval
            : NextActiveStart(now, config.StartHour, config.EndHour) + interval;
    }

    public static int FilledBars(int glasses, int goal, int barCount = HeaderBarCount)
    {
        if (goal <= 0 || barCount <= 0)
        {
            return 0;
        }

        var ratio = Math.Clamp(glasses / (double)goal, 0, 1);
        return (int)Math.Round(ratio * barCount);
    }

    public static string TodayKey(DateTimeOffset now) =>
        now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
