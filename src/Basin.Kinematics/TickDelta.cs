namespace Basin.Kinematics;

public struct TickDelta
{
    public const long MaxStepNanos = 1_000_000_000;

    private long _lastNanos;
    private bool _anchored;

    public readonly bool IsAnchored => _anchored;

    public long Advance(in FrameTick tick)
    {
        var now = tick.TargetPresentNanos;
        if (!_anchored)
        {
            _anchored = true;
            _lastNanos = now;
            return Math.Clamp(tick.RefreshIntervalNanos, 0, MaxStepNanos);
        }

        var elapsed = now - _lastNanos;
        if (elapsed <= 0)
        {
            return 0;
        }

        _lastNanos = now;
        return Math.Min(elapsed, MaxStepNanos);
    }

    public void Reset() => _anchored = false;
}
