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

    public static bool IsVisible(EffectLayerVisibility visibility) =>
        visibility.IslandVisible &&
        !visibility.UserHidden &&
        !visibility.FullscreenHidden &&
        !visibility.SettingsOpen &&
        visibility.GlassEnabled;
}
