using System.Diagnostics;
using Aster.Core.Interfaces;
using Aster.Core.Logic;
using Aster.UI.Abstractions;
using Aster.UI.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace Aster.UI.Services;

public sealed class WindowMotionController : IDisposable
{
    private readonly Window _window;
    private readonly ShellViewModel _state;
    private readonly IWindowPlacement _placement;
    private readonly ISettingsStore _settings;
    private readonly IAppLog _log;
    private IntPtr _hwnd;
    private readonly DispatcherTimer _hoverTimer = new() { Interval = TimeSpan.FromMilliseconds(90) };
    private readonly DispatcherTimer _collapseTimer = new() { Interval = TimeSpan.FromMilliseconds(320) };

    private SpringState _widthSpring = new() { Value = WindowGeometry.CollapsedWidth };
    private SpringState _heightSpring = new() { Value = WindowGeometry.CollapsedHeight };
    private double _targetWidth = WindowGeometry.CollapsedWidth;
    private double _targetHeight = WindowGeometry.CollapsedHeight;
    private bool _expanded;
    private bool _running;
    private bool _disposed;
    private bool _hoverSuppressed;
    private bool _pointerInside;
    private bool _pointerEventsSeen;
    private bool _pointerEventsAttached;
    private bool _dragActive;
    private long _lastTicks;
    private int _lastPhysicalWidth = -1;
    private int _lastPhysicalHeight = -1;

    public WindowMotionController(
        Window window,
        ShellViewModel state,
        IWindowPlacement placement,
        ISettingsStore settings,
        IAppLog log)
    {
        _window = window;
        _state = state;
        _placement = placement;
        _settings = settings;
        _log = log;

        _hoverTimer.Tick += (_, _) => OnHoverTick();
        _collapseTimer.Tick += (_, _) => OnCollapseTick();
    }

    public bool IsExpanded => _expanded;

    public void Start()
    {
        _hwnd = _window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (_hwnd == IntPtr.Zero)
        {
            _log.Warn("Window handle unavailable; motion disabled.");
            return;
        }

        AttachPointerEvents();
        ApplyCollapsedTarget(snap: true);
        _hoverTimer.Start();
    }

    private void AttachPointerEvents()
    {
        if (_pointerEventsAttached)
        {
            return;
        }

        _window.PointerEntered += OnPointerEntered;
        _window.PointerExited += OnPointerExited;
        _pointerEventsAttached = true;
    }

    private void DetachPointerEvents()
    {
        if (!_pointerEventsAttached)
        {
            return;
        }

        _window.PointerEntered -= OnPointerEntered;
        _window.PointerExited -= OnPointerExited;
        _pointerEventsAttached = false;
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        _pointerEventsSeen = true;
        _pointerInside = true;
        _collapseTimer.Stop();
        if (ShouldExpandOnHover(true, _hoverSuppressed))
        {
            SetExpanded(true);
        }
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        _pointerEventsSeen = true;
        _pointerInside = false;
        _hoverSuppressed = false;
        if (ShouldStartCollapse(_expanded, _pointerInside, _dragActive) &&
            !_collapseTimer.IsEnabled)
        {
            _collapseTimer.Start();
        }
    }

    public void SetExpanded(bool expanded)
    {
        if (_disposed || _expanded == expanded)
        {
            return;
        }

        if (!expanded && _dragActive)
        {
            return;
        }

        _expanded = expanded;
        _state.IsExpanded = expanded;
        _targetWidth = expanded
            ? WindowGeometry.ExpandedWidth
            : WindowGeometry.CollapsedWidthFor(_settings.Current.MinimalNotch);
        _targetHeight = expanded
            ? WindowGeometry.ExpandedHeight
            : WindowGeometry.CollapsedHeightFor(_settings.Current.MinimalNotch);
        EnsureFrames();
    }

    public void SuppressHover() => _hoverSuppressed = true;

    public void SetDragActive(bool active)
    {
        if (_disposed || _dragActive == active)
        {
            return;
        }

        _dragActive = active;

        if (active)
        {
            _collapseTimer.Stop();
            return;
        }

        if (ShouldStartCollapse(_expanded, _pointerInside, _dragActive) &&
            !_collapseTimer.IsEnabled)
        {
            _collapseTimer.Start();
        }
    }

    /// <summary>
    /// Hover expands the island in both normal and minimal-notch modes. The
    /// suppression flag is only set after an explicit close action.
    /// </summary>
    public static bool ShouldExpandOnHover(bool cursorInside, bool hoverSuppressed) =>
        cursorInside && !hoverSuppressed;

    /// <summary>
    /// A click expands the normal notch. The minimal notch is a hover-only
    /// surface, so a click must not open the full island.
    /// </summary>
    public static bool ShouldExpandOnClick(bool minimalNotch) => !minimalNotch;

