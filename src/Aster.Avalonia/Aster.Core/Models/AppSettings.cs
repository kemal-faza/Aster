namespace Aster.Core.Models;

public sealed record AppSettings(
    int SchemaVersion,
    bool StartWithWindows,
    bool MinimalNotch,
    bool HideInFullscreen,
    bool GlassBackground,
    bool BouncySpring,
    bool ShowMascot,
    bool VolumeToast,
    bool BrightnessToast,
    bool LyricsEnabled,
    string LyricsHighlightColor,
    bool WaterReminderEnabled,
    int WaterIntervalMinutes,
    int WaterActiveStartHour,
    int WaterActiveEndHour,
    int WaterDailyGoal,
    int WaterGlassesToday,
    string WaterDate,
    bool CameraMirror,
    int FocusMinutes,
    int ShortBreakMinutes,
    int LongBreakMinutes,
    bool FocusChime,
    bool AudioReactiveBars = true);

public static class SettingsSchema
{
    public const int CurrentVersion = 3;
    public const string DefaultLyricsHighlight = "#7C9CFF";

    public static AppSettings Default { get; } = new(
        SchemaVersion: CurrentVersion,
        StartWithWindows: false,
        MinimalNotch: false,
        HideInFullscreen: true,
        GlassBackground: true,
        BouncySpring: true,
        ShowMascot: true,
        VolumeToast: true,
        BrightnessToast: true,
        LyricsEnabled: true,
        LyricsHighlightColor: DefaultLyricsHighlight,
        WaterReminderEnabled: false,
        WaterIntervalMinutes: 60,
        WaterActiveStartHour: 8,
        WaterActiveEndHour: 22,
        WaterDailyGoal: 8,
        WaterGlassesToday: 0,
        WaterDate: string.Empty,
        CameraMirror: true,
        FocusMinutes: 25,
        ShortBreakMinutes: 5,
        LongBreakMinutes: 15,
        FocusChime: true,
        AudioReactiveBars: true);
}
