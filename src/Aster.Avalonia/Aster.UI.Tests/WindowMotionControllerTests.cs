using Aster.UI.Services;

namespace Aster.UI.Tests;

public class WindowMotionControllerTests
{
    // Hover expands in both modes. Minimal notch only changes the collapsed
    // geometry; it must not disable the hover affordance.
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    public void HoverExpandsOnlyWhenCursorIsInsideAndNotSuppressed(
        bool cursorInside,
        bool hoverSuppressed,
        bool expected)
    {
        Assert.Equal(
            expected,
            WindowMotionController.ShouldExpandOnHover(cursorInside, hoverSuppressed));
    }

    [Fact]
    public void MinimalNotchStillExpandsOnHover()
    {
        // The hover predicate has no minimal-notch input; this pins the contract
        // that minimal mode keeps hover expansion.
        Assert.True(WindowMotionController.ShouldExpandOnHover(cursorInside: true, hoverSuppressed: false));
        Assert.True(WindowMotionController.ShouldExpandOnClick(minimalNotch: false));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void ClickExpandsNormalNotchButNotMinimalNotch(bool minimalNotch, bool expected)
    {
        Assert.Equal(expected, WindowMotionController.ShouldExpandOnClick(minimalNotch));
    }

    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(false, false, false, false)]
    public void CollapseOnlyStartsWhenExpandedOutsideAndNotDragging(
        bool expanded,
        bool pointerInside,
        bool dragActive,
        bool expected)
    {
        Assert.Equal(
            expected,
            WindowMotionController.ShouldStartCollapse(
                expanded, pointerInside, dragActive));
    }
}
