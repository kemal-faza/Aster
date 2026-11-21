using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Aster.UI.Controls;

/// <summary>
/// A horizontal strip with a pill child that slides underneath its tabs. The
/// leading edge of the pill moves with a stiffer spring than the trailing edge,
/// so the highlight stretches while it travels between tabs.
/// </summary>
public sealed class PillStrip : Panel
{
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<PillStrip, double>(nameof(Spacing));

    public static readonly StyledProperty<bool> EqualWidthProperty =
        AvaloniaProperty.Register<PillStrip, bool>(nameof(EqualWidth));

    public static readonly StyledProperty<IBrush?> PillBrushProperty =
        AvaloniaProperty.Register<PillStrip, IBrush?>(nameof(PillBrush));

    public static readonly StyledProperty<double> PillCornerRadiusProperty =
        AvaloniaProperty.Register<PillStrip, double>(nameof(PillCornerRadius), 12d);

    private const double LeadStiffness = 620;
    private const double LeadDamping = 40;
    private const double TrailStiffness = 170;
    private const double TrailDamping = 19;
    private const int Substeps = 4;
    private const double SettlePosition = 0.1;
    private const double SettleVelocity = 1;

    private readonly Border _pill;
    private Control? _active;
    private EdgeSpring _left;
    private EdgeSpring _right;
    private double _targetLeft;
    private double _targetRight;
    private double _pillLeft;
    private double _pillRight;
    private double _arrangedWidth;
    private double _arrangedHeight;
    private bool _pillReady;
    private bool _animating;
    private long _lastFrame;

    static PillStrip()
    {
        AffectsMeasure<PillStrip>(SpacingProperty, EqualWidthProperty);
        AffectsArrange<PillStrip>(SpacingProperty, EqualWidthProperty);
    }

    public PillStrip()
    {
        _pill = new Border
        {
            IsHitTestVisible = false,
            IsVisible = false,
            CornerRadius = new CornerRadius(PillCornerRadius),
            Background = PillBrush,
        };
        Children.Add(_pill);
    }

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    public bool EqualWidth
    {
        get => GetValue(EqualWidthProperty);
        set => SetValue(EqualWidthProperty, value);
    }

    public IBrush? PillBrush
    {
        get => GetValue(PillBrushProperty);
        set => SetValue(PillBrushProperty, value);
    }

    public double PillCornerRadius
    {
        get => GetValue(PillCornerRadiusProperty);
        set => SetValue(PillCornerRadiusProperty, value);
    }

