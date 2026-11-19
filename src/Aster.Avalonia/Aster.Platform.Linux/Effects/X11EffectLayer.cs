using Aster.Core.Interfaces;
using Aster.Core.Logic;
using Aster.Core.Models;
using Aster.UI.Abstractions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaWindow = Avalonia.Controls.Window;

namespace Aster.Platform.Linux.Effects;

public sealed class X11EffectLayer : IEffectLayer
{
    private const uint DefaultAccentArgb = 0xFFF6A6C8;

    private readonly IAppLog _log;
    private AvaloniaWindow? _window;
    private Border? _shadowCarrier;
    private Border? _accentGlow;
    private IntPtr _islandHandle;
    private uint _accentArgb = DefaultAccentArgb;
    private bool _disabled;
    private bool _visible;

    public X11EffectLayer(IAppLog log) => _log = log;

    public bool IsAvailable => !_disabled;

    public void Attach(AvaloniaWindow island)
    {
        if (_window is not null || _disabled)
        {
            return;
        }

        try
        {
            _islandHandle = island.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            var window = new AvaloniaWindow
            {
                Title = "Aster Effect Layer",
                Width = EffectLayerGeometry.EffectWidth,
                Height = EffectLayerGeometry.EffectHeight,
                WindowDecorations = WindowDecorations.None,
                TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent },
                Background = Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false,
                ShowActivated = false,
                Opacity = 0,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Content = BuildContent()
            };

            // Apply the X11 hints on Opened so both handles are mapped; the
            // empty input shape is what makes the effect window click-through.
            // If the platform cannot provide transparency or the shape fails,
            // hide the window instead of leaving an interactive overlay.
            window.Opened += (_, _) =>
            {
                VerifyAndApply(window);
                if (!_visible)
                {
                    window.Hide();
                }
            };
            _window = window;
            // Show(owner) sets Owner; the setter itself is not public.
            window.Show(island);
        }
        catch (Exception ex)
        {
            _log.Error("Failed to create the X11 effect window.", ex);
            Dispose();
        }
    }

    public void Update(EffectLayerState state)
    {
        var window = _window;
        if (window is null || _disabled)
        {
            return;
        }

        try
        {
            // The effect layer stays mapped in the collapsed state so the
            // panel keeps a neutral shadow. Only the accent glow is gated by
            // expansion; otherwise a collapsed pill would get a pink halo.
            var shouldShow = state.Visible;
            if (!shouldShow)
            {
                _visible = false;
                // Attach maps the effect window before the first hidden-state
                // update, and the pending Show may finish after this update.
                // Opened re-checks _visible and hides it again.
                window.Hide();
                return;
            }

            _visible = true;
            ApplyAccent(state.AccentArgb);
            UpdateContent(EffectLayerGeometry.ContentFor(state));
            if (_accentGlow is not null)
            {
                _accentGlow.Opacity = Math.Clamp(state.ExpansionProgress, 0, 1);
            }

            if (!window.IsVisible)
            {
                window.Show();

                // Opened fires only for the first show. Re-apply the X11 hints
                // whenever the effect returns, or KWin drops its dock state.
                VerifyAndApply(window);
                if (_disabled)
                {
                    return;
                }
            }

            var bounds = EffectLayerGeometry.BoundsFor(state);
            var scaling = state.Scaling > 0 ? state.Scaling : 1;
            window.Position = new PixelPoint(bounds.X, bounds.Y);
            window.Width = bounds.Width / scaling;
            window.Height = bounds.Height / scaling;
            window.Opacity = state.Minimal ? 0.55 : 0.9;
        }
        catch (Exception ex)
        {
            _log.Error("Failed to update the X11 effect window.", ex);
            Disable("The X11 effect window failed during an update.");
        }
    }

    public void Dispose()
    {
        var window = _window;
        _window = null;
        _shadowCarrier = null;
        _accentGlow = null;
        _visible = false;
        if (window is null)
        {
            return;
        }

        try
        {
            window.Close();
        }
        catch (Exception ex)
        {
            _log.Warn($"Failed to close the X11 effect window: {ex.Message}");
        }
    }

    private void VerifyAndApply(AvaloniaWindow window)
    {
        if (_disabled)
        {
            return;
        }

        if (window.ActualTransparencyLevel == WindowTransparencyLevel.None)
        {
            Disable("The X11 effect window did not receive a transparent visual.");
            return;
        }

        try
        {
            var effectHandle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (!X11EffectNative.Apply(_islandHandle, effectHandle))
            {
                Disable("The X11 effect-layer hints could not be applied.");
            }
        }
        catch (Exception ex)
        {
            // Missing libXext, a destroyed display, or a closed window must not
            // leave an interactive top-level overlay behind.
            _log.Error("Failed to apply X11 effect-layer hints.", ex);
            Disable("The X11 effect-layer hints threw; disabling the effect window.");
        }
    }

    private void Disable(string reason)
    {
        if (_disabled)
        {
            return;
        }

        _disabled = true;
        _log.Warn(reason);
        var window = _window;
        _window = null;
        _shadowCarrier = null;
        _accentGlow = null;
        _visible = false;
        if (window is null)
        {
            return;
        }

        try
        {
            window.Hide();
            window.Close();
        }
        catch (Exception ex)
        {
            _log.Warn($"Failed to close the disabled effect window: {ex.Message}");
        }
    }

    private Control BuildContent()
    {
        _shadowCarrier = new Border
        {
            CornerRadius = new CornerRadius(0, 0, 22, 22),
            BoxShadow = BoxShadows.Parse("0 8 32 0 #8C000000"),
            IsHitTestVisible = false
        };

        // A Border background is clipped by CornerRadius, so the glow can never
        // leak past the island shape even while the island is animating.
        _accentGlow = new Border
        {
            CornerRadius = new CornerRadius(0, 0, 22, 22),
            Background = BuildGlowBrush(_accentArgb),
            IsHitTestVisible = false
        };

        return new Grid
        {
            Children =
            {
                _shadowCarrier,
                _accentGlow
            }
        };
    }

    private void UpdateContent(EffectLayerContent content)
    {
        if (_shadowCarrier is null || _accentGlow is null)
        {
            return;
        }

        var right = Math.Max(
            0,
            EffectLayerGeometry.EffectWidth - content.Left - content.Width);
        var bottom = Math.Max(
            0,
            EffectLayerGeometry.EffectHeight - content.Top - content.Height);
        var margin = new Thickness(content.Left, content.Top, right, bottom);
        _shadowCarrier.Margin = margin;
        _accentGlow.Margin = margin;
    }

    private void ApplyAccent(uint argb)
    {
        if (argb == _accentArgb || _accentGlow is null)
        {
            return;
        }

        _accentArgb = argb;
        _accentGlow.Background = BuildGlowBrush(argb);
    }

    private static RadialGradientBrush BuildGlowBrush(uint argb)
    {
        var brush = new RadialGradientBrush
        {
            GradientOrigin = new RelativePoint(0.5, 0.86, RelativeUnit.Relative),
            Center = new RelativePoint(0.5, 0.86, RelativeUnit.Relative),
            RadiusX = new RelativeScalar(1.05, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(0.3, RelativeUnit.Relative)
        };
        brush.GradientStops.Add(new GradientStop(WithAlpha(argb, 0x88), 0));
        brush.GradientStops.Add(new GradientStop(WithAlpha(argb, 0x00), 1));
        return brush;
    }

    private static Color WithAlpha(uint argb, byte alpha) => Color.FromArgb(
        alpha,
        (byte)((argb >> 16) & 0xFF),
        (byte)((argb >> 8) & 0xFF),
        (byte)(argb & 0xFF));
}
