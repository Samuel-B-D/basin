using Xunit;

namespace System7Wm.Tests;

public sealed class TerminalTests
{
    private static async Task<Session> StartAsync(IReadOnlyDictionary<string, string>? environment = null, params string[] extra)
    {
        var session = new Session(Prerequisites.Compositor(), "pixman", environment, extra);
        await session.DisplayAsync();
        return session;
    }

    private static async Task<string> OpenAsync(Session session, string command)
    {
        var mark = session.Mark();
        await session.SendAsync(command);
        var opened = await session.WaitForPrefixAsync("TERMINAL + ", since: mark);
        Assert.NotNull(opened);
        return opened!.Split(' ')[2];
    }

    private static async Task ChordAsync(Session session, params int[] keys)
    {
        foreach (var key in keys)
        {
            await session.SendAsync($"key {key} 1");
        }

        for (var i = keys.Length - 1; i >= 0; i--)
        {
            await session.SendAsync($"key {keys[i]} 0");
        }
    }

    private static async Task<string> WaitForFileAsync(string path)
    {
        for (var i = 0; i < 100 && (!File.Exists(path) || new FileInfo(path).Length == 0); i++)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        await Task.Delay(100, TestContext.Current.CancellationToken);
        return File.Exists(path) ? File.ReadAllText(path) : "";
    }

    [Fact]
    public async Task The_child_starts_on_a_sized_terminal_with_the_sessions_environment()
    {
        using var session = await StartAsync();
        var seen = Path.Combine(session.Home, "seen");
        var id = await OpenAsync(session, $"terminal sh -c 'echo \"$TERM $TERM_PROGRAM [$COLORTERM] $WAYLAND_DISPLAY $(tty) $(stty size) $(pwd)\" > {seen}; sleep 30'");
        var line = (await WaitForFileAsync(seen)).Trim();
        var display = await session.DisplayAsync();

        Assert.StartsWith($"xterm-256color system7-wm [] {display} /dev/pts/", line, StringComparison.Ordinal);
        Assert.EndsWith($" 24 80 {session.Home}", line, StringComparison.Ordinal);
        Assert.Contains($"TERMINAL + {id} ", await session.WaitForPrefixAsync($"TERMINAL + {id} "), StringComparison.Ordinal);
        await session.QuitCleanlyAsync();
    }

