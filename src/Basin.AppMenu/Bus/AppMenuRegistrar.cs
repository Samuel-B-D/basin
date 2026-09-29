using System.Text;
using Tmds.DBus.Protocol;
using static Basin.AppMenu.AppMenuLog;

namespace Basin.AppMenu;

public sealed class AppMenuRegistrar : IPathMethodHandler, IDisposable
{
    public const string BusName = "com.canonical.AppMenu.Registrar";

    private const string ObjectPath = "/com/canonical/AppMenu/Registrar";
    private const string Interface = "com.canonical.AppMenu.Registrar";

    private static readonly ReadOnlyMemory<byte> IntrospectXml = Encoding.UTF8.GetBytes(
        """
        <interface name="com.canonical.AppMenu.Registrar">
          <method name="RegisterWindow"><arg name="windowId" type="u" direction="in"/><arg name="menuObjectPath" type="o" direction="in"/></method>
          <method name="UnregisterWindow"><arg name="windowId" type="u" direction="in"/></method>
          <method name="GetMenuForWindow"><arg name="windowId" type="u" direction="in"/><arg name="service" type="s" direction="out"/><arg name="menuObjectPath" type="o" direction="out"/></method>
        </interface>
        """);

    private readonly AppMenuBus _bus;
    private readonly Lock _gate = new();
    private readonly Dictionary<uint, AppMenuAddress> _windows = [];
    private bool _disposed;

    public AppMenuRegistrar(AppMenuBus bus)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _ = StartAsync();
    }

    public bool OwnsName { get; private set; }

    public event Action<uint>? Changed;

    public event Action? Ready;

    string IPathMethodHandler.Path => ObjectPath;

    bool IPathMethodHandler.HandlesChildPaths => false;

    public AppMenuAddress? AddressOf(uint windowId)
    {
        lock (_gate)
        {
            return _windows.GetValueOrDefault(windowId);
        }
    }

    public void Forget(uint windowId)
    {
        lock (_gate)
        {
            _windows.Remove(windowId);
        }
    }

    ValueTask IPathMethodHandler.HandleMethodAsync(MethodContext context)
    {
        var request = context.Request;
        if (context.IsDBusIntrospectRequest)
        {
            context.ReplyIntrospectXml([IntrospectXml], []);
            return default;
        }

        if (request.InterfaceAsString != Interface)
        {
            context.ReplyUnknownMethodError();
            return default;
        }

        var reader = request.GetBodyReader();
        switch (request.MemberAsString)
        {
            case "RegisterWindow" when request.SignatureAsString == "uo":
            {
                var id = reader.ReadUInt32();
                var path = reader.ReadObjectPathAsString();
                var address = AppMenuAddress.DbusMenu(request.SenderAsString ?? "", path);
                lock (_gate)
                {
                    _windows[id] = address;
                }

                ReplyEmpty(context);
                Notify(id);
                return default;
            }

            case "UnregisterWindow" when request.SignatureAsString == "u":
            {
                var id = reader.ReadUInt32();
                bool removed;
                lock (_gate)
                {
                    removed = _windows.Remove(id);
                }

                ReplyEmpty(context);
                if (removed)
                {
                    Notify(id);
                }

                return default;
            }

            case "GetMenuForWindow" when request.SignatureAsString == "u":
            {
                var id = reader.ReadUInt32();
                var address = AddressOf(id);
                if (address is null)
                {
                    context.ReplyError("com.canonical.AppMenu.Registrar.Error.NoMenu", $"window {id} has no registered menu");
                    return default;
                }

                if (!context.NoReplyExpected)
                {
                    using var writer = context.CreateReplyWriter("so");
                    writer.WriteString(address.BusName);
                    writer.WriteObjectPath(address.ObjectPath);
                    context.Reply(writer.CreateMessage());
                }

                return default;
            }

            default:
                context.ReplyUnknownMethodError();
                return default;
        }
    }

    public void Dispose()
    {
        _disposed = true;
        lock (_gate)
        {
            _windows.Clear();
        }
    }

    private static void ReplyEmpty(MethodContext context)
    {
        if (context.NoReplyExpected)
        {
            return;
        }

        using var writer = context.CreateReplyWriter(null);
        context.Reply(writer.CreateMessage());
    }

    private void Notify(uint id) => _bus.Post(() =>
    {
        if (!_disposed)
        {
            Changed?.Invoke(id);
        }
    });

    private async Task StartAsync()
    {
        try
        {
            if (await _bus.ConnectionAsync().ConfigureAwait(false) is not { } connection || _disposed)
            {
                return;
            }

            connection.AddMethodHandler(this);
            var owns = await connection.TryRequestNameAsync(BusName, RequestNameOptions.None).ConfigureAwait(false);
            _bus.Post(() =>
            {
                OwnsName = owns;
                if (!owns)
                {
                    Log.Info($"{BusName} is owned by another process; windows register with it instead");
                }

                Ready?.Invoke();
            });
        }
        catch (Exception error) when (error is DBusExceptionBase or IOException or InvalidOperationException)
        {
            Log.Warn($"cannot own {BusName}: {error.Message}");
        }
    }
}
