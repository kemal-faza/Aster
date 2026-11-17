using System.Diagnostics;
using System.Text.Json;
using Aster.Core.Interfaces;
using Aster.Core.Models;
using Aster.Core.Settings;
using Aster.Platform.Windows.Media;
using Avalonia.Media.Imaging;
using Avalonia.Skia;

SkiaPlatform.Initialize();

var allowNoSession = args.Contains("--allow-no-session");
var runControls = args.Contains("--controls");
var allowTrackChange = args.Contains("--allow-track-change");

var log = new ConsoleLog();
var dispatcher = new InlineDispatcher();
var settingsPath = Path.Combine(Path.GetTempPath(), "aster-aot-probe-settings.json");

try
{
    var store = new JsonSettingsStore(settingsPath, log);
    await store.LoadAsync();
    var settings = new AppSettings(
        SchemaVersion: SettingsSchema.CurrentVersion,
        StartWithWindows: true,
        MinimalNotch: false,
        HideInFullscreen: true,
        GlassBackground: false,
        BouncySpring: true,
        ShowMascot: true,
        VolumeToast: true,
        BrightnessToast: true,
        LyricsEnabled: true,
        LyricsHighlightColor: "#7C9CFF",
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
    await store.SaveAsync(settings);
    var reloaded = new JsonSettingsStore(settingsPath, log);
    await reloaded.LoadAsync();

    if (reloaded.Current.SchemaVersion != settings.SchemaVersion ||
        reloaded.Current.StartWithWindows != settings.StartWithWindows ||
        reloaded.Current.MinimalNotch != settings.MinimalNotch ||
        reloaded.Current.HideInFullscreen != settings.HideInFullscreen ||
        reloaded.Current.GlassBackground != settings.GlassBackground ||
        reloaded.Current.BouncySpring != settings.BouncySpring)
    {
        Console.Error.WriteLine("JSON_ROUNDTRIP_FAILED");
        return 1;
    }

    var roundTrip = JsonSerializer.Serialize(settings, SettingsJsonContext.Default.AppSettings);
    using var document = JsonDocument.Parse(roundTrip);
    if (document.RootElement.GetProperty("schemaVersion").GetInt32() != 2)
    {
        throw new InvalidOperationException("AOT settings probe did not write schemaVersion 2.");
    }
}
finally
{
    File.Delete(settingsPath);
}

var changed = 0;
using var session = new SmtcMediaSession(dispatcher, log);
session.Changed += (_, _) => Interlocked.Increment(ref changed);
session.Start();

var wait = Stopwatch.StartNew();
while (wait.Elapsed < TimeSpan.FromSeconds(8) && session.Current == MediaSnapshot.Empty)
{
    await Task.Delay(100);
}

if (session.Current == MediaSnapshot.Empty)
{
    if (!allowNoSession)
    {
        Console.Error.WriteLine("NO_SESSION");
        return 2;
    }

    Console.WriteLine("NO_SESSION_ALLOWED");
}
else
{
    var snapshot = session.Current;
    Console.WriteLine($"TITLE={snapshot.Title}");
    Console.WriteLine($"ARTIST={snapshot.Artist}");
    Console.WriteLine($"PLAYBACK={snapshot.Playback}");
    Console.WriteLine($"DURATION={snapshot.Duration}");
    Console.WriteLine($"SOURCE={snapshot.SourceApp}");
    Console.WriteLine($"ARTWORK_BYTES={snapshot.ArtworkBytes?.Length ?? 0}");

    if (snapshot.ArtworkBytes is { Length: > 0 } bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var bitmap = Bitmap.DecodeToWidth(stream, 256);
        if (bitmap.PixelSize.Width > 256)
        {
            Console.Error.WriteLine("ARTWORK_DECODE_TOO_WIDE");
            return 3;
        }
    }
}

// Decode a bundled 1x1 PNG so the AOT path is exercised even without a session.
var tinyPng = Convert.FromBase64String(
    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
using (var stream = new MemoryStream(tinyPng))
using (var bitmap = Bitmap.DecodeToWidth(stream, 1))
{
    if (bitmap.PixelSize.Width != 1)
    {
        Console.Error.WriteLine("BITMAP_DECODE_FAILED");
        return 4;
    }
}

if (runControls && session.Current != MediaSnapshot.Empty)
{
    await session.PlayPauseAsync();
    await session.PlayPauseAsync();
    await session.SeekAsync(session.Current.Position);

    if (allowTrackChange)
    {
        await session.NextAsync();
        await session.PreviousAsync();
    }
}

if (changed == 0 && session.Current != MediaSnapshot.Empty)
{
    Console.Error.WriteLine("SESSION_EVENT_NOT_OBSERVED");
    return 5;
}

Console.WriteLine("AOT_PROBE_OK");
return 0;

internal sealed class InlineDispatcher : IUiDispatcher
{
    public bool CheckAccess() => true;
    public void Post(Action action) => action();
}

internal sealed class ConsoleLog : IAppLog
{
    public void Info(string message) => Console.WriteLine("INFO " + message);
    public void Warn(string message) => Console.WriteLine("WARN " + message);
    public void Error(string message, Exception? exception = null) =>
        Console.Error.WriteLine("ERROR " + message + (exception is null ? "" : " " + exception));
}
