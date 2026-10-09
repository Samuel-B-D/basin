using Basin.Diagnostics;
using Tmds.DBus.Protocol;
using static Basin.SystemStatus.SystemStatusLog;

namespace Basin.SystemStatus;

public sealed class SystemStatusBus : IDisposable
{
    private readonly ThreadAffinity _thread = ThreadAffinity.Capture();
    private readonly CompositorSynchronizationContext _context;
    private Task<DBusConnection?>? _connecting;
    private DBusConnection? _connection;
    private string? _failure;
    private bool _disposed;

    public SystemStatusBus(ICompositorEventLoop loop, SystemStatusOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(loop);
        options ??= new SystemStatusOptions();
        Loop = loop;
        Address = options.Address
            ?? (Environment.GetEnvironmentVariable("DBUS_SYSTEM_BUS_ADDRESS") is { Length: > 0 } fromEnvironment ? fromEnvironment : DBusAddress.System);
        CoalesceMilliseconds = Math.Max(0, options.CoalesceMilliseconds);
        _context = new CompositorSynchronizationContext(loop);
        BasinCounters.Track();
    }

    public string? Address { get; }

    public bool HasAddress => Address is not null;

    public bool IsDisposed => _disposed;

    public ICompositorEventLoop Loop { get; }

    internal int CoalesceMilliseconds { get; }

    internal string? Failure => _failure;

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

        _thread.Assert();
        _disposed = true;
        _connection?.Dispose();
        _connection = null;
        _context.Dispose();
        BasinCounters.Untrack();
    }

    private async Task<DBusConnection?> ConnectAsync()
    {
        if (Address is null)
        {
            _failure = "this session has no system bus address";
            Log.Info($"{_failure}; every system status reads as absent");
            return null;
        }

        try
        {
            var connection = new DBusConnection(new DBusConnectionOptions(Address)
            {
                AutoConnect = false,
                OnException = context =>
                {
                    if (!_disposed)
                    {
                        Log.Debug($"system bus: {context.Exception.Message}");
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
            _failure = $"the system bus is not reachable ({error.Message})";
            Log.Warn($"{_failure}; every system status reads as absent");
            return null;
        }
    }
}
