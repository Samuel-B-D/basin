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

    private void ApplyAddress(ShellWindow window) => Import(window, AppMenuImport.Resolve(
        _addresses,
        window.Surface,
        window.X11?.AppMenuServiceName,
        window.X11?.AppMenuObjectPath,
        _registrar,
        window.X11?.WindowId));

    private void Import(ShellWindow window, AppMenuAddress? address)
    {
        if (window.MenuImport is null)
        {
            if (address is null)
            {
                return;
            }

            var import = new AppMenuImport(MenuBus, _toplevels, window.Xdg);
            import.Ready += reader =>
            {
                BasinReport.Line($"APPMENU {window.Id} {(reader.Kind == AppMenuAddressKind.GMenu ? "gmenu" : "dbusmenu")} titles={reader.Menu.Items.Count}");
                RefreshMenusFor(window);
            };
            import.Failed += _ =>
            {
                BasinReport.Line($"APPMENU {window.Id} failed, synthesized");
                RefreshMenusFor(window);
            };
            import.Cleared += () => RefreshMenusFor(window);
            window.MenuImport = import;
        }

        window.MenuImport.Import(address);
    }

    private void RefreshMenusFor(ShellWindow window)
    {
        if (ReferenceEquals(_front, window.App))
        {
            RefreshMenus();
        }
    }
}
