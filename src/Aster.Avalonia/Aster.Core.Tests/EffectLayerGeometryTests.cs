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
    public void ContentTracksTheExpandedIsland()
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

        var content = EffectLayerGeometry.ContentFor(state);

        Assert.Equal(130.0, content.Left, 3);
        Assert.Equal(0.0, content.Top, 3);
        Assert.Equal(500.0, content.Width, 3);
        Assert.Equal(252.0, content.Height, 3);
        Assert.InRange(content.Left, 0, EffectLayerGeometry.EffectWidth - content.Width);
        Assert.InRange(
            content.Top,
            0,
            EffectLayerGeometry.EffectHeight - content.Height);
    }

    [Theory]
    [InlineData(600, 76, 230.0, 300.0, 38.0)]
    [InlineData(320, 24, 300.0, 160.0, 12.0)]
    public void ContentTracksCollapsedAndMinimalIslands(
        int islandWidth,
        int islandHeight,
        double expectedLeft,
        double expectedWidth,
        double expectedHeight)
    {
        var state = new EffectLayerState(
            Visible: true,
            Minimal: false,
            ExpansionProgress: 0,
            Scaling: 2.0,
            IslandX: 1140,
            IslandY: 0,
            IslandWidth: islandWidth,
            IslandHeight: islandHeight,
            AccentArgb: 0xFFF6A6C8);

        var content = EffectLayerGeometry.ContentFor(state);

        Assert.Equal(expectedLeft, content.Left, 3);
        Assert.Equal(expectedWidth, content.Width, 3);
        Assert.Equal(expectedHeight, content.Height, 3);
    }

    [Theory]
    [InlineData(600, 76)]
    [InlineData(800, 140)]
    [InlineData(1000, 504)]
    public void EffectWindowStaysCenteredWhileTheIslandResizes(
        int islandWidth,
        int islandHeight)
    {
        var screenWidth = 5120;
        var islandX = (screenWidth - islandWidth) / 2;
        var state = new EffectLayerState(
            Visible: true,
            Minimal: false,
            ExpansionProgress: 1,
            Scaling: 2.0,
            IslandX: islandX,
            IslandY: 0,
            IslandWidth: islandWidth,
            IslandHeight: islandHeight,
            AccentArgb: 0xFFF6A6C8);

        var bounds = EffectLayerGeometry.BoundsFor(state);
        var content = EffectLayerGeometry.ContentFor(state);

        Assert.Equal((screenWidth - bounds.Width) / 2, bounds.X);
        Assert.Equal(state.IslandX, bounds.X + content.Left * state.Scaling, 3);
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
