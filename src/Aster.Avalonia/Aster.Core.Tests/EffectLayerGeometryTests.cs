using Aster.Core.Logic;
using Aster.Core.Models;
using Xunit;

namespace Aster.Core.Tests;

public class EffectLayerGeometryTests
{
    [Fact]
    public void BoundsAreCenteredAndScaled()
    {
        var state = new EffectLayerState(
            Visible: true,
            Minimal: false,
            ExpansionProgress: 1,
            Scaling: 2.0,
            IslandX: 500,
            IslandY: 0,
            IslandWidth: 1000,
            IslandHeight: 504,
            AccentArgb: 0xFFF6A6C8);

        var bounds = EffectLayerGeometry.BoundsFor(state);

        Assert.Equal(1520, bounds.Width);
        Assert.Equal(920, bounds.Height);
        Assert.Equal(240, bounds.X);
        Assert.Equal(0, bounds.Y);
    }

    [Fact]
    public void VisibilityHonorsAllGates()
    {
        var visible = new EffectLayerVisibility(
            IslandVisible: true,
            UserHidden: false,
            FullscreenHidden: false,
            SettingsOpen: false,
            GlassEnabled: true);

        Assert.True(EffectLayerGeometry.IsVisible(visible));
        Assert.False(EffectLayerGeometry.IsVisible(visible with { SettingsOpen = true }));
        Assert.False(EffectLayerGeometry.IsVisible(visible with { UserHidden = true }));
        Assert.False(EffectLayerGeometry.IsVisible(visible with { FullscreenHidden = true }));
        Assert.False(EffectLayerGeometry.IsVisible(visible with { GlassEnabled = false }));
        Assert.False(EffectLayerGeometry.IsVisible(visible with { IslandVisible = false }));
    }
}
