using Basin.AppMenu.Protocol;
using Wayland.Server;

namespace Basin.AppMenu;

public sealed class GtkShellManager : IDisposable
{
    public const int Version = 5;

    public const uint GlobalMenuBar = 2;

    private readonly WlGlobal _global;
    private readonly CompositorGlobal _compositor;
    private readonly AppMenuAddresses _addresses;
    private readonly List<GtkShell1Resource> _shells = [];
    private bool _showsMenuBar;

    public GtkShellManager(WlServerDisplay display, CompositorGlobal compositor, AppMenuAddresses addresses)
    {
        ArgumentNullException.ThrowIfNull(display);
        _compositor = compositor ?? throw new ArgumentNullException(nameof(compositor));
        _addresses = addresses ?? throw new ArgumentNullException(nameof(addresses));
        _showsMenuBar = addresses.ShowsMenuBar;
        _global = display.CreateGlobal(GtkShell1.Interface, Version, OnBind);
    }

    public event Action<Surface, uint>? PresentRequested;

    public event Action<Surface, string?>? FocusRequested;

    public event Action<Surface, bool>? ModalChanged;

    public event Action<Surface, GtkShellGesture>? TitlebarGesture;

    public event Action<Surface?>? BellRequested;

    public uint Capabilities => _addresses.ShowsMenuBar ? GlobalMenuBar : 0;

    public void RefreshCapabilities()
    {
        if (_showsMenuBar == _addresses.ShowsMenuBar)
        {
            return;
        }

        _showsMenuBar = _addresses.ShowsMenuBar;
        foreach (var shell in _shells)
        {
            shell.SendCapabilities(Capabilities);
        }
    }

    public void Dispose() => _global.Dispose();

    private void OnBind(WlClient client, uint version, uint id)
    {
        var shell = new GtkShell1Resource(client, version, id);
        _shells.Add(shell);
        shell.Destroyed += (_, _) => _shells.Remove(shell);
        shell.SendCapabilities(Capabilities);
        var surfaces = new Dictionary<GtkSurface1Resource, Surface>();
        shell.GetGtkSurface += (_, e) =>
        {
            var gtk = new GtkSurface1Resource(client, shell.Version, e.GtkSurface);
            if (_compositor.ResolveSurface(e.Surface) is not { } surface)
            {
                return;
            }

            surfaces[gtk] = surface;
            Wire(gtk, surface);
            gtk.Destroyed += (_, _) => surfaces.Remove(gtk);
        };
        shell.SystemBell += (_, e) =>
            BellRequested?.Invoke(e.Surface is GtkSurface1Resource gtk && surfaces.TryGetValue(gtk, out var surface) ? surface : null);
    }

    private void Wire(GtkSurface1Resource gtk, Surface surface)
    {
        gtk.SetDbusProperties += (_, e) =>
        {
            if (surface.IsDestroyed)
            {
                return;
            }

            if (e.UniqueBusName is not { Length: > 0 } bus || (e.MenubarPath is not { Length: > 0 } && e.AppMenuPath is not { Length: > 0 }))
            {
                Clear(surface);
                return;
            }

            _addresses.Set(surface, new AppMenuAddress(
                AppMenuAddressKind.GMenu,
                bus,
                MenubarPath: e.MenubarPath ?? "",
                AppMenuPath: e.AppMenuPath ?? "",
                ApplicationPath: e.ApplicationObjectPath ?? "",
                WindowPath: e.WindowObjectPath ?? ""));
        };
        gtk.SetModal += (_, _) => ModalChanged?.Invoke(surface, true);
        gtk.UnsetModal += (_, _) => ModalChanged?.Invoke(surface, false);
        gtk.Present += (_, e) => PresentRequested?.Invoke(surface, e.Time);
        gtk.RequestFocus += (_, e) => FocusRequested?.Invoke(surface, e.StartupId);
        gtk.TitlebarGesture += (_, e) =>
        {
            if ((uint)e.Gesture is < 1 or > 3)
            {
                gtk.PostError((uint)GtkSurface1.Error.InvalidGesture, "unknown titlebar gesture");
                return;
            }

            TitlebarGesture?.Invoke(surface, (GtkShellGesture)(uint)e.Gesture);
        };
        gtk.Destroyed += (_, _) => Clear(surface);
    }

    private void Clear(Surface surface)
    {
        if (!surface.IsDestroyed && _addresses.AddressOf(surface)?.Kind == AppMenuAddressKind.GMenu)
        {
            _addresses.Set(surface, null);
        }
    }
}
