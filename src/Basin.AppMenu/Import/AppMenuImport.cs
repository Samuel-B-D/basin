using Basin.Shell.Xdg;

namespace Basin.AppMenu;

public sealed class AppMenuImport : IDisposable
{
    private readonly AppMenuBus _bus;
    private readonly XdgToplevelSource? _mirror;
    private readonly XdgToplevelWindow? _xdg;

    public AppMenuImport(AppMenuBus bus, XdgToplevelSource? mirror = null, XdgToplevelWindow? xdg = null)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
        _mirror = mirror;
        _xdg = xdg;
    }

    public event Action<IAppMenuSource>? Ready;

    public event Action<AppMenuAddress>? Failed;

    public event Action? Cleared;

    public AppMenuAddress? Address { get; private set; }

    public IAppMenuSource? Source { get; private set; }

    public static AppMenuAddress? Resolve(
        AppMenuAddresses addresses,
        Surface? surface,
        string? x11ServiceName = null,
        string? x11ObjectPath = null,
        AppMenuRegistrar? registrar = null,
        uint? x11WindowId = null)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        if (surface is not null && addresses.AddressOf(surface) is { } address)
        {
            return address;
        }

        if (x11ServiceName is { Length: > 0 } service && x11ObjectPath is { Length: > 0 } path)
        {
            return AppMenuAddress.DbusMenu(service, path);
        }

        return x11WindowId is { } windowId ? registrar?.AddressOf(windowId) : null;
    }

    public bool Import(AppMenuAddress? address)
    {
        if (Address == address)
        {
            return false;
        }

        Drop();
        Address = address;
        if (address is null)
        {
            Cleared?.Invoke();
            return true;
        }

        if (_xdg is not null && address.Kind == AppMenuAddressKind.DbusMenu)
        {
            _mirror?.SetAppMenu(_xdg, address.BusName, address.ObjectPath);
        }

        IAppMenuSource reader = address.Kind == AppMenuAddressKind.GMenu
            ? new GMenuReader(_bus, address)
            : new DbusMenuReader(_bus, address.BusName, address.ObjectPath);
        Source = reader;
        var ready = false;
        reader.Menu.Changed += () =>
        {
            if (!ready && reader.IsReady && ReferenceEquals(Source, reader))
            {
                ready = true;
                Ready?.Invoke(reader);
            }
        };
        reader.Failed += () =>
        {
            if (!ReferenceEquals(Source, reader))
            {
                return;
            }

            Source = null;
            reader.Dispose();
            Failed?.Invoke(address);
        };
        return true;
    }

    public void Dispose()
    {
        Drop();
        Address = null;
    }

    private void Drop()
    {
        Source?.Dispose();
        Source = null;
    }
}
