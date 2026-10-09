namespace LunaWm;

internal struct Motion
{
    private MotionSpec _spec;
    private long _startMillis;
    private double _durationMs;

    public double From { get; private set; }

    public double To { get; private set; }

    public double Value { get; private set; }

    public bool IsRunning { get; private set; }

    public void Start(in MotionSpec spec, double from, double to, long nowMillis, double timeScale)
    {
        _spec = spec;
        _startMillis = nowMillis;
        _durationMs = spec.DurationMs * timeScale;
        From = from;
        To = to;
        if (_durationMs <= 0 || from == to)
        {
            Value = to;
            IsRunning = false;
            return;
        }

        Value = from;
        IsRunning = true;
    }

    public void Jump(double value)
    {
        From = value;
        To = value;
        Value = value;
        IsRunning = false;
    }

    public bool Advance(long nowMillis)
    {
        if (!IsRunning)
        {
            return false;
        }

        var progress = (nowMillis - _startMillis) / _durationMs;
        if (progress >= 1)
        {
            Value = To;
            IsRunning = false;
            return true;
        }

        Value = From + ((To - From) * QtEasing.Evaluate(_spec.Curve, Math.Max(0, progress)));
        return true;
    }

    public void Settle()
    {
        Value = To;
        IsRunning = false;
    }
}
