using Aster.Core.Models;

namespace Aster.Core.Logic;

public static class SimulatedSpectrum
{
    private static readonly double[] Rates = { 2.1, 2.9, 3.7, 3.1, 4.3 };

    public static SpectrumFrame Next(double positionSeconds, int seed)
    {
        var position = Math.Max(0, positionSeconds);
        var levels = new float[5];

        for (var i = 0; i < levels.Length; i++)
        {
            var phase = i * 0.9 + (seed % 97) * 0.37;
            var wave = 0.5 + 0.5 * Math.Sin(position * Rates[i] + phase);
            var envelope = 0.55 + 0.45 * Math.Sin(position * 0.4 + i * 0.7);
            levels[i] = (float)Math.Clamp(0.25 + 0.75 * wave * envelope, 0, 1);
        }

        return new SpectrumFrame(
            levels[0], levels[1], levels[2], levels[3], levels[4]);
    }
}
