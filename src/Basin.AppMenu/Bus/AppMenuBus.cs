using Basin.Diagnostics;
using Tmds.DBus.Protocol;
using static Basin.AppMenu.AppMenuLog;

namespace Basin.AppMenu;

public sealed class AppMenuBus : IDisposable
{
    private readonly ThreadAffinity _thread = ThreadAffinity.Capture();
    private readonly CompositorSynchronizationContext _context;
    private readonly string? _address;
    private Task<DBusConnection?>? _connecting;
    private DBusConnection? _connection;
    private bool _disposed;

    public AppMenuBus(ICompositorEventLoop loop, string? address = null)
    {
        ArgumentNullException.ThrowIfNull(loop);
        _address = address ?? DBusAddress.Session;
        _context = new CompositorSynchronizationContext(loop);
        BasinCounters.Track();
    }

    public bool HasAddress => _address is not null;

    internal Task<DBusConnection?> ConnectionAsync()
    {
        _thread.Assert();
        return _connecting ??= ConnectAsync();
    }

    internal void Post(Action action)
    {
        if (_disposed)
        {
            return;
        }

        _context.Post(_ =>
        {
            if (!_disposed)
            {
                action();
            }
        }, null);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _connection?.Dispose();
        _connection = null;
        _context.Dispose();
        BasinCounters.Untrack();
    }

    private async Task<DBusConnection?> ConnectAsync()
    {
        if (_address is null)
        {
            Log.Info($"this session has no D-Bus session bus; every window gets menus of the compositor's own");
            return null;
        }

        try
        {
            var connection = new DBusConnection(new DBusConnectionOptions(_address)
            {
                AutoConnect = false,
                OnException = context =>
                {
                    if (!_disposed)
                    {
                        Log.Debug($"menu bus: {context.Exception.Message}");
                    }
                },
            });
            await connection.ConnectAsync().ConfigureAwait(false);
            if (_disposed)
            {
                connection.Dispose();
                return null;
            }

            _connection = connection;
            return connection;
        }
        catch (Exception error) when (error is DBusExceptionBase or IOException or InvalidOperationException or System.Net.Sockets.SocketException)
        {
            Log.Warn($"the session bus is not reachable ({error.Message}); every window gets menus of the compositor's own");
            return null;
        }
    }
}
