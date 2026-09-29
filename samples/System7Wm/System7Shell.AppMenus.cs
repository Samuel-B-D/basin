using Basin;
using Basin.AppMenu;
using Basin.Diagnostics;

namespace System7Wm;

internal sealed partial class System7Shell
{
    private readonly AppMenuAddresses _addresses = new() { ShowsMenuBar = true };
    private AppMenuBus? _menuBus;
    private AppMenuRegistrar? _registrar;

    private void InstallShellModules()
    {
        _services.Use(_addresses);
        _services.Install(AppMenuPack.Default);
    }

    private AppMenuBus MenuBus => _menuBus ??= new AppMenuBus(_host.Loop);

    internal ShellWindow? ActiveOf(AppGroup app) =>
        _active is { } active && ReferenceEquals(active.App, app) ? active : app.Windows.LastOrDefault();

    private void AttachAddresses()
    {
        if (MenuBus.HasAddress)
        {
            _registrar = new AppMenuRegistrar(MenuBus);
            _registrar.Changed += windowId =>
            {
                foreach (var window in _stack)
                {
                    if (window.X11 is { } x11 && x11.WindowId == windowId && window.Mapped)
                    {
                        ApplyAddress(window);
                    }
                }
            };
        }

        _addresses.Changed += (surface, address) =>
        {
            if (WindowOf(surface) is { Mapped: true } window && ReferenceEquals(window.Surface, surface))
            {
                Import(window, address);
            }
        };
    }

    private void ApplyAddress(ShellWindow window)
    {
        if (window.Surface is { } surface && _addresses.AddressOf(surface) is { } address)
        {
            Import(window, address);
        }
        else if (window.X11 is { AppMenuServiceName.Length: > 0, AppMenuObjectPath.Length: > 0 } x11)
        {
            Import(window, AppMenuAddress.DbusMenu(x11.AppMenuServiceName, x11.AppMenuObjectPath));
        }
        else if (window.X11 is { } registered && _registrar?.AddressOf(registered.WindowId) is { } registration)
        {
            Import(window, registration);
        }
        else if (window.X11 is not null)
        {
            Import(window, null);
        }
    }

    private void Import(ShellWindow window, AppMenuAddress? address)
    {
        if (window.ImportedAddress == address)
        {
            return;
        }

        window.Imported?.Dispose();
        window.Imported = null;
        window.ImportedAddress = address;
        if (address is not null && window.Xdg is { } xdg && address.Kind == AppMenuAddressKind.DbusMenu)
        {
            _toplevels?.SetAppMenu(xdg, address.BusName, address.ObjectPath);
        }

        if (address is null)
        {
            RefreshMenusFor(window);
            return;
        }

        IAppMenuSource reader = address.Kind == AppMenuAddressKind.GMenu
            ? new GMenuReader(MenuBus, address)
            : new DbusMenuReader(MenuBus, address.BusName, address.ObjectPath);
        window.Imported = reader;
        var ready = false;
        reader.Menu.Changed += () =>
        {
            if (!ready && reader.IsReady && ReferenceEquals(window.Imported, reader))
            {
                ready = true;
                BasinReport.Line($"APPMENU {window.Id} {(address.Kind == AppMenuAddressKind.GMenu ? "gmenu" : "dbusmenu")} titles={reader.Menu.Items.Count}");
                RefreshMenusFor(window);
            }
        };
        reader.Failed += () =>
        {
            if (!ReferenceEquals(window.Imported, reader))
            {
                return;
            }

            BasinReport.Line($"APPMENU {window.Id} failed, synthesized");
            window.Imported = null;
            reader.Dispose();
            RefreshMenusFor(window);
        };
    }

    private void RefreshMenusFor(ShellWindow window)
    {
        if (ReferenceEquals(_front, window.App))
        {
            RefreshMenus();
        }
    }
}
