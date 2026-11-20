using System.Text.Json;
using Aster.Core.Models;
using Aster.Core.Settings;
using Xunit;

namespace Aster.Core.Tests;

public class SettingsMigrationTests
{
    [Fact]
    public void V1PreservesFiveFieldsAndFillsDefaults()
    {
        const string json = """
        {
          "schemaVersion": 1,
          "startWithWindows": true,
          "minimalNotch": true,
          "hideInFullscreen": false,
          "glassBackground": false,
          "bouncySpring": false
        }
        """;

        var loaded = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.SettingsDocument);
        Assert.NotNull(loaded);

        var settings = SettingsMigrations.FromDocument(loaded!);

        Assert.Equal(3, settings.SchemaVersion);
        Assert.True(settings.StartWithWindows);
        Assert.True(settings.MinimalNotch);
        Assert.False(settings.HideInFullscreen);
        Assert.False(settings.GlassBackground);
        Assert.False(settings.BouncySpring);
        Assert.True(settings.ShowMascot);
        Assert.True(settings.VolumeToast);
        Assert.True(settings.BrightnessToast);
        Assert.True(settings.LyricsEnabled);
        Assert.Equal("#7C9CFF", settings.LyricsHighlightColor);
        Assert.False(settings.WaterReminderEnabled);
        Assert.Equal(60, settings.WaterIntervalMinutes);
        Assert.Equal(8, settings.WaterActiveStartHour);
        Assert.Equal(22, settings.WaterActiveEndHour);
        Assert.Equal(8, settings.WaterDailyGoal);
        Assert.Equal(0, settings.WaterGlassesToday);
        Assert.Equal(string.Empty, settings.WaterDate);
        Assert.True(settings.CameraMirror);
        Assert.Equal(25, settings.FocusMinutes);
        Assert.Equal(5, settings.ShortBreakMinutes);
        Assert.Equal(15, settings.LongBreakMinutes);
        Assert.True(settings.FocusChime);
        Assert.True(settings.AudioReactiveBars);
    }

    [Fact]
    public void MissingFieldsUseDefaults()
    {
        var settings = SettingsMigrations.FromDocument(new SettingsDocument());

        Assert.Equal(3, settings.SchemaVersion);
        Assert.True(settings.HideInFullscreen);
        Assert.True(settings.GlassBackground);
        Assert.True(settings.BouncySpring);
        Assert.Equal(8, settings.WaterActiveStartHour);
        Assert.Equal("#7C9CFF", settings.LyricsHighlightColor);
        Assert.True(settings.AudioReactiveBars);
    }

    [Fact]
    public void V1IgnoresV2Fields()
    {
        var settings = SettingsMigrations.FromDocument(new SettingsDocument
        {
            SchemaVersion = 1,
            LyricsHighlightColor = "#112233",
            WaterDailyGoal = 12,
            FocusMinutes = 30,
            AudioReactiveBars = false
        });

        Assert.Equal("#7C9CFF", settings.LyricsHighlightColor);
        Assert.Equal(8, settings.WaterDailyGoal);
        Assert.Equal(25, settings.FocusMinutes);
        Assert.True(settings.AudioReactiveBars);
    }

    [Fact]
    public void V2ExplicitFalseSurvives()
    {
        var settings = SettingsMigrations.FromDocument(new SettingsDocument
        {
            SchemaVersion = 2,
            AudioReactiveBars = false
        });

        Assert.Equal(3, settings.SchemaVersion);
        Assert.False(settings.AudioReactiveBars);
    }

    [Fact]
    public void InvalidValuesAreClampedOrDefaulted()
    {
        var settings = SettingsMigrations.FromDocument(new SettingsDocument
        {
            SchemaVersion = 2,
            WaterIntervalMinutes = 999,
            WaterActiveStartHour = -4,
            WaterDailyGoal = 99,
            FocusMinutes = 0,
            LyricsHighlightColor = "nope",
            WaterDate = "2026-13-99"
        });

        Assert.Equal(240, settings.WaterIntervalMinutes);
        Assert.Equal(0, settings.WaterActiveStartHour);
        Assert.Equal(20, settings.WaterDailyGoal);
        Assert.Equal(1, settings.FocusMinutes);
        Assert.Equal("#7C9CFF", settings.LyricsHighlightColor);
        Assert.Equal(string.Empty, settings.WaterDate);
    }
}
