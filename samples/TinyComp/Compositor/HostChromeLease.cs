using Basin.Backend.Wayland;

namespace TinyComp;

internal sealed class HostChromeLease : IDisposable
{
    private readonly Func<WaylandOutput, WaylandHostFrame, IDisposable> _create;
    private IDisposable _current;
    private bool _disposed;

    public HostChromeLease(WaylandOutput output, WaylandHostFrame frame, Func<WaylandOutput, WaylandHostFrame, IDisposable> create)
    {
        Output = output;
        Frame = frame;
        _create = create;
        _current = create(output, frame);
    }

    public WaylandOutput Output { get; }

    public WaylandHostFrame Frame { get; }

    public void Rebuild()
    {
        if (_disposed)
        {
            return;
        }

        _current.Dispose();
        _current = _create(Output, Frame);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _current.Dispose();
    }
}
