using Wayland.Server;

namespace Basin.Transport.Waypipe;

public sealed class WaypipeAdoption : IDisposable
{
    private const string DmabufInterface = "zwp_linux_dmabuf_v1";

    private readonly WlServerDisplay _display;
    private readonly ClientBufferRegistry _buffers;
    private readonly CompositorGlobal? _compositor;
    private readonly bool _hostHasDmabuf;
    private readonly WlServerDisplay.GlobalFilter? _inner;
    private readonly Dictionary<WlClient, WaypipeChannel> _clients = [];
    private LinuxDmabufGlobal? _synthetic;
    private bool _filtering;

    public WaypipeAdoption(
        WlServerDisplay display,
        ClientBufferRegistry buffers,
        CompositorGlobal? compositor,
        bool hostHasDmabuf,
        WlServerDisplay.GlobalFilter? inner = null)
    {
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(buffers);
        _display = display;
        _buffers = buffers;
        _compositor = compositor;
        _hostHasDmabuf = hostHasDmabuf;
        _inner = inner;
    }

    public int Count => _clients.Count;

    public WlClient Adopt(WaypipeChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        if (!_filtering)
        {
            _filtering = true;
            _display.SetGlobalFilter(Visible);
        }

        if (channel.Options.CarriesDmabuf && !_hostHasDmabuf && _synthetic is null)
        {
            _synthetic = new LinuxDmabufGlobal(
                _display,
                _buffers,
                channel.Globals.Formats,
                WaypipeGlobals.SyntheticMainDevice,
                compositor: _compositor);
        }

        var client = _display.CreateClient(channel.Transport);
        _clients[client] = channel;
        client.Destroyed += () => _clients.Remove(client);
        return client;
    }

    public void Dispose()
    {
        foreach (var client in _clients.Keys.ToArray())
        {
            client.Destroy();
        }

        _clients.Clear();
        if (_filtering)
        {
            _filtering = false;
            _display.SetGlobalFilter(_inner);
        }

        _synthetic?.Dispose();
        _synthetic = null;
    }

    internal bool Visible(WlClient client, WlGlobal? global, string interfaceName)
    {
        if (_inner is not null && !_inner(client, global, interfaceName))
        {
            return false;
        }

        return _clients.TryGetValue(client, out var channel)
            ? channel.Globals.Carries(interfaceName)
            : _synthetic is null || interfaceName != DmabufInterface;
    }
}