    public static bool ShouldStartCollapse(
        bool expanded, bool pointerInside, bool dragActive) =>
        expanded && !pointerInside && !dragActive;

    public void Reapply()
    {
        if (_hwnd != IntPtr.Zero)
        {
            ApplySize(force: true);
        }
    }

    public void ApplySettings()
    {
        if (_settings.Current.MinimalNotch && _expanded)
        {
            _expanded = false;
            _state.IsExpanded = false;
            ApplyCollapsedTarget(snap: true);
            return;
        }

        if (!_expanded)
        {
            ApplyCollapsedTarget(snap: true);
        }
    }

    private void ApplyCollapsedTarget(bool snap)
    {
        _targetWidth = WindowGeometry.CollapsedWidthFor(_settings.Current.MinimalNotch);
        _targetHeight = WindowGeometry.CollapsedHeightFor(_settings.Current.MinimalNotch);

        if (!snap)
        {
            return;
        }

        _widthSpring = new SpringState { Value = _targetWidth };
        _heightSpring = new SpringState { Value = _targetHeight };
        ApplySize(force: true);
        _state.ExpansionProgress = 0;
        _state.IsExpanded = false;
        _expanded = false;
    }

    private void OnHoverTick()
    {
        if (_hwnd == IntPtr.Zero || !_pointerEventsSeen || !_expanded || _dragActive)
        {
            return;
        }

        // Pointer enter/leave is the source of truth. The geometry check is a
        // safety net for a missed leave event; it must never re-expand the
        // island, because XQueryPointer can be stale under XWayland.
        if (_pointerInside && !_placement.IsCursorInside(_hwnd))
        {
            _pointerInside = false;
            if (!_collapseTimer.IsEnabled)
            {
                _collapseTimer.Start();
            }
        }
    }

    private void OnCollapseTick()
    {
        _collapseTimer.Stop();
        if (_hwnd == IntPtr.Zero || _pointerInside || _dragActive)
        {
            return;
        }

        SetExpanded(false);
    }

    private void EnsureFrames()
    {
        if (_running || _disposed)
        {
            return;
        }

        _running = true;
        _lastTicks = 0;
        _window.RequestAnimationFrame(OnFrame);
    }

    private void OnFrame(TimeSpan _)
    {
        if (_disposed)
        {
            _running = false;
            return;
        }

        var now = Stopwatch.GetTimestamp();
        var dt = _lastTicks == 0
            ? 1.0 / 60.0
            : Math.Min((now - _lastTicks) / (double)Stopwatch.Frequency, 0.05);
        _lastTicks = now;

        var bouncy = _settings.Current.BouncySpring;
        var widthSettled = _widthSpring.Step(_targetWidth, dt, bouncy);
        var heightSettled = _heightSpring.Step(_targetHeight, dt, bouncy);

        ApplySize(force: false);
        _state.ExpansionProgress = Progress();

        if (widthSettled && heightSettled)
        {
            ApplySize(force: true);
            _running = false;
            return;
        }

        _window.RequestAnimationFrame(OnFrame);
    }

    private double Progress()
    {
        var collapsed = WindowGeometry.CollapsedWidthFor(_settings.Current.MinimalNotch);
        return Math.Clamp(
            (_widthSpring.Value - collapsed) /
            Math.Max(1, WindowGeometry.ExpandedWidth - collapsed),
            0,
            1);
    }

    private void ApplySize(bool force)
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        var screen = _window.Screens.Primary ?? _window.Screens.All.FirstOrDefault();
        if (screen is null)
        {
            return;
        }

        var scaling = screen.Scaling;
        var physicalWidth = (int)Math.Round(_widthSpring.Value * scaling);
        var physicalHeight = (int)Math.Round(_heightSpring.Value * scaling);

        if (!force &&
            Math.Abs(physicalWidth - _lastPhysicalWidth) < 1 &&
            Math.Abs(physicalHeight - _lastPhysicalHeight) < 1)
        {
            return;
        }

        _lastPhysicalWidth = physicalWidth;
        _lastPhysicalHeight = physicalHeight;
        _window.Width = _widthSpring.Value;
        _window.Height = _heightSpring.Value;

        var x = WindowGeometry.CenteredLeft(
            screen.Bounds.X,
            screen.Bounds.Width,
            _widthSpring.Value,
            scaling);
        _placement.SetBounds(_hwnd, x, screen.Bounds.Y, physicalWidth, physicalHeight);
    }

    public void Dispose()
    {
        _disposed = true;
        _dragActive = false;
        DetachPointerEvents();
        _hoverTimer.Stop();
        _collapseTimer.Stop();
    }
}
