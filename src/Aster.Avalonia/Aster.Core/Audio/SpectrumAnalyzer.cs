using Aster.Core.Models;

namespace Aster.Core.Audio;

/// <summary>
/// Managed mono spectrum analyzer. Input is float samples at 48 kHz. The
/// analyzer keeps a 2048-sample window and advances by 1024 samples.
/// </summary>
public sealed class SpectrumAnalyzer
{
    public const int SampleRate = 48000;
    public const int FftSize = 2048;
    public const int HopSize = 1024;

    private const double HannCoherentGain = 0.5;
    private const double FrameSeconds = (double)HopSize / SampleRate;
    private const double AttackSeconds = 0.045;
    private const double ReleaseSeconds = 0.180;
    private const double FloorDb = -70.0;
    private const double CeilingDb = -12.0;
    private const double SilenceDb = -65.0;

    private static readonly (double Low, double High)[] BandRanges =
    {
        (40, 120),
        (120, 380),
        (380, 1200),
        (1200, 4000),
        (4000, 12000)
    };

    private readonly double[] _window = new double[FftSize];
    private readonly double[] _real = new double[FftSize];
    private readonly double[] _imag = new double[FftSize];
    private readonly int[] _reverse = new int[FftSize];
    private readonly double[] _cos = new double[FftSize / 2];
    private readonly double[] _sin = new double[FftSize / 2];
    private readonly float[] _samples = new float[FftSize];
    private readonly double[] _levels = new double[BandRanges.Length];
    private int _sampleCount;

    public SpectrumAnalyzer()
    {
        for (var i = 0; i < FftSize; i++)
        {
            _window[i] = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (FftSize - 1)));
        }

        for (var i = 0; i < FftSize / 2; i++)
        {
            _cos[i] = Math.Cos(2.0 * Math.PI * i / FftSize);
            _sin[i] = Math.Sin(2.0 * Math.PI * i / FftSize);
        }

        var bits = (int)Math.Log2(FftSize);
        for (var i = 0; i < FftSize; i++)
        {
            var value = 0;
            for (var bit = 0; bit < bits; bit++)
            {
                if ((i & (1 << bit)) != 0)
                {
                    value |= 1 << (bits - 1 - bit);
                }
            }

            _reverse[i] = value;
        }
    }

    /// <summary>Clears the pending samples and smoothed levels.</summary>
    public void Reset()
    {
        _sampleCount = 0;
        Array.Clear(_levels);
    }

    /// <summary>
    /// Feeds samples. Returns the last completed frame, or null when fewer than
    /// 2048 samples have arrived since the last analysis.
    /// </summary>
    public SpectrumFrame? Process(ReadOnlySpan<float> samples)
    {
        SpectrumFrame? last = null;
        while (!samples.IsEmpty)
        {
            var copy = Math.Min(FftSize - _sampleCount, samples.Length);
            samples[..copy].CopyTo(_samples.AsSpan(_sampleCount));
            _sampleCount += copy;
            samples = samples[copy..];

            if (_sampleCount < FftSize)
            {
                continue;
            }

            last = Analyze();
            Array.Copy(_samples, HopSize, _samples, 0, FftSize - HopSize);
            _sampleCount = FftSize - HopSize;
        }

        return last;
    }

    private SpectrumFrame Analyze()
    {
        for (var i = 0; i < FftSize; i++)
        {
            _real[i] = _samples[i] * _window[i];
            _imag[i] = 0;
        }

        Transform();

        Span<double> levels = stackalloc double[BandRanges.Length];
        for (var band = 0; band < BandRanges.Length; band++)
        {
            var (low, high) = BandRanges[band];
            var first = Math.Clamp(
                (int)Math.Ceiling(low * FftSize / SampleRate), 1, FftSize / 2 - 1);
            var lastBin = Math.Clamp(
                (int)Math.Floor(high * FftSize / SampleRate), first, FftSize / 2 - 1);
            var power = 0.0;

            for (var bin = first; bin <= lastBin; bin++)
            {
                var magnitude = Math.Sqrt(
                    _real[bin] * _real[bin] + _imag[bin] * _imag[bin]);
                var amplitude = 2.0 * magnitude / (FftSize * HannCoherentGain);
                power += amplitude * amplitude / 2.0;
            }

            var rms = Math.Sqrt(power);
            var dbfs = 20.0 * Math.Log10(Math.Max(rms, 1e-9));
            var target = dbfs <= SilenceDb
                ? 0.0
                : Math.Clamp((dbfs - FloorDb) / (CeilingDb - FloorDb), 0.0, 1.0);
            var tau = target > _levels[band] ? AttackSeconds : ReleaseSeconds;
            var alpha = 1.0 - Math.Exp(-FrameSeconds / tau);
            _levels[band] += (target - _levels[band]) * alpha;
            levels[band] = Math.Clamp(_levels[band], 0.0, 1.0);
        }

        return new SpectrumFrame(
            (float)levels[0],
            (float)levels[1],
            (float)levels[2],
            (float)levels[3],
            (float)levels[4]);
    }

    private void Transform()
    {
        for (var i = 0; i < FftSize; i++)
        {
            var j = _reverse[i];
            if (j > i)
            {
                (_real[i], _real[j]) = (_real[j], _real[i]);
                (_imag[i], _imag[j]) = (_imag[j], _imag[i]);
            }
        }

        for (var size = 2; size <= FftSize; size <<= 1)
        {
            var half = size / 2;
            var step = FftSize / size;

            for (var start = 0; start < FftSize; start += size)
            {
                for (var j = 0; j < half; j++)
                {
                    var twiddle = j * step;
                    var wr = _cos[twiddle];
                    var wi = -_sin[twiddle];
                    var a = start + j;
                    var b = a + half;
                    var tr = _real[b] * wr - _imag[b] * wi;
                    var ti = _real[b] * wi + _imag[b] * wr;
                    _real[b] = _real[a] - tr;
                    _imag[b] = _imag[a] - ti;
                    _real[a] += tr;
                    _imag[a] += ti;
                }
            }
        }
    }
}
