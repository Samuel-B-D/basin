namespace Basin.Desktop;

public sealed class PointerConstraintDriver : IDisposable
{
    private readonly PointerConstraintsManager _constraints;
    private readonly Seat.Seat _seat;
    private readonly Action _onActiveDeactivated;
    private PointerConstraint? _active;
    private double _originX;
    private double _originY;
    private bool _disposed;

    internal PointerConstraintDriver(PointerConstraintsManager constraints, Seat.Seat seat)
    {
        _constraints = constraints;
        _seat = seat;
        _onActiveDeactivated = OnActiveDeactivated;
        _seat.Pointer.FocusChanged += OnFocusChanged;
        _seat.Pointer.Moved += OnMoved;
    }

    public Func<PointerConstraint, bool>? Allow { get; set; }

    public PointerConstraint? Active => _active;

    public bool IsLocked => _active is { Kind: ConstraintKind.Lock };

    public event Action<PointerConstraint?, PointerConstraint?>? Changed;

    public void Refresh()
    {
        if (_disposed)
        {
            return;
        }

        var pointer = _seat.Pointer;
        PointerConstraint? next = null;
        if (pointer.Focus is { } focus && _constraints.ConstraintFor(focus) is { } candidate &&
            Allow?.Invoke(candidate) != false &&
            (candidate == _active || candidate.Contains(pointer.X, pointer.Y)))
        {
            next = candidate;
        }

        if (next == _active)
        {
            if (next is not null)
            {
                RememberOrigin();
            }

            return;
        }

        var previous = _active;
        if (previous is not null)
        {
            previous.Deactivated -= _onActiveDeactivated;
            _active = null;
            previous.Deactivate();
        }

        if (next is not null)
        {
            RememberOrigin();
            _active = next;
            next.Deactivated += _onActiveDeactivated;
            next.Activate();
        }

        Changed?.Invoke(previous, _active);
    }

    public bool Constrain(double fromX, double fromY, ref double layoutX, ref double layoutY)
    {
        switch (_active)
        {
            case { Kind: ConstraintKind.Lock }:
                var held = layoutX != fromX || layoutY != fromY;
                (layoutX, layoutY) = (fromX, fromY);
                return held;

            case { Kind: ConstraintKind.Confine } confine:
                var surfaceX = layoutX - _originX;
                var surfaceY = layoutY - _originY;
                if (!confine.Clamp(ref surfaceX, ref surfaceY))
                {
                    return false;
                }

                (layoutX, layoutY) = (surfaceX + _originX, surfaceY + _originY);
                return true;

            default:
                return false;
        }
    }

    public bool ReleasePoint(PointerConstraint constraint, out double layoutX, out double layoutY)
    {
        ArgumentNullException.ThrowIfNull(constraint);
        if (constraint.Kind == ConstraintKind.Lock && constraint.CursorPositionHint is { } hint)
        {
            layoutX = _originX + hint.X;
            layoutY = _originY + hint.Y;
            return true;
        }

        layoutX = 0;
        layoutY = 0;
        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _seat.Pointer.FocusChanged -= OnFocusChanged;
        _seat.Pointer.Moved -= OnMoved;
        if (_active is { } active)
        {
            active.Deactivated -= _onActiveDeactivated;
            _active = null;
        }
    }

    private void RememberOrigin()
    {
        var pointer = _seat.Pointer;
        _originX = pointer.LayoutX - pointer.X;
        _originY = pointer.LayoutY - pointer.Y;
    }

    private void OnFocusChanged(Surface? focus) => Refresh();

    private void OnMoved(uint timeMs, double x, double y)
    {
        if (_active is null && (_seat.Pointer.Focus is not { } focus || _constraints.ConstraintFor(focus) is null))
        {
            return;
        }

        Refresh();
    }

    private void OnActiveDeactivated()
    {
        if (_active is not { } previous)
        {
            return;
        }

        previous.Deactivated -= _onActiveDeactivated;
        _active = null;
        Changed?.Invoke(previous, null);
    }
}
