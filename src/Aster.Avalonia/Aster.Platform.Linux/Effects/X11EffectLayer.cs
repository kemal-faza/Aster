using Aster.Core.Interfaces;
using Aster.Core.Logic;
using Aster.Core.Models;
using Aster.UI.Abstractions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaWindow = Avalonia.Controls.Window;

namespace Aster.Platform.Linux.Effects;

public sealed class X11EffectLayer : IEffectLayer
{
    private const uint DefaultAccentArgb = 0xFFF6A6C8;

    private readonly IAppLog _log;
    private AvaloniaWindow? _window;
    private Ellipse? _accentGlow;
    private IntPtr _islandHandle;
    private uint _accentArgb = DefaultAccentArgb;

    public X11EffectLayer(IAppLog log) => _log = log;

    public bool IsAvailable => true;

    public void Attach(AvaloniaWindow island)
    {
        if (_window is not null)
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
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Content = BuildContent()
            };

            // Apply the X11 hints on Opened so both handles are mapped; the
            // empty input shape is what makes the effect window click-through.
            window.Opened += (_, _) => ApplyNativeHints(window);
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
        if (window is null)
        {
            return;
        }

        try
        {
            if (!state.Visible)
            {
                if (window.IsVisible)
                {
                    window.Hide();
                }

                return;
            }

            ApplyAccent(state.AccentArgb);

            if (!window.IsVisible)
            {
                window.Show();

                // Opened fires only for the first show. Re-apply the X11 hints
                // whenever the effect returns, or KWin drops its dock state.
                ApplyNativeHints(window);
            }

            var bounds = EffectLayerGeometry.BoundsFor(state);
            var scaling = state.Scaling > 0 ? state.Scaling : 1;
            window.Position = new PixelPoint(bounds.X, bounds.Y);
            window.Width = bounds.Width / scaling;
            window.Height = bounds.Height / scaling;
            window.Opacity = state.Minimal
                ? 0.55
                : 0.9 * Math.Clamp(state.ExpansionProgress, 0, 1);
        }
        catch (Exception ex)
        {
            _log.Error("Failed to update the X11 effect window.", ex);
        }
    }

    public void Dispose()
    {
        var window = _window;
        _window = null;
        _accentGlow = null;
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

    private void ApplyNativeHints(AvaloniaWindow window)
    {
        try
        {
            var effectHandle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (!X11EffectNative.Apply(_islandHandle, effectHandle))
            {
                _log.Warn("X11 effect-layer hints were not applied; the glow stays a normal window.");
            }
        }
        catch (Exception ex)
        {
            // Missing libXext, a destroyed display, or a closed window must not
            // take the app down; the glow simply keeps normal window semantics.
            _log.Error("Failed to apply X11 effect-layer hints.", ex);
        }
    }

    private Control BuildContent()
    {
        _accentGlow = new Ellipse
        {
            Width = 540,
            Height = 150,
            Margin = new Thickness(0, 142, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Fill = BuildGlowBrush(_accentArgb)
        };

        return new Grid
        {
            Children =
            {
                new Border
                {
                    Margin = new Thickness(120, 0, 120, 200),
                    CornerRadius = new CornerRadius(0, 0, 22, 22),
                    BoxShadow = BoxShadows.Parse("0 8 32 0 #8C000000")
                },
                _accentGlow
            }
        };
    }

    private void ApplyAccent(uint argb)
    {
        if (argb == _accentArgb || _accentGlow is null)
        {
            return;
        }

        _accentArgb = argb;
        _accentGlow.Fill = BuildGlowBrush(argb);
    }

    private static RadialGradientBrush BuildGlowBrush(uint argb)
    {
        var brush = new RadialGradientBrush();
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
