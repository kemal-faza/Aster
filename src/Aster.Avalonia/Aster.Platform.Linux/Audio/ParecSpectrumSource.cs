using System.Diagnostics;
using System.Runtime.InteropServices;
using Aster.Core.Audio;
using Aster.Core.Interfaces;
using Aster.Core.Models;

namespace Aster.Platform.Linux.Audio;

public sealed class ParecSpectrumSource : IAudioSpectrumSource
{
    private const int SampleRate = 48000;
    private readonly IAppLog _log;
    private readonly SpectrumAnalyzer _analyzer = new();
    private readonly object _gate = new();
    private readonly byte[] _pending = new byte[64 * 1024];
    private SpectrumFrame _latest;
    private bool _available;
    private bool _running;
    private bool _disposed;
    private CancellationTokenSource? _cts;
    private Task? _worker;
    private Process? _process;
    private long _lastSampleTicks;
    private int _pendingCount;
    private int _consecutiveFailures;
    private int _runId;

    public ParecSpectrumSource(IAppLog log) => _log = log;

    public bool IsAvailable
    {
        get
        {
            lock (_gate)
            {
                return _available;
            }
        }
    }

    public SpectrumFrame Latest
    {
        get
        {
            lock (_gate)
            {
                return _latest;
            }
        }
    }

    public static string MonitorForSink(string sink) =>
        sink.EndsWith(".monitor", StringComparison.Ordinal) ? sink : sink + ".monitor";

    public void Start()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ParecSpectrumSource));
            }

            if (_running)
            {
                return;
            }

            _running = true;
            _available = false;
            _latest = SpectrumFrame.Silence;
            _pendingCount = 0;
            _consecutiveFailures = 0;
            _analyzer.Reset();
            var runId = ++_runId;
            var cts = new CancellationTokenSource();
            _cts = cts;
            _worker = Task.Run(() => RunAsync(cts, runId), cts.Token);
        }
    }

    public void Stop()
    {
        CancellationTokenSource? cts;
        Process? process;

        lock (_gate)
        {
            if (!_running)
            {
                return;
            }

            _running = false;
            _available = false;
            _latest = SpectrumFrame.Silence;
            _runId++;
            cts = _cts;
            _cts = null;
            process = _process;
            _process = null;
        }

        try
        {
            cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        TryKill(process);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        var worker = _worker;
        try
        {
            worker?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }
    }

    private async Task RunAsync(CancellationTokenSource cts, int runId)
    {
        var token = cts.Token;

        try
        {
            while (!token.IsCancellationRequested)
            {
                var sink = await TryGetDefaultSinkAsync(token);
                if (string.IsNullOrWhiteSpace(sink))
                {
                    _log.Warn("Audio spectrum disabled: pactl did not report a default sink.");
                    return;
                }

                var monitor = MonitorForSink(sink);
                Process? process = null;

                try
                {
                    process = StartParec(monitor);
                    lock (_gate)
                    {
                        _process = process;
                        _lastSampleTicks = Environment.TickCount64;
                    }

                    using var monitorCts =
                        CancellationTokenSource.CreateLinkedTokenSource(token);
                    var watchdog = WatchAsync(process, monitor, monitorCts.Token);

                    await ReadAsync(process, token);

                    monitorCts.Cancel();
                    try
                    {
                        await watchdog;
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    if (_consecutiveFailures == 0)
                    {
                        _log.Warn($"Audio spectrum capture failed: {ex.Message}");
                    }
                }
                finally
                {
                    TryKill(process);
                    process?.Dispose();
                    lock (_gate)
                    {
                        _process = null;
                        _available = false;
                    }
                }

                if (token.IsCancellationRequested)
                {
                    return;
                }

                _consecutiveFailures++;
                if (_consecutiveFailures >= 3)
                {
                    _log.Warn("Audio spectrum disabled after three failed restarts.");
                    return;
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_consecutiveFailures), token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
        finally
        {
            lock (_gate)
            {
                if (_runId == runId)
                {
                    _running = false;
                    _available = false;
                    _latest = SpectrumFrame.Silence;
                    _process = null;
                }
            }

            cts.Dispose();
        }
    }

    private async Task ReadAsync(Process process, CancellationToken token)
    {
        var stream = process.StandardOutput.BaseStream;

        while (!token.IsCancellationRequested)
        {
            var read = await stream.ReadAsync(_pending.AsMemory(_pendingCount), token);
            if (read <= 0)
            {
                return;
            }

            _pendingCount += read;
            Interlocked.Exchange(ref _lastSampleTicks, Environment.TickCount64);
            var usable = _pendingCount & ~3;

            if (usable > 0)
            {
                var floats = MemoryMarshal.Cast<byte, float>(
                    _pending.AsSpan(0, usable));
                var frame = _analyzer.Process(floats);
                if (frame is { } value)
                {
                    _consecutiveFailures = 0;
                    lock (_gate)
                    {
                        _latest = value;
                        _available = true;
                    }
                }

                _pendingCount -= usable;
                if (_pendingCount > 0)
                {
                    Buffer.BlockCopy(_pending, usable, _pending, 0, _pendingCount);
                }
            }
        }
    }

    private async Task WatchAsync(
        Process process,
        string monitor,
        CancellationToken token)
    {
        var lastSinkCheck = Environment.TickCount64;

        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(250, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            var now = Environment.TickCount64;
            var last = Interlocked.Read(ref _lastSampleTicks);
            if (now - last > 500)
            {
                lock (_gate)
                {
                    _available = false;
                    _latest = SpectrumFrame.Silence;
                }
            }

            if (now - last > 5000)
            {
                TryKill(process);
                return;
            }

            if (now - lastSinkCheck > 5000)
            {
                lastSinkCheck = now;
                var current = await TryGetDefaultSinkAsync(token);
                if (!string.IsNullOrWhiteSpace(current) &&
                    !string.Equals(MonitorForSink(current), monitor, StringComparison.Ordinal))
                {
                    TryKill(process);
                    return;
                }
            }
        }
    }

    private static Process StartParec(string monitor)
    {
        var info = new ProcessStartInfo("parec")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        info.ArgumentList.Add("-d");
        info.ArgumentList.Add(monitor);
        info.ArgumentList.Add("--format=float32le");
        info.ArgumentList.Add("--rate=48000");
        info.ArgumentList.Add("--channels=1");
        info.ArgumentList.Add("--raw");
        info.ArgumentList.Add("--latency-msec=50");

        var process = Process.Start(info) ??
            throw new InvalidOperationException("parec did not start.");
        _ = DrainAsync(process.StandardError);
        return process;
    }

    private static async Task DrainAsync(StreamReader reader)
    {
        try
        {
            await reader.BaseStream.CopyToAsync(Stream.Null);
        }
        catch
        {
        }
    }

    private static async Task<string?> TryGetDefaultSinkAsync(CancellationToken token)
    {
        try
        {
            var info = new ProcessStartInfo("pactl")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            info.ArgumentList.Add("get-default-sink");

            using var process = Process.Start(info);
            if (process is null)
            {
                return null;
            }

            var text = await process.StandardOutput.ReadToEndAsync(token);
            await process.WaitForExitAsync(token);
            return process.ExitCode == 0 ? text.Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    private static void TryKill(Process? process)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }
}
