namespace Basin.SoftKeyboard;

public sealed class SoftKeyboardTimer : IDisposable
{
    private readonly SoftKeyboardEngine _engine;
    private readonly Func<long> _clock;
    private readonly IEventSource _timer;
    private long _armed = long.MaxValue;

    public SoftKeyboardTimer(ICompositorEventLoop loop, SoftKeyboardEngine engine, Func<long>? clock = null)
    {
        ArgumentNullException.ThrowIfNull(loop);
        ArgumentNullException.ThrowIfNull(engine);
        _engine = engine;
        _clock = clock ?? (static () => Environment.TickCount64);
        _timer = loop.AddTimer(OnTimer);
        engine.DeadlineChanged += Rearm;
    }

    public void Dispose()
    {
        _engine.DeadlineChanged -= Rearm;
        _timer.Remove();
    }

    private void OnTimer()
    {
        _armed = long.MaxValue;
        _engine.Tick(_clock());
        Rearm();
    }

    private void Rearm()
    {
        var deadline = _engine.NextDeadline;
        if (deadline == _armed)
        {
            return;
        }

        _armed = deadline;
        if (deadline == long.MaxValue)
        {
            _timer.UpdateTimer(0);
            return;
        }

        var delay = deadline - _clock();
        _timer.UpdateTimer((int)Math.Clamp(delay, 1, int.MaxValue));
    }
}
