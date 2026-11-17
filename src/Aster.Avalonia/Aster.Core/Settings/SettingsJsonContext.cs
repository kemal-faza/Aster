using System.Text.Json.Serialization;
using Aster.Core.Models;

namespace Aster.Core.Settings;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(SettingsDocument))]
public partial class SettingsJsonContext : JsonSerializerContext
{
}
