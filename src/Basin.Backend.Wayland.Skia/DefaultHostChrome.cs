using Basin.UI.Skia;

namespace Basin.Backend.Wayland.Skia;

public sealed class DefaultHostChrome : IDisposable
{
    private readonly WaylandOutput _output;
    private readonly SkiaUIHost _host;
    private readonly FlatFrameTheme _theme;
    private bool _disposed;

    private DefaultHostChrome(WaylandOutput output, WaylandHostFrame frame, string? title)
    {
        _output = output;
        _host = new SkiaUIHost();
        _theme = FlatFrameTheme.Default();
        Chrome = new FrameHostChrome(frame, _host, new FlatFrameRenderer(_theme), title);
        _output.Committed += Chrome.OnOutputChanged;
    }

    public FrameHostChrome Chrome { get; }

    public static DefaultHostChrome Attach(WaylandOutput output, WaylandHostFrame frame, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(frame);
        return new DefaultHostChrome(output, frame, title);
    }

    public static IDisposable Wire(WaylandOutput output, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new Wiring(output, title);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _output.Committed -= Chrome.OnOutputChanged;
        Chrome.Dispose();
        _theme.Dispose();
        _host.Dispose();
    }

    private sealed class Wiring : IDisposable
    {
        private readonly WaylandOutput _output;
        private readonly string? _title;
        private DefaultHostChrome? _chrome;

        public Wiring(WaylandOutput output, string? title)
        {
            _output = output;
            _title = title;
            output.HostFrameAvailable += OnFrame;
        }

        public void Dispose()
        {
            _output.HostFrameAvailable -= OnFrame;
            _chrome?.Dispose();
            _chrome = null;
        }

        private void OnFrame(WaylandHostFrame frame)
        {
            _chrome?.Dispose();
            _chrome = Attach(_output, frame, _title);
        }
    }
}
