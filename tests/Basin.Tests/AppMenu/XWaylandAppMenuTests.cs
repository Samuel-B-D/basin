using Basin.XWayland;
using Xunit;

namespace Basin.Tests;

public sealed class XWaylandAppMenuTests
{
    [Fact]
    public void An_x11_window_reports_its_kde_appmenu_properties_and_follows_changes()
    {
        Assert.SkipUnless(PrivateBus.OnPath("Xwayland") && PrivateBus.OnPath("xeyes") && PrivateBus.OnPath("xprop"),
            "Xwayland, xeyes and xprop are needed");
        using var host = new CompositorTestHost();
        using var shell = new XwaylandShellGlobal(host.Display, host.Compositor);
        using var server = new XWaylandServer(host.Display, host.Loop);
        XWaylandWm? wm = null;
        XWaylandWindow? window = null;
        server.Ready += fd =>
        {
            wm = new XWaylandWm(fd, host.Loop, shell, host.Seat);
            wm.WindowMapped += mapped => window ??= mapped;
        };

        using var eyes = XWaylandTestSupport.Start("xeyes", server.DisplayName);
        try
        {
            XWaylandTestSupport.Pump(host, () => window is not null);
            Assert.NotNull(window);
            Assert.Equal("", window.AppMenuServiceName);
            var changes = 0;
            window.AppMenuChanged += () => changes++;

            var id = $"0x{window.WindowId:x}";
            XWaylandTestSupport.Run(host, server.DisplayName, "-id", id, "-f", "_KDE_NET_WM_APPMENU_SERVICE_NAME", "8s", "-set", "_KDE_NET_WM_APPMENU_SERVICE_NAME", ":1.77");
            XWaylandTestSupport.Run(host, server.DisplayName, "-id", id, "-f", "_KDE_NET_WM_APPMENU_OBJECT_PATH", "8s", "-set", "_KDE_NET_WM_APPMENU_OBJECT_PATH", "/MenuBar/3");
            XWaylandTestSupport.Pump(host, () => window.AppMenuObjectPath == "/MenuBar/3");

            Assert.Equal(":1.77", window.AppMenuServiceName);
            Assert.Equal("/MenuBar/3", window.AppMenuObjectPath);
            Assert.True(changes >= 1);
        }
        finally
        {
            eyes.Kill();
            eyes.WaitForExit(2000);
            wm?.Dispose();
        }
    }

}
