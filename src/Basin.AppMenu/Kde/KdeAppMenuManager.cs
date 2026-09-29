using Basin.AppMenu.Protocol;
using Wayland.Server;

namespace Basin.AppMenu;

public sealed class KdeAppMenuManager : IDisposable
{
    public const int Version = 2;

    private readonly WlGlobal _global;
    private readonly CompositorGlobal _compositor;
    private readonly AppMenuAddresses _addresses;

    public KdeAppMenuManager(WlServerDisplay display, CompositorGlobal compositor, AppMenuAddresses addresses)
    {
        ArgumentNullException.ThrowIfNull(display);
        _compositor = compositor ?? throw new ArgumentNullException(nameof(compositor));
        _addresses = addresses ?? throw new ArgumentNullException(nameof(addresses));
        _global = display.CreateGlobal(OrgKdeKwinAppmenuManager.Interface, Version, OnBind);
    }

    public void Dispose() => _global.Dispose();

    private void OnBind(WlClient client, uint version, uint id)
    {
        var manager = new OrgKdeKwinAppmenuManagerResource(client, version, id);
        manager.Create += (_, e) =>
        {
            var menu = new OrgKdeKwinAppmenuResource(client, manager.Version, e.Id);
            var surface = _compositor.ResolveSurface(e.Surface);
            if (surface is null)
            {
                return;
            }

            menu.SetAddress += (_, address) =>
            {
                if (surface.IsDestroyed)
                {
                    return;
                }

                _addresses.Set(surface, address.ServiceName.Length == 0 || address.ObjectPath.Length == 0
                    ? null
                    : AppMenuAddress.DbusMenu(address.ServiceName, address.ObjectPath));
            };
            menu.Destroyed += (_, _) =>
            {
                if (!surface.IsDestroyed && _addresses.AddressOf(surface)?.Kind == AppMenuAddressKind.DbusMenu)
                {
                    _addresses.Set(surface, null);
                }
            };
        };
    }
}
