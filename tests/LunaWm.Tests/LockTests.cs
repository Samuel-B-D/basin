using System.Diagnostics;
using Xunit;

namespace LunaWm.Tests;

public sealed class LockTests
{
    private const string Stub = "read p; [ \"$p\" = 1234 ] && echo 1 || echo 0";

    private static bool Starts(string line, string prefix) => line.StartsWith(prefix, StringComparison.Ordinal);

    private static string Script(string name)
    {
        var root = AppContext.BaseDirectory;
        for (var i = 0; i < 10 && root is not null; i++)
        {
            var candidate = Path.Combine(root, "scripts", name);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            root = Path.GetDirectoryName(root.TrimEnd(Path.DirectorySeparatorChar));
        }

        Assert.Skip($"scripts/{name} is not in this tree");
        return null!;
    }

    [Fact]
    public async Task Lock_refuses_a_wrong_entry_and_unlocks_through_the_helper()
    {
        using var config = new TempConfig();
        using var session = new LunaSession(LunaSession.Compositor(), config.Path, config.Environment, "--pam-helper-command", Stub);
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "SOCKET ")));
        await session.SendAsync("lock");
        Assert.NotNull(await session.WaitForAsync(line => line == "LOCKED"));
        Assert.NotNull(await session.WaitForAsync(line => line == "LOCK on", poke: "where", fresh: true));
        await session.SendAsync("unlock-type 0000");
        Assert.NotNull(await session.WaitForAsync(line => line == "UNLOCK pad password"));
        await session.SendAsync("unlock-submit");
        Assert.NotNull(await session.WaitForAsync(line => line == "UNLOCK failed 1"));
        await session.SendAsync("unlock-type 1234");
        await session.SendAsync("unlock-submit");
        Assert.NotNull(await session.WaitForAsync(line => line == "UNLOCKED"));
        Assert.NotNull(await session.WaitForAsync(line => line == "LOCK off", poke: "where", fresh: true));
        await session.SendAsync("quit");
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "FRAMES ")));
    }

    [Fact]
    public async Task Five_wrong_entries_hold_the_pad_and_a_pin_style_shows_the_keypad()
    {
        using var config = new TempConfig("[lock]\nstyle = \"pin\"\n");
        using var session = new LunaSession(LunaSession.Compositor(), config.Path, config.Environment, "--pam-helper-command", Stub);
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "SOCKET ")));
        await session.SendAsync("lock");
        Assert.NotNull(await session.WaitForAsync(line => line == "LOCKED"));
        for (var i = 1; i <= 5; i++)
        {
            await session.SendAsync("unlock-type 1111");
            await session.SendAsync("unlock-submit");
            Assert.NotNull(await session.WaitForAsync(line => line == $"UNLOCK failed {i}"));
        }

        Assert.NotNull(await session.WaitForAsync(line => line == "UNLOCK lockout"));
        Assert.NotNull(await session.WaitForAsync(line => line == "UNLOCK pad pin"));
        await session.SendAsync("unlock-type 1234");
        await session.SendAsync("unlock-submit");
        Assert.Null(await session.WaitForAsync(line => line == "UNLOCKED", attempts: 10));
        await session.SendAsync("quit");
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "FRAMES ")));
    }

    [Fact]
    public async Task An_external_session_lock_takes_precedence_over_the_own_lock()
    {
        var script = Script("session-lock.sh");
        using var config = new TempConfig();
        using var session = new LunaSession(LunaSession.Compositor(), config.Path, config.Environment, "--pam-helper-command", Stub);
        var socket = await session.WaitForAsync(line => Starts(line, "SOCKET "));
        await session.SendAsync("lock");
        Assert.NotNull(await session.WaitForAsync(line => line == "LOCKED"));
        Assert.NotNull(await session.WaitForAsync(line => line == "  lock shown", poke: "where", fresh: true));

        var info = new ProcessStartInfo(script) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add("2");
        info.Environment["WAYLAND_DISPLAY"] = socket!.Split(' ')[1];
        using var locker = Process.Start(info)!;
        Assert.NotNull(await session.WaitForAsync(line => line == "LOCKED session"));
        Assert.NotNull(await session.WaitForAsync(line => line == "  lock hidden", poke: "where", fresh: true));
        Assert.NotNull(await session.WaitForAsync(line => line == "UNLOCKED session", attempts: 150));
        Assert.True(locker.WaitForExit(15000));
        Assert.NotNull(await session.WaitForAsync(line => line == "  lock shown", poke: "where", fresh: true));
        Assert.NotNull(await session.WaitForAsync(line => line == "LOCK on", poke: "where", fresh: true));
        await session.SendAsync("quit");
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "FRAMES ")));
    }

    [Fact]
    public async Task The_real_pam_helper_unlocks_with_the_test_account()
    {
        var user = Environment.GetEnvironmentVariable("LUNAWM_PAM_TEST_USER");
        var password = Environment.GetEnvironmentVariable("LUNAWM_PAM_TEST_PASSWORD");
        Assert.SkipWhen(string.IsNullOrEmpty(user) || string.IsNullOrEmpty(password), "LUNAWM_PAM_TEST_USER and LUNAWM_PAM_TEST_PASSWORD are not set");
        Assert.SkipUnless(user == Environment.UserName, "the helper checks the account luna-wm runs as");
        using var config = new TempConfig();
        using var session = new LunaSession(LunaSession.Compositor(), config.Path, config.Environment);
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "SOCKET ")));
        await session.SendAsync("lock");
        Assert.NotNull(await session.WaitForAsync(line => line == "LOCKED"));
        await session.SendAsync($"unlock-type {password}");
        await session.SendAsync("unlock-submit");
        Assert.NotNull(await session.WaitForAsync(line => line == "UNLOCKED", attempts: 100));
        await session.SendAsync("quit");
        Assert.NotNull(await session.WaitForAsync(line => Starts(line, "FRAMES ")));
    }
}
