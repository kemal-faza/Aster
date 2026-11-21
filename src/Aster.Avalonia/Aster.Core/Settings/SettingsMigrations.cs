using System.Globalization;
using Aster.Core.Models;

namespace Aster.Core.Settings;

public static class SettingsMigrations
{
    public static AppSettings FromDocument(SettingsDocument doc)
    {
        var d = SettingsSchema.Default;

        // Missing schemaVersion and version 1 are legacy documents: keep only
        // the five fields that existed in v1 and default every later field.
        if (doc.SchemaVersion is null or 1)
        {
            return d with
            {
                StartWithWindows = doc.StartWithWindows ?? d.StartWithWindows,
                MinimalNotch = doc.MinimalNotch ?? d.MinimalNotch,
                HideInFullscreen = doc.HideInFullscreen ?? d.HideInFullscreen,
                GlassBackground = doc.GlassBackground ?? d.GlassBackground,
                BouncySpring = doc.BouncySpring ?? d.BouncySpring
            };
        }

        return new AppSettings(
            SchemaVersion: SettingsSchema.CurrentVersion,
            StartWithWindows: doc.StartWithWindows ?? d.StartWithWindows,
            MinimalNotch: doc.MinimalNotch ?? d.MinimalNotch,
            HideInFullscreen: doc.HideInFullscreen ?? d.HideInFullscreen,
            GlassBackground: doc.GlassBackground ?? d.GlassBackground,
            BouncySpring: doc.BouncySpring ?? d.BouncySpring,
            ShowMascot: doc.ShowMascot ?? d.ShowMascot,
            VolumeToast: doc.VolumeToast ?? d.VolumeToast,
            BrightnessToast: doc.BrightnessToast ?? d.BrightnessToast,
            LyricsEnabled: doc.LyricsEnabled ?? d.LyricsEnabled,
            WaterReminderEnabled: doc.WaterReminderEnabled ?? d.WaterReminderEnabled,
            WaterIntervalMinutes: Math.Clamp(doc.WaterIntervalMinutes ?? d.WaterIntervalMinutes, 1, 240),
            WaterActiveStartHour: Math.Clamp(doc.WaterActiveStartHour ?? d.WaterActiveStartHour, 0, 23),
            WaterActiveEndHour: Math.Clamp(doc.WaterActiveEndHour ?? d.WaterActiveEndHour, 0, 23),
            WaterDailyGoal: Math.Clamp(doc.WaterDailyGoal ?? d.WaterDailyGoal, 1, 20),
            WaterGlassesToday: Math.Clamp(doc.WaterGlassesToday ?? d.WaterGlassesToday, 0, 99),
            WaterDate: NormalizeDate(doc.WaterDate),
            CameraMirror: doc.CameraMirror ?? d.CameraMirror,
            FocusMinutes: Math.Clamp(doc.FocusMinutes ?? d.FocusMinutes, 1, 180),
            ShortBreakMinutes: Math.Clamp(doc.ShortBreakMinutes ?? d.ShortBreakMinutes, 1, 180),
            LongBreakMinutes: Math.Clamp(doc.LongBreakMinutes ?? d.LongBreakMinutes, 1, 180),
            FocusChime: doc.FocusChime ?? d.FocusChime,
            AudioReactiveBars: doc.AudioReactiveBars ?? d.AudioReactiveBars);
    }

    private static string NormalizeDate(string? value) =>
        DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out _) ? value! : string.Empty;
}
