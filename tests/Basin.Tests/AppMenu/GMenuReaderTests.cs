using Basin.AppMenu;
using Xunit;

namespace Basin.Tests;

public sealed class GMenuReaderTests
{
    private static AppMenuAddress AddressOf(GMenuExporter exporter) => new(
        AppMenuAddressKind.GMenu,
        exporter.UniqueName,
        MenubarPath: "/org/basin/appmenu/menus/menubar",
        ApplicationPath: "/org/basin/appmenu",
        WindowPath: "/org/basin/appmenu/window/1");

    [Fact]
    public void Glib_menus_become_titles_with_sections_as_separators()
    {
        using var daemon = PrivateBus.Start();
        using var exporter = GMenuExporter.Start(daemon);
        using var host = new CompositorTestHost();
        using var bus = new AppMenuBus(host.Loop, daemon.Address);
        using var reader = new GMenuReader(bus, AddressOf(exporter));
        PortalBusTests.PumpUntil(host, () => reader.IsReady && reader.Menu.Items.Count == 2);

        var file = reader.Menu.Items[0];
        Assert.Equal("File", file.Label);
        Assert.True(file.IsSubmenu);
        Assert.Equal(["Open…", "Print", "", "Quit"], file.Children.Select(c => c.Label));
        Assert.Equal(AppMenuItemKind.Separator, file.Children[2].Kind);
        Assert.Equal(new AppMenuShortcut(AppMenuModifiers.Control, "o"), file.Children[0].Shortcut);
        Assert.True(file.Children[0].Enabled);
        Assert.False(file.Children[1].Enabled);
        var wrap = reader.Menu.Items[1].Children[1];
        Assert.Equal(AppMenuItemKind.Check, wrap.Kind);
        Assert.False(wrap.ToggleState);
    }

    [Fact]
    public void Activation_reaches_app_and_window_actions_and_state_follows()
    {
        using var daemon = PrivateBus.Start();
        using var exporter = GMenuExporter.Start(daemon);
        using var host = new CompositorTestHost();
        using var bus = new AppMenuBus(host.Loop, daemon.Address);
        using var reader = new GMenuReader(bus, AddressOf(exporter));
        PortalBusTests.PumpUntil(host, () => reader.IsReady && reader.Menu.Items.Count == 2);

        reader.Activate(reader.Menu.Items[0].Children[3], 0);
        reader.Activate(reader.Menu.Items[1].Children[1], 0);
        PortalBusTests.PumpUntil(host, () => exporter.Activated.Count == 2);
        Assert.Equal(["quit", "wrap"], exporter.Activated);
        PortalBusTests.PumpUntil(host, () => reader.Menu.Items[1].Children[1].ToggleState);
    }

    [Fact]
    public void A_gmenu_address_that_nothing_answers_fails()
    {
        using var daemon = PrivateBus.Start();
        using var host = new CompositorTestHost();
        using var bus = new AppMenuBus(host.Loop, daemon.Address);
        using var reader = new GMenuReader(bus, new AppMenuAddress(AppMenuAddressKind.GMenu, ":1.99", MenubarPath: "/nope"));
        var failed = false;
        reader.Failed += () => failed = true;
        PortalBusTests.PumpUntil(host, () => failed);
    }
}
