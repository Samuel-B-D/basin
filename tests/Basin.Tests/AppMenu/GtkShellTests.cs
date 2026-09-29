using Basin.AppMenu;
using Xunit;

namespace Basin.Tests;

public sealed class GtkShellTests
{
    [Theory]
    [InlineData(true, 2u)]
    [InlineData(false, 0u)]
    public void Capabilities_advertise_the_global_menu_bar_only_when_the_compositor_draws_one(bool shows, uint expected)
    {
        using var host = new CompositorTestHost();
        var addresses = new AppMenuAddresses { ShowsMenuBar = shows };
        using var manager = new GtkShellManager(host.Display, host.Compositor, addresses);
        var proxy = HyprlandTestSupport.Bind<Basin.AppMenu.Protocol.GtkShell1>(host, "gtk_shell1", 5);
        uint? capabilities = null;
        proxy.Capabilities += (_, e) => capabilities = e.Capabilities;
        host.PumpUntil(() => capabilities is not null);

        Assert.Equal(expected, capabilities);
    }

    [Fact]
    public void Turning_the_menu_bar_on_resends_the_capabilities()
    {
        using var host = new CompositorTestHost();
        var addresses = new AppMenuAddresses();
        using var manager = new GtkShellManager(host.Display, host.Compositor, addresses);
        var proxy = HyprlandTestSupport.Bind<Basin.AppMenu.Protocol.GtkShell1>(host, "gtk_shell1", 5);
        var seen = new List<uint>();
        proxy.Capabilities += (_, e) => seen.Add(e.Capabilities);
        host.PumpUntil(() => seen.Count == 1);

        addresses.ShowsMenuBar = true;
        manager.RefreshCapabilities();
        host.Display.FlushClients();
        host.PumpUntil(() => seen.Count == 2);
        Assert.Equal([0u, GtkShellManager.GlobalMenuBar], seen);
    }

    [Fact]
    public void Dbus_properties_become_a_gmenu_address_and_release_clears_it()
    {
        using var host = new CompositorTestHost();
        var addresses = new AppMenuAddresses { ShowsMenuBar = true };
        using var manager = new GtkShellManager(host.Display, host.Compositor, addresses);
        var surface = host.Client.Compositor.CreateSurface();
        var proxy = HyprlandTestSupport.Bind<Basin.AppMenu.Protocol.GtkShell1>(host, "gtk_shell1", 5);
        var gtk = proxy.GetGtkSurface(surface);
        gtk.SetDbusProperties("org.example.App", null, "/org/example/App/menus/menubar", "/org/example/App/window/1", "/org/example/App", ":1.5");
        host.PumpToServer();

        var server = host.Compositor.Surfaces.Single();
        var address = addresses.AddressOf(server);
        Assert.NotNull(address);
        Assert.Equal(AppMenuAddressKind.GMenu, address.Kind);
        Assert.Equal(":1.5", address.BusName);
        Assert.Equal("/org/example/App/menus/menubar", address.MenubarPath);
        Assert.Equal("/org/example/App/window/1", address.WindowPath);
        Assert.Equal("/org/example/App", address.ApplicationPath);

        gtk.Release();
        host.PumpToServer();
        Assert.Null(addresses.AddressOf(server));
        HyprlandTestSupport.AssertAlive(host);
    }

    [Fact]
    public void Modal_present_and_gestures_are_reported_and_a_bad_gesture_is_the_protocols_error()
    {
        using var host = new CompositorTestHost();
        var addresses = new AppMenuAddresses();
        using var manager = new GtkShellManager(host.Display, host.Compositor, addresses);
        var surface = host.Client.Compositor.CreateSurface();
        var proxy = HyprlandTestSupport.Bind<Basin.AppMenu.Protocol.GtkShell1>(host, "gtk_shell1", 5);
        var gtk = proxy.GetGtkSurface(surface);
        var modal = false;
        uint presented = 0;
        GtkShellGesture? gesture = null;
        manager.ModalChanged += (_, on) => modal = on;
        manager.PresentRequested += (_, time) => presented = time;
        manager.TitlebarGesture += (_, g) => gesture = g;

        var seat = host.Client.Seat;
        Assert.NotNull(seat);
        gtk.SetModal();
        gtk.Present(77);
        gtk.TitlebarGesture(1, seat, Basin.AppMenu.Protocol.GtkSurface1.Gesture.DoubleClick);
        host.PumpToServer();
        Assert.True(modal);
        Assert.Equal(77u, presented);
        Assert.Equal(GtkShellGesture.DoubleClick, gesture);
        HyprlandTestSupport.AssertAlive(host);

        gtk.TitlebarGesture(2, seat, (Basin.AppMenu.Protocol.GtkSurface1.Gesture)9);
        HyprlandTestSupport.AssertKilled(host);
    }
}