    [Fact]
    public async Task Exit_status_zero_closes_the_window_and_any_other_keeps_it_with_its_line()
    {
        using var session = await StartAsync();
        var closed = await OpenAsync(session, "terminal sh -c 'printf done; exit 0'");
        Assert.NotNull(await session.WaitForLineAsync($"TERMINAL exit {closed} status=0 closed"));
        Assert.NotNull(await session.WaitForLineAsync($"TERMINAL - {closed}"));

        var kept = await OpenAsync(session, "terminal sh -c 'exit 3'");
        Assert.NotNull(await session.WaitForLineAsync($"TERMINAL exit {kept} status=3 kept"));
        var killed = await OpenAsync(session, "terminal sh -c 'kill -TERM $$'");
        Assert.NotNull(await session.WaitForLineAsync($"TERMINAL exit {killed} signal=SIGTERM kept"));

        await session.SendAsync($"activate {kept}");
        await session.SendAsync("menu Edit/Select All");
        await session.SendAsync("menu Edit/Copy");
        var text = await session.WaitForAsync(line => line.Contains("[Process exited: 3]", StringComparison.Ordinal), poke: "ipc clipboard/read {}");
        Assert.NotNull(text);

        var shells = (await session.StateAsync()).Where(line => line.StartsWith("SHELL ", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, shells.Count);
        await session.SendAsync($"terminal-close {kept}");
        Assert.NotNull(await session.WaitForLineAsync($"TERMINAL - {kept}"));
        await session.QuitCleanlyAsync();
    }

    [Fact]
    public async Task The_osc_title_names_the_window()
    {
        using var session = await StartAsync();
        var id = await OpenAsync(session, "terminal sh -c 'printf \"\\033]0;my title\\007\"; sleep 30'");
        Assert.NotNull(await session.WaitForLineAsync($"TERMINAL title {id} \"my title\""));
        var window = await session.StateLineAsync($"WINDOW {id} ");
        Assert.EndsWith("\"my title\"", window!, StringComparison.Ordinal);
        await session.QuitCleanlyAsync();
    }

    [Fact]
    public async Task A_bell_plays_the_alert_sound()
    {
        var shared = Directory.CreateTempSubdirectory("system7-wm-bell-");
        try
        {
            var played = Path.Combine(shared.FullName, "played");
            var player = Path.Combine(shared.FullName, "pw-play");
            File.WriteAllText(player, $"#!/bin/sh\necho \"$1\" >> '{played}'\n");
            File.SetUnixFileMode(player, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            using var session = await StartAsync(new Dictionary<string, string> { ["PATH"] = $"{shared.FullName}:{Environment.GetEnvironmentVariable("PATH")}", ["BASIN_MUTE"] = "" });
            await OpenAsync(session, "terminal sh -c 'printf \"\\007\"; sleep 30'");
            Assert.NotNull(await session.WaitForLineAsync("BEEP Simple Beep"));
            Assert.EndsWith("alert-Simple-Beep.wav", (await WaitForFileAsync(played)).Trim(), StringComparison.Ordinal);
            await session.QuitCleanlyAsync();
        }
        finally
        {
            shared.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Keys_reach_the_child_and_option_sends_escape()
    {
        using var session = await StartAsync();
        var keys = Path.Combine(session.Home, "keys");
        await OpenAsync(session, $"terminal sh -c 'stty raw -echo; head -c 6 | od -An -tx1 > {keys}; sleep 30'");
        await Task.Delay(500, TestContext.Current.CancellationToken);
        await ChordAsync(session, 38);
        await ChordAsync(session, 31);
        await ChordAsync(session, 28);
        await ChordAsync(session, 29, 46);
        await ChordAsync(session, 56, 45);
        Assert.Equal("6c 73 0d 03 1b 78", (await WaitForFileAsync(keys)).Trim());
        await session.QuitCleanlyAsync();
    }

    [Fact]
    public async Task Command_c_and_command_v_go_through_the_clipboard()
    {
        using var session = await StartAsync();
        var pasted = Path.Combine(session.Home, "pasted");
        await OpenAsync(session, $"terminal sh -c 'printf copied; stty raw -echo; head -c 6 > {pasted}; sleep 30'");
        await Task.Delay(500, TestContext.Current.CancellationToken);
        await ChordAsync(session, 125, 30);
        Assert.NotNull(await session.WaitForLineAsync("CHORD a"));
        await ChordAsync(session, 125, 46);
        Assert.NotNull(await session.WaitForLineAsync("CHORD c"));
        Assert.NotNull(await session.WaitForAsync(line => line.Contains("copied", StringComparison.Ordinal) && line.Contains("text", StringComparison.Ordinal), poke: "ipc clipboard/read {}"));

        await session.SendAsync("ipc clipboard/write {\"text\":\"pasted\"}");
        await Task.Delay(300, TestContext.Current.CancellationToken);
        await ChordAsync(session, 125, 47);
        Assert.NotNull(await session.WaitForLineAsync("CHORD v"));
        Assert.Equal("pasted", await WaitForFileAsync(pasted));
        await session.QuitCleanlyAsync();
    }

    [Fact]
    public async Task The_chord_opens_a_terminal()
    {
        using var session = await StartAsync();
        await ChordAsync(session, 125, 56, 20);
        Assert.NotNull(await session.WaitForLineAsync("CHORD new_terminal"));
        Assert.NotNull(await session.WaitForPrefixAsync("TERMINAL + "));
        Assert.Contains("FRONT terminal ", await session.StateLineAsync("FRONT "), StringComparison.Ordinal);
        await session.QuitCleanlyAsync();
    }

    [Fact]
    public async Task The_font_menu_changes_the_front_window_and_the_next_default()
    {
        using var session = await StartAsync();
        var id = await OpenAsync(session, "terminal");
        await session.SendAsync("menu Font/Monaco 12");
        Assert.NotNull(await session.WaitForLineAsync($"TERMINAL font {id} 12"));
        var state = Path.Combine(session.Home, ".local", "state", "system7-wm", "terminal.toml");
        Assert.Contains("font_size = 12", await WaitForFileAsync(state), StringComparison.Ordinal);
        await session.QuitCleanlyAsync();
    }

    [Theory]
    [InlineData("builtin")]
    [InlineData("TERMINAL")]
    [InlineData("config")]
    public async Task A_terminal_desktop_entry_runs_in_the_terminal_the_ladder_picks(string ladder)
    {
        var shared = Directory.CreateTempSubdirectory("system7-wm-ladder-");
        try
        {
            var ran = Path.Combine(shared.FullName, "ran");
            var stub = Path.Combine(shared.FullName, "stubterm");
            File.WriteAllText(stub, $"#!/bin/sh\nprintf '%s ' \"$@\" > '{ran}'\n");
            File.SetUnixFileMode(stub, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var home = Directory.CreateTempSubdirectory("system7-wm-ladder-home-");
            var environment = new Dictionary<string, string> { ["XDG_DATA_HOME"] = Path.Combine(home.FullName, "data") };
            var applications = Path.Combine(home.FullName, "data", "applications");
            Directory.CreateDirectory(applications);
            File.WriteAllText(Path.Combine(applications, "entrytest.desktop"),
                "[Desktop Entry]\nType=Application\nName=Entry Test\nTerminal=true\nExec=sh -c \"echo from-entry; exit 4\"\n");
            var extra = new List<string>();
            if (ladder == "TERMINAL")
            {
                environment["TERMINAL"] = stub;
            }
            else if (ladder == "config")
            {
                var config = Path.Combine(home.FullName, "system7-wm.toml");
                File.WriteAllText(config, $"[terminal]\ncommand = \"{stub}\"\n");
                extra.AddRange(["--config", config]);
            }

            using var session = await StartAsync(environment, [.. extra]);
            var mark = session.Mark();
            await session.SendAsync("menu apple/Applications/Other/Entry Test");
            if (ladder == "builtin")
            {
                var opened = await session.WaitForPrefixAsync("TERMINAL + ", since: mark);
                var id = opened!.Split(' ')[2];
                Assert.NotNull(await session.WaitForLineAsync($"TERMINAL exit {id} status=4 kept"));
                Assert.EndsWith("\"Entry Test\"", await session.StateLineAsync($"WINDOW {id} "), StringComparison.Ordinal);
            }
            else
            {
                Assert.Equal("-e sh -c echo from-entry; exit 4", (await WaitForFileAsync(ran)).Trim());
                Assert.Null(await session.WaitForPrefixAsync("TERMINAL + ", since: mark, attempts: 5));
            }

            await session.QuitCleanlyAsync();
            home.Delete(recursive: true);
        }
        finally
        {
            shared.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task The_remembered_box_survives_a_restart_and_a_second_window_cascades()
    {
        var home = Directory.CreateTempSubdirectory("system7-wm-box-");
        try
        {
            var environment = new Dictionary<string, string> { ["XDG_STATE_HOME"] = home.FullName };
            using (var session = await StartAsync(environment))
            {
                var id = await OpenAsync(session, "terminal");
                var window = await session.StateLineAsync($"WINDOW {id} ");
                var box = window!.Split(' ')[4];
                var at = box.Split(',');
                var x = int.Parse(at[0], System.Globalization.CultureInfo.InvariantCulture);
                var y = int.Parse(at[1], System.Globalization.CultureInfo.InvariantCulture);
                await session.SendAsync($"move {x + 100} {y - 10}");
                await Task.Delay(200, TestContext.Current.CancellationToken);
                await session.SendAsync("button 272 1");
                await session.SendAsync($"move {x + 160} {y + 50}");
                await Task.Delay(200, TestContext.Current.CancellationToken);
                await session.SendAsync("button 272 0");
                await Task.Delay(300, TestContext.Current.CancellationToken);
                Assert.Contains($" {x + 60},{y + 60},", await session.StateLineAsync($"WINDOW {id} "), StringComparison.Ordinal);
                await session.QuitCleanlyAsync();
                x += 60;
                y += 60;

                using var again = await StartAsync(environment);
                var first = await OpenAsync(again, "terminal");
                Assert.Contains($" {x},{y},", await again.StateLineAsync($"WINDOW {first} "), StringComparison.Ordinal);
                var second = await OpenAsync(again, "terminal");
                Assert.Contains($" {x + 20},{y + 20},", await again.StateLineAsync($"WINDOW {second} "), StringComparison.Ordinal);
                await again.QuitCleanlyAsync();
            }
        }
        finally
        {
            home.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task A_resize_reaches_the_child()
    {
        using var session = await StartAsync();
        var sizes = Path.Combine(session.Home, "sizes");
        var id = await OpenAsync(session, $"terminal sh -c 'trap \"stty size >> {sizes}\" WINCH; while :; do sleep 0.1; done'");
        await session.SendAsync($"zoom {id}");
        var text = await WaitForFileAsync(sizes);
        Assert.NotEqual("24 80", text.Trim().Split('\n')[^1]);
        await session.QuitCleanlyAsync();
    }
}
