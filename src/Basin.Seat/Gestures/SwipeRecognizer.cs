namespace Basin.Seat;

public sealed class SwipeRecognizer
{
    private const double RubberBandFalloff = 4.0;

    private double _raw;
    private double _width = 1;
    private double _travel;
    private VelocityTracker _tracker;
    private uint _beganMillis;

    public SwipeRecognizer(uint fingers = 3) => Fingers = fingers;

    public uint Fingers { get; }

    public SwipeAxis Axis { get; set; } = SwipeAxis.Horizontal;

    public bool IsActive { get; private set; }

    public double Progress
    {
        get
        {
            if (_raw > 0)
            {
                return ClampHigh ? Damp(_raw) : Math.Min(_raw, 1);
            }

            return _raw < 0
                ? ClampLow ? -Damp(-_raw) : Math.Max(_raw, -1)
                : 0;
        }
    }

    public int Direction => Math.Sign(_raw);

    public double Velocity => _tracker.Smoothed.X;

    public double ReleaseVelocity { get; private set; }

    public double CommitFraction { get; set; } = 0.5;

    public double FlingPerSecond { get; set; } = 1200;

    public bool ClampLow { get; set; }

    public bool ClampHigh { get; set; }

    public bool Begin(uint fingers, double width, uint timeMs)
    {
        Abort();
        ReleaseVelocity = 0;
        if (fingers != Fingers || width <= 0)
        {
            return false;
        }

        IsActive = true;
        _width = width;
        _beganMillis = timeMs;
        _tracker.Add(0, 0, 0);
        return true;
    }

    public bool Update(double dx, double dy, uint timeMs)
    {
        if (!IsActive)
        {
            return false;
        }

        var along = Axis == SwipeAxis.Vertical ? dy : dx;
        _raw += along / _width;
        _travel += along;
        _tracker.Add(_travel, 0, Nanos(timeMs));
        return true;
    }

    public SwipeOutcome End(bool cancelled, uint timeMs)
    {
        if (!IsActive)
        {
            return SwipeOutcome.None;
        }

        var progress = Progress;
        var velocity = _tracker.Velocity(Nanos(timeMs)).X;
        ReleaseVelocity = cancelled ? 0 : velocity / _width;
        var againstLimit = (_raw > 0 && ClampHigh) || (_raw < 0 && ClampLow);
        Abort();

        if (cancelled || againstLimit || progress == 0)
        {
            return SwipeOutcome.Cancel;
        }

        if (Math.Abs(progress) >= CommitFraction)
        {
            return SwipeOutcome.Commit;
        }

        return Math.Abs(velocity) >= FlingPerSecond && Math.Sign(velocity) == Math.Sign(progress)
            ? SwipeOutcome.Commit
            : SwipeOutcome.Cancel;
    }

    public void Abort()
    {
        IsActive = false;
        _raw = 0;
        _travel = 0;
        _tracker.Reset();
        _beganMillis = 0;
        ClampLow = false;
        ClampHigh = false;
    }

    private long Nanos(uint timeMs) => (long)(uint)(timeMs - _beganMillis) * 1_000_000;

    private static double Damp(double excess) => excess / (1 + (excess * RubberBandFalloff));
}
