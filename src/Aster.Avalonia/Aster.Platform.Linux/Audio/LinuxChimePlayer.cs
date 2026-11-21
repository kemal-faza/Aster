using System.Diagnostics;
using Aster.Core.Interfaces;
using Aster.UI.Abstractions;

namespace Aster.Platform.Linux.Audio;

public sealed class LinuxChimePlayer : IChimePlayer
{
    private readonly IAppLog _log;
    private readonly string _wavPath;
    private bool _unavailable;

    public LinuxChimePlayer(IAppLog log, string? wavPath = null)
    {
        _log = log;
        _wavPath = wavPath ?? Path.Combine(
            AppContext.BaseDirectory, "Assets", "focus-chime.wav");
    }

    public void Play()
    {
        if (_unavailable)
        {
            return;
        }

        if (!File.Exists(_wavPath))
        {
            _unavailable = true;
            _log.Warn($"Focus chime asset is missing: {_wavPath}");
            return;
        }

        if (TryStart("paplay", _wavPath))
        {
            return;
        }

        if (TryStart("canberra-gtk-play", "--file=" + _wavPath))
        {
            return;
        }

        _unavailable = true;
        _log.Warn("Focus chime unavailable: install paplay or canberra-gtk-play.");
    }

    private bool TryStart(string executable, string argument)
    {
        try
        {
            var startInfo = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(argument);

            var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            _ = DrainAsync(process);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task DrainAsync(Process process)
    {
        try
        {
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
            {
                _log.Warn($"Focus chime exited with {process.ExitCode}.");
            }
        }
        catch (Exception)
        {
            // A missing or killed player is already handled by the fallback.
        }
        finally
        {
            process.Dispose();
        }
    }
}
