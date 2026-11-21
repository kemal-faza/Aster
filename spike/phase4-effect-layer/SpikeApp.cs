using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;

namespace EffectLayerSpike;

public sealed class SpikeApp : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            base.OnFrameworkInitializationCompleted();
            return;
        }

        var island = new Window
        {
            Title = "Effect Spike Island",
            Width = 300,
            Height = 38,
            WindowDecorations = WindowDecorations.None,
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent },
            Background = Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            ShowActivated = false,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Content = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#F2070708")),
                CornerRadius = new CornerRadius(0, 0, 22, 22)
            }
        };

        var screen = island.Screens.Primary ?? island.Screens.All[0];

        var effect = new Window
        {
            Title = "Effect Spike Effect",
            Width = 760,
            Height = 460,
            WindowDecorations = WindowDecorations.None,
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent },
            Background = Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            ShowActivated = false,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Content = new Grid
            {
                Children =
                {
                    new Border
                    {
                        Margin = new Thickness(120, 0, 120, 200),
                        CornerRadius = new CornerRadius(0, 0, 22, 22),
                        BoxShadow = BoxShadows.Parse("0 8 32 0 #8C000000")
                    },
                    new Ellipse
                    {
                        Width = 540,
                        Height = 150,
                        Margin = new Thickness(0, 142, 0, 0),
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                        Fill = new RadialGradientBrush
                        {
                            GradientStops =
                            {
                                new GradientStop(Color.Parse("#88F6A6C8"), 0),
                                new GradientStop(Color.Parse("#00F6A6C8"), 1)
                            }
                        }
                    }
                }
            }
        };

        var scaling = screen.Scaling;
        island.Position = new PixelPoint(
            screen.Bounds.X + (screen.Bounds.Width - (int)Math.Round(island.Width * scaling)) / 2,
            screen.Bounds.Y);
        effect.Position = new PixelPoint(
            screen.Bounds.X + (screen.Bounds.Width - (int)Math.Round(effect.Width * scaling)) / 2,
            screen.Bounds.Y);

        island.Opened += (_, _) =>
        {
            effect.Opened += (_, _) =>
            {
                var islandHandle = island.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                var effectHandle = effect.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                X11Spike.Apply(islandHandle, effectHandle);
                var ready = Environment.GetEnvironmentVariable("ASTER_READY_FILE");
                if (!string.IsNullOrWhiteSpace(ready))
                {
                    File.WriteAllText(ready, "ready");
                }
            };
            effect.Show();
        };

        desktop.MainWindow = island;
        island.Show();
        base.OnFrameworkInitializationCompleted();
    }
}
