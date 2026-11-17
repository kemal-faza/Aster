using System.Text.Json;
using Aster.Core.Models;
using Aster.Core.Settings;

namespace Aster.Core.Tests;

public class SettingsSchemaTests
{
    [Fact]
    public void DefaultsMatchL2Contract()
    {
        var settings = SettingsSchema.Default;

        Assert.Equal(2, settings.SchemaVersion);
        Assert.False(settings.StartWithWindows);
        Assert.False(settings.MinimalNotch);
        Assert.True(settings.HideInFullscreen);
        Assert.True(settings.GlassBackground);
        Assert.True(settings.BouncySpring);
        Assert.True(settings.ShowMascot);
        Assert.Equal("#7C9CFF", settings.LyricsHighlightColor);
    }

    [Fact]
    public void JsonRoundTripPreservesValues()
    {
        var original = new AppSettings(
            SchemaVersion: SettingsSchema.CurrentVersion,
            StartWithWindows: true,
            MinimalNotch: true,
            HideInFullscreen: false,
            GlassBackground: false,
            BouncySpring: false,
            ShowMascot: true,
            VolumeToast: true,
            BrightnessToast: true,
            LyricsEnabled: true,
            LyricsHighlightColor: SettingsSchema.DefaultLyricsHighlight,
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
            FocusChime: true);
        var json = JsonSerializer.Serialize(original, SettingsJsonContext.Default.AppSettings);
        var restored = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings);

        Assert.Equal(original, restored);
        Assert.Contains("\"schemaVersion\": 2", json);
    }
}
