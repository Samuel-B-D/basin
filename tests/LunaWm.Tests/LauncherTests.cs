using Xunit;

namespace LunaWm.Tests;

public sealed class LauncherTests
{
    private static bool Starts(string line, string prefix) => line.StartsWith(prefix, StringComparison.Ordinal);

    private static string WlClient(string name)
    {
        var root = AppContext.BaseDirectory;
        for (var i = 0; i < 10 && root is not null; i++)
        {
            var candidate = Path.Combine(root, "scripts", "wlclients", "bin", name);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            root = Path.GetDirectoryName(root.TrimEnd(Path.DirectorySeparatorChar));
        }

        Assert.Skip($"scripts/wlclients/bin/{name} has not been built");
        return null!;
    }

    private const string Config = """
        [quick_launch]
        apps = ["one.desktop", "two.desktop"]

        [[launcher.tab]]
        name = "Apps"

        [[launcher.tab]]
        name = "Settings"
        categories = ["Settings"]

        [notifications]
        sound = false
        """;

    private static void Entries(TempConfig config, string exec)
    {
        var applications = Path.Combine(config.Directory, "data", "applications");
        Directory.CreateDirectory(applications);
        File.WriteAllText(Path.Combine(applications, "one.desktop"), $"[Desktop Entry]\nType=Application\nName=One\nExec={exec}\n");
        File.WriteAllText(Path.Combine(applications, "two.desktop"), $"[Desktop Entry]\nType=Application\nName=Two\nExec={exec}\n");
        File.WriteAllText(Path.Combine(applications, "panel.desktop"), $"[Desktop Entry]\nType=Application\nName=Panel\nExec={exec}\nCategories=Settings;\n");
    }

    [Fact]
    public async Task The_launcher_lists_its_tabs_launches_an_icon_and_saves_nothing_until_changed()
    {
        var ssdwin = WlClient("ssdwin");
        using var config = new TempConfig(Config);
        Entries(config, ssdwin);
        using var session = new LunaSession(LunaSession.Compositor(), config.Path, config.Environment);
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "SOCKET ")));
        await session.SendAsync("launcher");
        Assert.NotNull(await session.WaitForAsync(line => line == "LAUNCHER on"));
        Assert.NotNull(await session.WaitForAsync(line => line == "  tab Apps: one.desktop,two.desktop", poke: "where", fresh: true));
        Assert.NotNull(await session.WaitForAsync(line => line == "  tab Settings: panel.desktop", poke: "where", fresh: true));
        await session.SendAsync("launcher-tab settings");
        Assert.NotNull(await session.WaitForAsync(line => line == "LAUNCHERTAB Settings icons=1"));

        await session.SendAsync("launcher-launch Two");
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "LAUNCH Two pid=")));
        Assert.NotNull(await session.WaitForAsync(line => line == "LAUNCHER off"));
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "CARD + 1 ssdwin stack=0")));
        Assert.False(File.Exists(Path.Combine(config.Directory, "state", "luna-wm", "launcher.json")));
        await session.SendAsync("quit");
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "FRAMES ")));
    }

    [Fact]
    public async Task Card_view_shows_the_dock_and_a_held_swipe_launches_from_it()
    {
        var ssdwin = WlClient("ssdwin");
        using var config = new TempConfig(Config);
        Entries(config, ssdwin);
        using var session = new LunaSession(LunaSession.Compositor(), config.Path, config.Environment);
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "SOCKET ")));
        Assert.NotNull(await session.WaitForAsync(line => line == "  dock shown one.desktop,two.desktop", poke: "where", fresh: true));

        await session.SendAsync("launcher-launch One");
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "CARD + 1 ")));
        Assert.NotNull(await session.WaitForAsync(line => line == "  dock hidden one.desktop,two.desktop", poke: "where", fresh: true));

        await session.SendAsync("edge bottom 0.05 hold 600");
        Assert.NotNull(await session.WaitForAsync(line => line == "QUICKLAUNCH hold"));
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "LAUNCH Two pid=")));
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "CARD + 2 ssdwin stack=1")));
        await session.SendAsync("quit");
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "FRAMES ")));
    }

    [Fact]
    public async Task A_launch_shows_a_ghost_in_its_icon_color_with_its_icon()
    {
        using var config = new TempConfig(Config);
        var applications = Path.Combine(config.Directory, "data", "applications");
        Directory.CreateDirectory(applications);
        var icon = Path.Combine(config.Directory, "red.svg");
        File.WriteAllText(icon, "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"64\" height=\"64\"><rect width=\"64\" height=\"64\" fill=\"#ff0000\"/></svg>");
        File.WriteAllText(Path.Combine(applications, "slow.desktop"), $"[Desktop Entry]\nType=Application\nName=Slow\nExec=sleep 5\nIcon={icon}\n");
        using var session = new LunaSession(LunaSession.Compositor(), config.Path, config.Environment);
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "SOCKET ")));
        await session.SendAsync("launcher-launch Slow");
        Assert.NotNull(await session.WaitForAsync(line => line == "GHOST Slow icon=128 color=#e97777"));
        await session.SendAsync("quit");
        var frames = await session.WaitForAsync(line => Starts(line, "FRAMES "));
        Assert.NotNull(frames);
        var fields = frames!.Split(' ');
        if (fields[3] != "untracked")
        {
            Assert.Equal("0", fields[3]);
        }
    }
}