    private int TabCount
    {
        get
        {
            var count = 0;
            foreach (var child in Children)
            {
                if (!ReferenceEquals(child, _pill))
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>
    /// Selects the child the pill should sit under. Call this after the child's
    /// checked/active state changes. Before the first layout pass the pill snaps
    /// into place without animating.
    /// </summary>
    public void MoveTo(Control? child, bool animate = true)
    {
        if (child is not null && !Children.Contains(child))
        {
            return;
        }

        var previous = _active;
        var newIndex = child is null ? -1 : Children.IndexOf(child);
        var oldIndex = previous is null ? newIndex : Children.IndexOf(previous);

        _active = child;
        if (child is null)
        {
            _pillReady = false;
            _pill.IsVisible = false;
            return;
        }

        var bounds = child.Bounds;
        var stripWidth = _arrangedWidth > 0 ? _arrangedWidth : Bounds.Width;
        if (bounds.Width <= 0 || stripWidth <= 0)
        {
            _pillReady = false;
            _pill.IsVisible = false;
            return;
        }

        if (ReferenceEquals(previous, child))
        {
            if (!_pillReady)
            {
                SetTarget(bounds, movingRight: true, animate: false);
            }
            else if (TargetsDiffer(bounds))
            {
                SetTarget(bounds, bounds.X >= _targetLeft, animate);
            }

            return;
        }

        SetTarget(bounds, newIndex >= oldIndex, animate && _pillReady);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = TabCount;
        if (count == 0)
        {
            return default;
        }

        var gap = Math.Max(0, Spacing) * (count - 1);
        var width = gap;
        var height = 0d;

        if (EqualWidth && double.IsFinite(availableSize.Width))
        {
            var childWidth = Math.Max(0, (availableSize.Width - gap) / count);
            foreach (var child in Children)
            {
                if (ReferenceEquals(child, _pill))
                {
                    continue;
                }

                child.Measure(new Size(childWidth, availableSize.Height));
                height = Math.Max(height, child.DesiredSize.Height);
            }

            return new Size(availableSize.Width, height);
        }

        foreach (var child in Children)
        {
            if (ReferenceEquals(child, _pill))
            {
                continue;
            }

            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            width += child.DesiredSize.Width;
            height = Math.Max(height, child.DesiredSize.Height);
        }

        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _arrangedWidth = finalSize.Width;
        _arrangedHeight = finalSize.Height;

        var count = TabCount;
        if (count == 0)
        {
            _pill.IsVisible = false;
            return finalSize;
        }

        var spacing = Math.Max(0, Spacing);
        var gap = spacing * (count - 1);
        var cellWidth = EqualWidth
            ? Math.Max(0, (finalSize.Width - gap) / count)
            : 0;
        var x = 0d;

        foreach (var child in Children)
        {
            if (ReferenceEquals(child, _pill))
            {
                continue;
            }

            var width = EqualWidth ? cellWidth : child.DesiredSize.Width;
            child.Arrange(new Rect(x, 0, width, finalSize.Height));
            x += width + spacing;
        }

        if (_active is null || !Children.Contains(_active))
        {
            _active = null;
            _pillReady = false;
            _pill.IsVisible = false;
            return finalSize;
        }

        var bounds = _active.Bounds;
        if (bounds.Width <= 0 || finalSize.Width <= 0)
        {
            _pillReady = false;
            _pill.IsVisible = false;
            return finalSize;
        }

        if (!_pillReady)
        {
            SetTarget(bounds, movingRight: true, animate: false);
        }
        else if (TargetsDiffer(bounds))
        {
            SetTarget(bounds, bounds.X >= _targetLeft, animate: true);
        }
        else
        {
            ApplyPill();
        }

        return finalSize;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!Children.Contains(_pill))
        {
            Children.Insert(0, _pill);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PillBrushProperty)
        {
            _pill.Background = PillBrush;
        }
        else if (change.Property == PillCornerRadiusProperty)
        {
            _pill.CornerRadius = new CornerRadius(PillCornerRadius);
        }
    }

    private bool TargetsDiffer(Rect bounds) =>
        Math.Abs(bounds.X - _targetLeft) > 0.05 ||
        Math.Abs(bounds.Right - _targetRight) > 0.05;

    private void SetTarget(Rect bounds, bool movingRight, bool animate)
    {
        _targetLeft = bounds.X;
        _targetRight = bounds.Right;

        if (!animate || !_pillReady)
        {
            _left = new EdgeSpring { Value = _targetLeft };
            _right = new EdgeSpring { Value = _targetRight };
            ApplyPill();
            _pillReady = true;
            _animating = false;
            return;
        }

        _left.Stiffness = movingRight ? TrailStiffness : LeadStiffness;
        _left.Damping = movingRight ? TrailDamping : LeadDamping;
        _right.Stiffness = movingRight ? LeadStiffness : TrailStiffness;
        _right.Damping = movingRight ? LeadDamping : TrailDamping;
        StartAnimation();
    }

    private void ApplyPill()
    {
        var width = _arrangedWidth > 0 ? _arrangedWidth : Bounds.Width;
        var height = _arrangedHeight > 0 ? _arrangedHeight : Bounds.Height;
        if (width <= 0 || height <= 0)
        {
            _pill.IsVisible = false;
            return;
        }

        var left = Math.Clamp(_left.Value, 0, width);
        var right = Math.Clamp(_right.Value, left, width);
        _pillLeft = left;
        _pillRight = right;
        _pill.IsVisible = true;
        _pill.Arrange(new Rect(left, 0, Math.Max(0, right - left), height));
    }

    private void StartAnimation()
    {
        if (_animating)
        {
            return;
        }

        var top = TopLevel.GetTopLevel(this);
        if (top is null)
        {
            SnapToTarget();
            return;
        }

        _animating = true;
        _lastFrame = 0;
        top.RequestAnimationFrame(OnFrame);
    }

    private void OnFrame(TimeSpan _)
    {
        if (!_animating)
        {
            return;
        }

        var top = TopLevel.GetTopLevel(this);
        if (top is null)
        {
            _animating = false;
            return;
        }

        var now = Stopwatch.GetTimestamp();
        var dt = _lastFrame == 0
            ? 1d / 60d
            : Math.Min((now - _lastFrame) / (double)Stopwatch.Frequency, 0.05);
        _lastFrame = now;

        var step = dt / Substeps;
        var settled = true;
        for (var i = 0; i < Substeps; i++)
        {
            settled &= Step(ref _left, _targetLeft, step);
            settled &= Step(ref _right, _targetRight, step);
        }

        ApplyPill();

        if (settled)
        {
            SnapToTarget();
            _animating = false;
            return;
        }

        top.RequestAnimationFrame(OnFrame);
    }

    private void SnapToTarget()
    {
        _left = new EdgeSpring { Value = _targetLeft };
        _right = new EdgeSpring { Value = _targetRight };
        ApplyPill();
    }

    private static bool Step(ref EdgeSpring spring, double target, double dt)
    {
        var acceleration =
            (target - spring.Value) * spring.Stiffness - spring.Velocity * spring.Damping;
        spring.Velocity += acceleration * dt;
        spring.Value += spring.Velocity * dt;
        return Math.Abs(target - spring.Value) < SettlePosition &&
               Math.Abs(spring.Velocity) < SettleVelocity;
    }

    private struct EdgeSpring
    {
        public double Value;
        public double Velocity;
        public double Stiffness;
        public double Damping;
    }
}
