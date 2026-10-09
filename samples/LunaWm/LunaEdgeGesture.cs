using Basin.Seat;

namespace LunaWm;

internal sealed class LunaEdgeGesture : ITouchGestures
{
    private const int Capacity = EdgeSwipeRecognizer.WithheldCapacity;

    private readonly EdgeSwipeSample[] _withheld = new EdgeSwipeSample[Capacity];
    private int _withheldCount;
    private Phase _phase;
    private int _id;
    private LunaEdge _edge;
    private double _scale = 1;
    private double _x0;
    private double _y0;
    private uint _began;
    private VelocityTracker _tracker;

    public ILunaEdgeHandler? Handler { get; set; }

    public double ClaimDistance { get; set; } = 8;

    public double AlongSlop { get; set; } = 16;

    public uint WithholdMillis { get; set; } = 150;

    public bool IsActive => _phase != Phase.Idle;

    public LunaEdge Edge => _edge;

    public TouchGestureVerdict Down(int id, uint timeMs, double x, double y)
    {
        if (_phase != Phase.Idle)
        {
            return _phase == Phase.Claimed ? TouchGestureVerdict.Owned : TouchGestureVerdict.Pass;
        }

        if (Handler is not { } handler || !handler.TryBegin(x, y, out var edge, out var scale) || edge == LunaEdge.None)
        {
            return TouchGestureVerdict.Pass;
        }

        _phase = Phase.Candidate;
        _id = id;
        _edge = edge;
        _scale = scale <= 0 ? 1 : scale;
        _x0 = x;
        _y0 = y;
        _began = timeMs;
        _tracker.Reset();
        _tracker.Add(x, y, 0);
        _withheldCount = 0;
        Withhold(new EdgeSwipeSample(timeMs, x, y, Down: true));
        return TouchGestureVerdict.Withhold;
    }

    public TouchGestureVerdict Motion(int id, uint timeMs, double x, double y)
    {
        if (_phase == Phase.Idle || id != _id)
        {
            return _phase == Phase.Claimed ? TouchGestureVerdict.Owned : TouchGestureVerdict.Pass;
        }

        _tracker.Add(x, y, Nanos(timeMs));
        if (_phase == Phase.Claimed)
        {
            Handler?.Move(x, y, timeMs);
            return TouchGestureVerdict.Owned;
        }

        var dx = x - _x0;
        var dy = y - _y0;
        var (inward, along) = _edge switch
        {
            LunaEdge.Bottom => (-dy, dx),
            LunaEdge.Top => (dy, dx),
            LunaEdge.Left => (dx, dy),
            _ => (-dx, dy),
        };
        var claim = ClaimDistance * _scale;
        if (inward >= claim && inward >= Math.Abs(along))
        {
            return TryClaim(EdgeAxis.Inward, x, y, timeMs);
        }

        if (Math.Abs(along) >= claim && Math.Abs(along) > Math.Abs(inward))
        {
            if (_edge == LunaEdge.Bottom && Math.Abs(along) >= AlongSlop * _scale)
            {
                return TryClaim(EdgeAxis.Along, x, y, timeMs);
            }

            if (_edge != LunaEdge.Bottom && Math.Abs(along) >= AlongSlop * _scale)
            {
                return Decline();
            }
        }

        if (timeMs - _began > WithholdMillis || _withheldCount >= Capacity)
        {
            return Decline();
        }

        Withhold(new EdgeSwipeSample(timeMs, x, y, Down: false));
        return TouchGestureVerdict.Withhold;
    }

    public TouchGestureVerdict Up(int id, uint timeMs)
    {
        if (_phase == Phase.Idle || id != _id)
        {
            return _phase == Phase.Claimed ? TouchGestureVerdict.Owned : TouchGestureVerdict.Pass;
        }

        if (_phase == Phase.Candidate)
        {
            _phase = Phase.Idle;
            return TouchGestureVerdict.Decline;
        }

        var (vx, vy) = _tracker.Velocity(Nanos(timeMs));
        _phase = Phase.Idle;
        Handler?.End(vx, vy, timeMs, cancelled: false);
        return TouchGestureVerdict.Finish;
    }

    public void Cancel()
    {
        if (_phase == Phase.Claimed)
        {
            Handler?.End(0, 0, 0, cancelled: true);
        }

        _phase = Phase.Idle;
        _withheldCount = 0;
    }

    public int TakeWithheld(Span<EdgeSwipeSample> into)
    {
        var count = Math.Min(_withheldCount, into.Length);
        _withheld.AsSpan(0, count).CopyTo(into);
        _withheldCount = 0;
        return count;
    }

    private TouchGestureVerdict TryClaim(EdgeAxis axis, double x, double y, uint timeMs)
    {
        if (Handler is { } handler && handler.Claim(_edge, axis, _x0, _y0, timeMs))
        {
            _phase = Phase.Claimed;
            _withheldCount = 0;
            handler.Move(x, y, timeMs);
            return TouchGestureVerdict.Claim;
        }

        return Decline();
    }

    private TouchGestureVerdict Decline()
    {
        _phase = Phase.Idle;
        return TouchGestureVerdict.Decline;
    }

    private void Withhold(in EdgeSwipeSample sample)
    {
        if (_withheldCount < Capacity)
        {
            _withheld[_withheldCount++] = sample;
        }
    }

    private long Nanos(uint timeMs) => (long)(uint)(timeMs - _began) * 1_000_000;

    private enum Phase
    {
        Idle,

        Candidate,

        Claimed,
    }
}
