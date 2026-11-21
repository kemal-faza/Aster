using Aster.Core.Interfaces;
using Aster.Platform.Linux.Audio;

namespace Aster.Platform.Linux.Tests;

public class LinuxChimePlayerTests
{
    [Fact]
    public void MissingWavLogsOnceAndDoesNotThrow()
    {
        var log = new RecordingLog();
        var missing = Path.Combine(
            Path.GetTempPath(), $"aster-missing-{Guid.NewGuid():N}.wav");
        var player = new LinuxChimePlayer(log, missing);

        player.Play();
        player.Play();

        Assert.Single(log.Warnings);
    }

    private sealed class RecordingLog : IAppLog
    {
        public List<string> Warnings { get; } = new();
        public void Info(string message) { }
        public void Warn(string message) => Warnings.Add(message);
        public void Error(string message, Exception? exception = null) { }
    }
}
