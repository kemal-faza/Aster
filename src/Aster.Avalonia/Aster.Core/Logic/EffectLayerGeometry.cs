using Aster.Core.Models;

namespace Aster.Core.Logic;

public static class EffectLayerGeometry
{
    public const double EffectWidth = 760;
    public const double EffectHeight = 460;

    public static EffectLayerBounds BoundsFor(EffectLayerState state)
    {
        var width = (int)Math.Round(EffectWidth * state.Scaling);
        var height = (int)Math.Round(EffectHeight * state.Scaling);
        var x = state.IslandX + (state.IslandWidth - width) / 2;
        return new EffectLayerBounds(x, state.IslandY, width, height);
    }

    /// <summary>
    /// The island rectangle inside the fixed effect window. The shadow carrier
    /// and the accent glow must use this rect so they stay attached to the
    /// island while it expands, collapses, or switches to the minimal notch.
    /// </summary>
    public static EffectLayerContent ContentFor(EffectLayerState state)
    {
        var scaling = state.Scaling > 0 ? state.Scaling : 1;
        var width = Math.Clamp(state.IslandWidth / scaling, 1, EffectWidth);
        var height = Math.Clamp(state.IslandHeight / scaling, 1, EffectHeight);
        return new EffectLayerContent((EffectWidth - width) / 2, 0, width, height);
    }

    public static bool IsVisible(EffectLayerVisibility visibility) =>
        visibility.IslandVisible &&
        !visibility.UserHidden &&
        !visibility.FullscreenHidden &&
        !visibility.SettingsOpen &&
        visibility.GlassEnabled;
}

public readonly record struct EffectLayerContent(
    double Left,
    double Top,
    double Width,
    double Height);
