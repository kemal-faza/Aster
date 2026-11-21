using Aster.Core.Interfaces;
using Aster.Platform.Linux.Audio;
using Xunit;

namespace Aster.Platform.Linux.Tests;

public class ParecSpectrumSourceTests
{
    [Fact]
    public void MonitorNameAppendsThePulseSuffix()
    {
        Assert.Equal(
            "alsa_output.pci-0000_00_1f.3.HiFi__Headphones__sink.monitor",
            ParecSpectrumSource.MonitorForSink(
                "alsa_output.pci-0000_00_1f.3.HiFi__Headphones__sink"));
    }

    [Fact]
    public void MonitorNameRejectsAnExistingMonitorSuffix()
    {
        Assert.Equal(
            "speaker.monitor",
            ParecSpectrumSource.MonitorForSink("speaker.monitor"));
    }

    [Fact]
    public void StartAfterDisposeThrows()
    {
        var source = new ParecSpectrumSource(new NullLog());
        source.Dispose();

        Assert.Throws<ObjectDisposedException>(() => source.Start());
    }

    [Fact]
    public void DisposeIsIdempotent()
    {
        var source = new ParecSpectrumSource(new NullLog());

        source.Dispose();
        source.Dispose();
    }

    private sealed class NullLog : IAppLog
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }
}
