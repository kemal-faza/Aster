using Aster.Core.Audio;
using Xunit;

namespace Aster.Core.Tests;

public class SpectrumAnalyzerTests
{
    private const int Rate = SpectrumAnalyzer.SampleRate;

    private static float[] Tone(double hz, double amplitude, double seconds = 1)
    {
        var samples = new float[(int)(Rate * seconds)];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (float)(amplitude * Math.Sin(2 * Math.PI * hz * i / Rate));
        }

        return samples;
    }

    [Fact]
    public void MidToneDominatesMidBand()
    {
        var analyzer = new SpectrumAnalyzer();
        var frame = analyzer.Process(Tone(440, 0.141421));

        Assert.NotNull(frame);
        var value = frame!.Value;
        Assert.True(value.Mid > 0.5f, $"mid={value.Mid}");
        Assert.True(value.Mid > value.Bass, $"mid={value.Mid} bass={value.Bass}");
        Assert.True(value.Mid > value.Treble, $"mid={value.Mid} treble={value.Treble}");
    }

    [Fact]
    public void FullScaleToneMapsHigh()
    {
        var analyzer = new SpectrumAnalyzer();
        var frame = analyzer.Process(Tone(1000, 1.0));

        Assert.NotNull(frame);
        Assert.InRange(frame!.Value.Mid, 0.9f, 1.0f);
    }

    [Fact]
    public void MinusTwentyDbfsToneMapsToTheFixture()
    {
        // Amplitude 0.141421 is RMS 0.1, i.e. -20 dBFS.
        var analyzer = new SpectrumAnalyzer();
        var frame = analyzer.Process(Tone(1000, 0.141421));

        Assert.NotNull(frame);
        Assert.InRange(frame!.Value.Mid, 0.80f, 0.92f);
    }

    [Fact]
    public void TrebleToneDominatesTheTopBands()
    {
        var analyzer = new SpectrumAnalyzer();
        var frame = analyzer.Process(Tone(5000, 0.5));

        Assert.NotNull(frame);
        var value = frame!.Value;
        Assert.True(value.Treble > value.Bass, $"treble={value.Treble} bass={value.Bass}");
        Assert.True(value.Treble > value.HighMid, $"treble={value.Treble} high-mid={value.HighMid}");
    }

    [Fact]
    public void MidToneLeavesLowMidLow()
    {
        var analyzer = new SpectrumAnalyzer();
        var frame = analyzer.Process(Tone(440, 0.5));

        Assert.NotNull(frame);
        Assert.True(frame!.Value.Mid > frame.Value.LowMid);
    }

    [Fact]
    public void AttackRisesFasterThanReleaseFalls()
    {
        var analyzer = new SpectrumAnalyzer();
        var shortTone = analyzer.Process(Tone(440, 0.5, seconds: 0.1));
        Assert.NotNull(shortTone);
        var attacked = shortTone!.Value.Mid;

        analyzer.Reset();
        var settledFrame = analyzer.Process(Tone(440, 0.5, seconds: 1));
        Assert.NotNull(settledFrame);
        var settled = settledFrame!.Value.Mid;

        var fallSamples = new float[SpectrumAnalyzer.HopSize * 8];
        var fallFrame = analyzer.Process(fallSamples);
        Assert.NotNull(fallFrame);
        var afterFall = fallFrame!.Value.Mid;

        Assert.True(settled > 0);
        Assert.True(
            attacked / settled > afterFall / settled,
            $"rise={attacked / settled} fall={afterFall / settled}");
    }

    [Fact]
    public void SilenceIsZero()
    {
        var analyzer = new SpectrumAnalyzer();
        var frame = analyzer.Process(new float[Rate]);

        Assert.NotNull(frame);
        Assert.Equal(0f, frame!.Value.Bass);
        Assert.Equal(0f, frame.Value.LowMid);
        Assert.Equal(0f, frame.Value.Mid);
        Assert.Equal(0f, frame.Value.HighMid);
        Assert.Equal(0f, frame.Value.Treble);
    }

    [Fact]
    public void DcOffsetDoesNotPegBands()
    {
        var analyzer = new SpectrumAnalyzer();
        var samples = new float[Rate];
        Array.Fill(samples, 0.5f);

        var frame = analyzer.Process(samples);

        Assert.NotNull(frame);
        Assert.True(frame!.Value.Energy < 0.1f, $"energy={frame.Value.Energy}");
    }

    [Fact]
    public void ReleaseFallsAfterToneStops()
    {
        var analyzer = new SpectrumAnalyzer();
        var loud = analyzer.Process(Tone(440, 0.5));
        Assert.NotNull(loud);
        var before = loud!.Value.Mid;

        var quiet = analyzer.Process(new float[Rate]);

        Assert.NotNull(quiet);
        Assert.True(quiet!.Value.Mid < before, $"before={before} after={quiet.Value.Mid}");
    }

    [Fact]
    public void ResetClearsThePendingWindow()
    {
        var analyzer = new SpectrumAnalyzer();
        analyzer.Process(Tone(440, 0.5));
        analyzer.Reset();

        var frame = analyzer.Process(new float[Rate]);

        Assert.NotNull(frame);
        Assert.Equal(0f, frame!.Value.Mid);
    }
}
