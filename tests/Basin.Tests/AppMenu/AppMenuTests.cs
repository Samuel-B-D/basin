using Basin.AppMenu;
using Xunit;

namespace Basin.Tests;

public sealed class AppMenuTests
{
    [Theory]
    [InlineData("_File", "File")]
    [InlineData("Save _As…", "Save As…")]
    [InlineData("snake__case", "snake_case")]
    [InlineData("No marker", "No marker")]
    [InlineData("_", "")]
    public void Mnemonic_markers_are_removed_and_doubled_ones_kept(string label, string expected) =>
        Assert.Equal(expected, AppMenuLabels.StripMnemonic(label));

    [Fact]
    public void Dbusmenu_shortcuts_read_modifiers_and_one_key()
    {
        var shortcut = AppMenuShortcut.FromDbusMenu(["Control", "Shift", "Q"]);
        Assert.Equal(new AppMenuShortcut(AppMenuModifiers.Control | AppMenuModifiers.Shift, "q"), shortcut);
        Assert.Null(AppMenuShortcut.FromDbusMenu(["Control"]));
    }

    [Fact]
    public void Gtk_accelerators_read_primary_as_control()
    {
        Assert.Equal(new AppMenuShortcut(AppMenuModifiers.Control, "w"), AppMenuShortcut.FromGtkAccelerator("<Primary>w"));
        Assert.Equal(new AppMenuShortcut(AppMenuModifiers.Control | AppMenuModifiers.Shift, "F5"), AppMenuShortcut.FromGtkAccelerator("<Control><Shift>F5"));
        Assert.Null(AppMenuShortcut.FromGtkAccelerator("<Primary>"));
    }

    [Fact]
    public void Kde_appmenu_reports_an_address_per_surface_and_release_clears_it()
    {
        using var host = new CompositorTestHost();
        var addresses = new AppMenuAddresses();
        using var manager = new KdeAppMenuManager(host.Display, host.Compositor, addresses);
        var surface = host.Client.Compositor.CreateSurface();
        var proxy = HyprlandTestSupport.Bind<Basin.AppMenu.Protocol.OrgKdeKwinAppmenuManager>(host, "org_kde_kwin_appmenu_manager", 2);
        var changes = 0;
        addresses.Changed += (_, _) => changes++;

        var menu = proxy.Create(surface);
        menu.SetAddress(":1.42", "/MenuBar/1");
        host.PumpToServer();

        var server = host.Compositor.Surfaces.Single();
        Assert.Equal(AppMenuAddress.DbusMenu(":1.42", "/MenuBar/1"), addresses.AddressOf(server));

        menu.SetAddress(":1.42", "/MenuBar/1");
        host.PumpToServer();
        Assert.Equal(1, changes);

        menu.Release();
        host.PumpToServer();
        Assert.Null(addresses.AddressOf(server));
        HyprlandTestSupport.AssertAlive(host);
    }

    [Fact]
    public void An_address_goes_away_with_its_surface()
    {
        using var host = new CompositorTestHost();
        var addresses = new AppMenuAddresses();
        using var manager = new KdeAppMenuManager(host.Display, host.Compositor, addresses);
        var surface = host.Client.Compositor.CreateSurface();
        var proxy = HyprlandTestSupport.Bind<Basin.AppMenu.Protocol.OrgKdeKwinAppmenuManager>(host, "org_kde_kwin_appmenu_manager", 2);
        proxy.Create(surface).SetAddress(":1.7", "/m");
        host.PumpToServer();
        Assert.Equal(1, addresses.Count);

        surface.Destroy();
        host.PumpToServer();
        Assert.Equal(0, addresses.Count);
    }

    [Fact]
    public void Dbusmenu_reader_builds_the_model_and_activates_items()
    {
        using var daemon = PrivateBus.Start();
        using var host = new CompositorTestHost();
        using var server = PortalBusTests.Await(host, TestDbusMenuServer.StartAsync(daemon.Address));
        server.Root.Add(new TestMenuNode(1, "_File")
        {
            Children =
            [
                new TestMenuNode(2, "_Open…") { Shortcut = ["Control", "o"] },
                new TestMenuNode(3, "") { Separator = true },
                new TestMenuNode(4, "_Quit") { Enabled = false },
            ],
        });
        server.Root.Add(new TestMenuNode(5, "_View")
        {
            Children = [new TestMenuNode(6, "Word __Wrap") { Toggle = "checkmark", Checked = true }],
        });

        using var bus = new AppMenuBus(host.Loop, daemon.Address);
        using var reader = new DbusMenuReader(bus, server.UniqueName, TestDbusMenuServer.ObjectPath);
        PortalBusTests.PumpUntil(host, () => reader.IsReady);

        var titles = reader.Menu.Items;
        Assert.Equal(["File", "View"], titles.Select(t => t.Label));
        var file = titles[0];
        Assert.True(file.IsSubmenu);
        Assert.Equal(new AppMenuShortcut(AppMenuModifiers.Control, "o"), file.Children[0].Shortcut);
        Assert.Equal(AppMenuItemKind.Separator, file.Children[1].Kind);
        Assert.False(file.Children[2].Enabled);
        var wrap = titles[1].Children[0];
        Assert.Equal("Word _Wrap", wrap.Label);
        Assert.Equal(AppMenuItemKind.Check, wrap.Kind);
        Assert.True(wrap.ToggleState);

        server.AboutToShowNeedsUpdate = true;
        reader.AboutToShow(file);
        reader.Activate(file.Children[0], 1234);
        PortalBusTests.PumpUntil(host, () => { lock (server.Events) { return server.Events.Count == 1; } });
        Assert.Equal((2, "clicked"), server.Events[0]);
        PortalBusTests.PumpUntil(host, () => { lock (server.AboutToShow) { return server.AboutToShow.Contains(1); } });
    }

    [Fact]
    public void Dbusmenu_reader_rereads_after_layout_updated()
    {
        using var daemon = PrivateBus.Start();
        using var host = new CompositorTestHost();
        using var server = PortalBusTests.Await(host, TestDbusMenuServer.StartAsync(daemon.Address));
        server.Root.Add(new TestMenuNode(1, "File"));
        using var bus = new AppMenuBus(host.Loop, daemon.Address);
        using var reader = new DbusMenuReader(bus, server.UniqueName, TestDbusMenuServer.ObjectPath);
        PortalBusTests.PumpUntil(host, () => reader.IsReady);
        var changed = 0;
        reader.Menu.Changed += () => changed++;

        server.Root.Add(new TestMenuNode(2, "Edit"));
        server.EmitLayoutUpdated();
        PortalBusTests.PumpUntil(host, () => reader.Menu.Items.Count == 2);
        Assert.True(changed >= 1);
    }

    [Fact]
    public void Dbusmenu_reader_fails_when_nothing_answers()
    {
        using var daemon = PrivateBus.Start();
        using var host = new CompositorTestHost();
        using var bus = new AppMenuBus(host.Loop, daemon.Address);
        using var reader = new DbusMenuReader(bus, "org.example.Nobody", "/MenuBar/1");
        var failed = false;
        reader.Failed += () => failed = true;
        PortalBusTests.PumpUntil(host, () => failed);
        Assert.False(reader.IsReady);
    }
}
