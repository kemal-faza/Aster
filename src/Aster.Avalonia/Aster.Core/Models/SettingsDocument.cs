namespace Aster.Core.Models;

public sealed class SettingsDocument
{
    public int? SchemaVersion { get; set; }
    public bool? StartWithWindows { get; set; }
    public bool? MinimalNotch { get; set; }
    public bool? HideInFullscreen { get; set; }
    public bool? GlassBackground { get; set; }
    public bool? BouncySpring { get; set; }
    public bool? ShowMascot { get; set; }
    public bool? VolumeToast { get; set; }
    public bool? BrightnessToast { get; set; }
    public bool? LyricsEnabled { get; set; }
    public string? LyricsHighlightColor { get; set; }
    public bool? WaterReminderEnabled { get; set; }
    public int? WaterIntervalMinutes { get; set; }
    public int? WaterActiveStartHour { get; set; }
    public int? WaterActiveEndHour { get; set; }
    public int? WaterDailyGoal { get; set; }
    public int? WaterGlassesToday { get; set; }
    public string? WaterDate { get; set; }
    public bool? CameraMirror { get; set; }
    public int? FocusMinutes { get; set; }
    public int? ShortBreakMinutes { get; set; }
    public int? LongBreakMinutes { get; set; }
    public bool? FocusChime { get; set; }
    public bool? AudioReactiveBars { get; set; }
}
