using Basin.XWayland;
using Xunit;

namespace Basin.Tests;

public sealed class XWaylandPropertyTests
{
    [Fact]
    public void A_title_set_after_map_reaches_the_window()
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
            XWaylandTestSupport.Run(host, server.DisplayName, "-id", $"0x{window!.WindowId:x}", "-f", "_NET_WM_NAME", "8u", "-set", "_NET_WM_NAME", "renamed");
            XWaylandTestSupport.Pump(host, () => window.Title == "renamed");
        }
        finally
        {
            eyes.Kill();
            eyes.WaitForExit(2000);
            wm?.Dispose();
        }
    }
}
