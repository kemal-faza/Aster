namespace Aster.Core.Models;

public readonly record struct SpectrumFrame(
    float Bass,
    float LowMid,
    float Mid,
    float HighMid,
    float Treble)
{
    public static SpectrumFrame Silence => default;

    public float Energy =>
        Math.Clamp(
            0.30f * Bass + 0.25f * LowMid + 0.20f * Mid +
            0.15f * HighMid + 0.10f * Treble,
            0f,
            1f);
}
