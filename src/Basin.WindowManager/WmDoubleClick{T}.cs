namespace Basin.WindowManager;

public sealed class WmDoubleClick<T>
    where T : class
{
    public const long ThresholdMs = 400;

    private T? _target;
    private long _at;

    public bool Click(T target) => Click(target, Environment.TickCount64);

    public bool Click(T target, long nowMs)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (_target is not null && ReferenceEquals(_target, target) && nowMs - _at <= ThresholdMs)
        {
            _target = null;
            return true;
        }

        _target = target;
        _at = nowMs;
        return false;
    }

    public void Reset() => _target = null;

    public void Forget(T target)
    {
        if (ReferenceEquals(_target, target))
        {
            _target = null;
        }
    }
}
