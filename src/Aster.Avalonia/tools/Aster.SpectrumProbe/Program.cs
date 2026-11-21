using System.Diagnostics;
using System.Globalization;
using Aster.Core.Interfaces;
using Aster.Platform.Linux.Audio;

var seconds = 3.0;
string? csvPath = null;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--seconds" when i + 1 < args.Length:
            seconds = double.Parse(args[++i], CultureInfo.InvariantCulture);
            break;
        case "--csv" when i + 1 < args.Length:
            csvPath = args[++i];
            break;
    }
}

var log = new ConsoleLog();
using var source = new ParecSpectrumSource(log);
var writer = csvPath is null
    ? Console.Out
    : new StreamWriter(csvPath, false) { AutoFlush = true };
var closeWriter = csvPath is not null;

try
{
    writer.WriteLine("t_ms,available,bass,lowmid,mid,highmid,treble");
    source.Start();
    var clock = Stopwatch.StartNew();
    var anyAvailable = false;

    while (clock.Elapsed.TotalSeconds < seconds)
    {
        var frame = source.Latest;
        var available = source.IsAvailable;
        anyAvailable |= available;
        writer.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{clock.ElapsedMilliseconds},{(available ? 1 : 0)},{frame.Bass:F4},{frame.LowMid:F4},{frame.Mid:F4},{frame.HighMid:F4},{frame.Treble:F4}"));
        await Task.Delay(50);
    }

    source.Stop();
    writer.Flush();
    return anyAvailable ? 0 : 3;
}
finally
{
    if (closeWriter)
    {
        writer.Dispose();
    }
}

internal sealed class ConsoleLog : IAppLog
{
    public void Info(string message) => Console.Error.WriteLine(message);
    public void Warn(string message) => Console.Error.WriteLine(message);
    public void Error(string message, Exception? exception = null) =>
        Console.Error.WriteLine($"{message} {exception}");
}
