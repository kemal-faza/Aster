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
    public void SchemaHasTwentyTwoUserFields()
    {
        var userFields = typeof(AppSettings).GetProperties().Length - 1;

        Assert.Equal(22, userFields);
    }

    [Fact]
    public void JsonRoundTripPreservesValues()
    {
        var original = SettingsTestData.NonDefault;
        var json = JsonSerializer.Serialize(original, SettingsJsonContext.Default.AppSettings);
        var restored = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings);

        Assert.Equal(original, restored);
        Assert.Contains("\"schemaVersion\": 2", json);
    }
}
