using System.Diagnostics;
using Xunit;

namespace LunaWm.Tests;

public sealed class NotificationTests
{
    private static bool Starts(string line, string prefix) => line.StartsWith(prefix, StringComparison.Ordinal);

    private static Process Send(string address, params string[] arguments)
    {
        var info = new ProcessStartInfo(LunaSession.Client("notify-send")) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        info.Environment["DBUS_SESSION_BUS_ADDRESS"] = address;
        return Process.Start(info)!;
    }

    private static async Task<LunaSession> Start(TempConfig config, PrivateBus bus, PrivateBus system)
    {
        var environment = config.Environment;
        environment["DBUS_SESSION_BUS_ADDRESS"] = bus.Address;
        environment["DBUS_SYSTEM_BUS_ADDRESS"] = system.Address;
        var session = new LunaSession(LunaSession.Compositor(), config.Path, environment);
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "NOTIFICATIONS on ")));
        return session;
    }

    [Fact]
    public async Task A_notification_banners_lands_in_the_dashboard_and_a_dismiss_closes_it()
    {
        using var bus = PrivateBus.Start();
        using var system = PrivateBus.Start();
        using var config = new TempConfig("[quick_launch]\napps = []\n");
        using var session = await Start(config, bus, system);
        using (var sent = Send(bus.Address, "-a", "Mail", "Hello", "From the test"))
        {
            Assert.True(sent.WaitForExit(5000));
        }

        Assert.NotNull(await session.WaitForAsync(line => line == "NOTIFY + 1 Mail normal Hello"));
        Assert.NotNull(await session.WaitForAsync(line => line == "BANNER 1 Hello"));
        Assert.NotNull(await session.WaitForAsync(line => line == "SOUND banner muted"));
        await session.SendAsync("dashboard");
        Assert.NotNull(await session.WaitForAsync(line => line == "DASHBOARD on"));
        Assert.NotNull(await session.WaitForAsync(line => line == "  group Mail count=1 Hello", poke: "where", fresh: true));
        await session.SendAsync("dismiss 1");
        Assert.NotNull(await session.WaitForAsync(line => line == "NOTIFY - 1 2"));
        Assert.NotNull(await session.WaitForAsync(line => line == "BANNER end 1"));
        await session.SendAsync("notify Local summary");
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "NOTIFY posted ")));
        await session.SendAsync("quit");
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "FRAMES ")));
    }

    [Fact]
    public async Task Tapping_a_dashboard_row_invokes_its_default_action_in_the_client()
    {
        using var bus = PrivateBus.Start();
        using var system = PrivateBus.Start();
        using var config = new TempConfig("[quick_launch]\napps = []\n");
        using var session = await Start(config, bus, system);
        using var sent = Send(bus.Address, "-a", "Mail", "-A", "default=Open", "Open me");
        Assert.NotNull(await session.WaitForAsync(line => line == "NOTIFY + 1 Mail normal Open me"));
        await session.SendAsync("dashboard");
        Assert.NotNull(await session.WaitForAsync(line => line == "DASHBOARD on"));
        await session.SettleAsync();
        await session.SendAsync("touch 0.85 0.07");
        Assert.NotNull(await session.WaitForAsync(line => line == "NOTIFY invoke 1 default"));
        Assert.True(sent.WaitForExit(5000));
        Assert.Equal("default", sent.StandardOutput.ReadToEnd().Trim());
        Assert.NotNull(await session.WaitForAsync(line => line == "NOTIFY - 1 2"));
        await session.SendAsync("quit");
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "FRAMES ")));
    }

    [Fact]
    public async Task A_critical_notification_raises_an_alert_whose_button_reaches_the_client()
    {
        using var bus = PrivateBus.Start();
        using var system = PrivateBus.Start();
        using var config = new TempConfig("[quick_launch]\napps = []\n");
        using var session = await Start(config, bus, system);
        using var sent = Send(bus.Address, "-a", "Clock", "-u", "critical", "-A", "snooze=Snooze", "-A", "stop=Stop", "Alarm", "Wake up");
        Assert.NotNull(await session.WaitForAsync(line => line == "ALERT 1 Alarm"));
        Assert.NotNull(await session.WaitForAsync(line => line == "SOUND alert muted"));
        var state = await session.WaitForAsync(line => Starts(line, "  notifications "), poke: "where", fresh: true);
        Assert.Contains("alert=1", state!, StringComparison.Ordinal);
        await session.SettleAsync();
        await session.SendAsync("touch 0.35 0.16");
        Assert.NotNull(await session.WaitForAsync(line => line == "ALERT chose 1 snooze"));
        Assert.True(sent.WaitForExit(5000));
        Assert.Equal("snooze", sent.StandardOutput.ReadToEnd().Trim());
        await session.SendAsync("quit");
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "FRAMES ")));
    }

    [Fact]
    public async Task Image_data_and_an_image_path_become_the_notification_icon()
    {
        var gdbus = LunaSession.Client("gdbus");
        using var bus = PrivateBus.Start();
        using var system = PrivateBus.Start();
        using var config = new TempConfig("[quick_launch]\napps = []\n");
        using var session = await Start(config, bus, system);
        var info = new ProcessStartInfo(gdbus) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[]
        {
            "call", "--session", "--dest", "org.freedesktop.Notifications", "--object-path", "/org/freedesktop/Notifications",
            "--method", "org.freedesktop.Notifications.Notify", "--", "Photos", "0", "", "Picture", "With pixels", "[]",
            "{'image-data': <(2, 2, 6, false, 8, 3, [byte 255, 0, 0, 0, 255, 0, 0, 0, 255, 255, 255, 255])>}", "-1",
        })
        {
            info.ArgumentList.Add(argument);
        }

        info.Environment["DBUS_SESSION_BUS_ADDRESS"] = bus.Address;
        using (var call = Process.Start(info)!)
        {
            Assert.True(call.WaitForExit(5000));
            Assert.Equal(0, call.ExitCode);
        }

        Assert.NotNull(await session.WaitForAsync(line => line == "NOTIFY + 1 Photos normal Picture"));
        Assert.NotNull(await session.WaitForAsync(line => line == "NOTIFY image 1 2x2"));
        await session.SendAsync("dismiss 1");
        Assert.NotNull(await session.WaitForAsync(line => line == "NOTIFY - 1 2"));

        var picture = Path.Combine(config.Directory, "picture.svg");
        File.WriteAllText(picture, "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"16\" height=\"16\"><rect width=\"16\" height=\"16\" fill=\"#2071bb\"/></svg>");
        using (var sent = Send(bus.Address, "-a", "Photos", "-h", $"string:image-path:file://{picture}", "From a file"))
        {
            Assert.True(sent.WaitForExit(5000));
        }

        Assert.NotNull(await session.WaitForAsync(line => line == "NOTIFY image 2 path"));
        await session.SendAsync("quit");
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "FRAMES ")));
    }

    [Fact]
    public async Task A_swipe_down_from_the_right_of_the_status_bar_pulls_the_dashboard()
    {
        using var bus = PrivateBus.Start();
        using var system = PrivateBus.Start();
        using var config = new TempConfig();
        using var session = await Start(config, bus, system);
        await session.SettleAsync();
        await session.SendAsync("touchdrag 0.9 0.015 0 0.25 200");
        Assert.NotNull(await session.WaitForAsync(line => line == "BARPRESS pull"));
        Assert.NotNull(await session.WaitForAsync(line => line == "DASHBOARD on"));
        await session.SendAsync("gesture back");
        Assert.NotNull(await session.WaitForAsync(line => line == "DASHBOARD off"));
        await session.SettleAsync();
        var mark = session.Mark();
        await session.SendAsync("move 900 12");
        await session.SendAsync("button 272 1");
        await session.SendAsync("move 900 40");
        await session.SendAsync("move 900 80");
        await session.SendAsync("button 272 0");
        Assert.NotNull(await session.WaitForAsync(line => line == "DASHBOARD on", since: mark));
        Assert.Null(await session.WaitForAsync(line => line.StartsWith("MENU ", StringComparison.Ordinal), since: mark, attempts: 5));
        await session.SendAsync("quit");
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "FRAMES ")));
    }
}
