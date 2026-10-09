using Basin;
using Basin.Hosted;
using Basin.Transport.Waypipe;

namespace PortComp;

public sealed class PortCompSession : IDisposable
{
    private readonly BasinCompositorHost _host;
    private readonly Action<Action> _post;
    private readonly Func<CancellationToken, Task<Stream>> _open;
    private readonly WaypipeChannelOptions? _options;
    private readonly List<WaypipeChannel> _channels = [];
    private readonly CancellationTokenSource _closing = new();
    private bool _disposed;
    private WaypipeAdoption? _adoption;

    public PortCompSession(
        BasinCompositorHost host,
        Action<Action> postToCompositor,
        Func<CancellationToken, Task<Stream>> open,
        WaypipeChannelOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(postToCompositor);
        ArgumentNullException.ThrowIfNull(open);
        _host = host;
        _post = postToCompositor;
        _open = open;
        _options = options;
    }

    public int Clients { get; private set; }

    public event Action? Changed;

    public event Action<Exception?>? Ended;

    public Task StartAsync() => OpenSpareAsync(first: true);

    private async Task OpenSpareAsync(bool first = false)
    {
        if (_disposed)
        {
            return;
        }

        Stream stream;
        try
        {
            stream = await _open(_closing.Token).ConfigureAwait(true);
        }
        catch (Exception ex) when (!_disposed && !first)
        {
            Ended?.Invoke(ex);
            return;
        }

        if (_disposed)
        {
            stream.Dispose();
            return;
        }

        WaypipeChannel channel = null!;
        channel = WaypipeChannel.AttachChannel(stream, WaypipeCompression.Lz4, options: _options, configure: attached =>
        {
            channel = attached;
            _channels.Add(attached);
            attached.Opened += () =>
            {
                Clients++;
                Changed?.Invoke();
                _ = OpenSpareAsync();
            };
            attached.Ended += failure =>
            {
                var carried = _channels.Remove(attached);
                if (carried && Clients > 0)
                {
                    Clients--;
                }

                Changed?.Invoke();
                if (_channels.Count == 0 && !_disposed)
                {
                    Ended?.Invoke(failure);
                }
            };
        });
        _post(() =>
        {
            if (_disposed)
            {
                return;
            }

            _adoption ??= new WaypipeAdoption(
                _host.Display,
                _host.Services.Require<ClientBufferRegistry>(),
                _host.Services.Require<CompositorGlobal>(),
                _host.Dmabuf is not null);
            _adoption.Adopt(channel);
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _closing.Cancel();
        foreach (var channel in _channels.ToArray())
        {
            channel.Dispose();
        }

        _channels.Clear();
        _post(() =>
        {
            _adoption?.Dispose();
            _adoption = null;
        });
        _closing.Dispose();
    }
}
