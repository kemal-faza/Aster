namespace Aster.Core.Models;

public readonly record struct EffectLayerState(
    bool Visible,
    bool Minimal,
    double ExpansionProgress,
    double Scaling,
    int IslandX,
    int IslandY,
    int IslandWidth,
    int IslandHeight,
    uint AccentArgb);

public readonly record struct EffectLayerVisibility(
    bool IslandVisible,
    bool UserHidden,
    bool FullscreenHidden,
    bool SettingsOpen,
    bool GlassEnabled);

public readonly record struct EffectLayerBounds(int X, int Y, int Width, int Height);
