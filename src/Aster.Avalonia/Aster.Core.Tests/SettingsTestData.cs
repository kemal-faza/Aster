using Aster.Core.Models;

namespace Aster.Core.Tests;

internal static class SettingsTestData
{
    public static AppSettings NonDefault { get; } = SettingsSchema.Default with
    {
        StartWithWindows = true,
        MinimalNotch = true,
        HideInFullscreen = false,
        GlassBackground = false,
        BouncySpring = false,
        ShowMascot = false,
        VolumeToast = false,
        BrightnessToast = false,
        LyricsEnabled = false,
        WaterReminderEnabled = true,
        WaterIntervalMinutes = 90,
        WaterActiveStartHour = 9,
        WaterActiveEndHour = 21,
        WaterDailyGoal = 12,
        WaterGlassesToday = 3,
        WaterDate = "2026-11-17",
        CameraMirror = false,
        FocusMinutes = 30,
        ShortBreakMinutes = 7,
        LongBreakMinutes = 20,
        FocusChime = false
    };
}
