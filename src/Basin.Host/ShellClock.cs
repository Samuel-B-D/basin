namespace Basin.Host;

public sealed class ShellClock : IDisposable
{
    private readonly IEventSource _timer;
    private readonly Func<DateTime, string?> _format;
    private readonly Action<string?> _show;
    private string? _frozen;

    public ShellClock(ICompositorEventLoop loop, Func<DateTime, string?> format, Action<string?> show)
    {
        ArgumentNullException.ThrowIfNull(loop);
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(show);
        _format = format;
        _show = show;
        _timer = loop.AddTimer(Refresh);
    }

    public Func<bool>? PerSecond { get; set; }

    public string? Frozen
    {
        get => _frozen;
        set
        {
            _frozen = value;
            Refresh();
        }
    }

    public void Refresh()
    {
        if (_timer.IsRemoved)
        {
            return;
        }

        if (_frozen is { } frozen)
        {
            _show(frozen);
            return;
        }

        var now = DateTime.Now;
        _show(_format(now));
        var delay = PerSecond?.Invoke() == true
            ? 1000 - now.Millisecond
            : ((60 - now.Second) * 1000) - now.Millisecond;
        _timer.UpdateTimer(Math.Max(1, delay));
    }

    public void Dispose()
    {
        if (!_timer.IsRemoved)
        {
            _timer.Remove();
        }
    }
}
