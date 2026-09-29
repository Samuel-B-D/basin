using System.Diagnostics;
using System.Globalization;
using Xunit;

namespace System7Wm.Tests;

public sealed class AllocationTests
{
    private static int Number(string line, string prefix) =>
        int.Parse(line[prefix.Length..].Split(' ')[0], CultureInfo.InvariantCulture);

    [Fact]
    public async Task An_idle_desktop_with_the_bar_at_rest_draws_no_frames()
    {
        using var session = new Session(Prerequisites.Compositor());
        await session.DisplayAsync();
        await session.SendAsync("clock 12:00");
        Assert.NotNull(await session.WaitForLineAsync("CLOCK 12:00"));
        await Task.Delay(1500, TestContext.Current.CancellationToken);

        var before = Number((await session.StateLineAsync("FRAMECOUNT "))!, "FRAMECOUNT ");
        await Task.Delay(TimeSpan.FromSeconds(4), TestContext.Current.CancellationToken);
        var after = Number((await session.StateLineAsync("FRAMECOUNT "))!, "FRAMECOUNT ");
        Assert.Equal(before, after);
        await session.QuitCleanlyAsync();
    }

    [Fact]
    public async Task A_hundred_menu_cycles_return_to_the_baseline()
    {
        using var session = new Session(Prerequisites.Compositor());
        await session.DisplayAsync();
        await Task.Delay(1000, TestContext.Current.CancellationToken);
        var surfaces = await session.StateLineAsync("SURFACES ");
        var popups = await session.StateLineAsync("POPUPS ");

        await session.SendAsync("move 60 10");
        for (var i = 0; i < 100; i++)
        {
            var mark = session.Mark();
            await session.SendAsync("button 272 1");
            Assert.NotNull(await session.WaitForLineAsync("MENUBAR open", mark));
            await session.SendAsync("button 272 0");
            Assert.NotNull(await session.WaitForLineAsync("MENUBAR closed", mark));
        }

        await Task.Delay(500, TestContext.Current.CancellationToken);
        Assert.Equal(surfaces, await session.StateLineAsync("SURFACES "));
        Assert.Equal(popups, await session.StateLineAsync("POPUPS "));
        Assert.StartsWith("MENUBAR rest open=False", (await session.StateLineAsync("MENUBAR "))!, StringComparison.Ordinal);
        await session.QuitCleanlyAsync();
    }

    [Fact]
    public async Task Churning_windows_with_app_layering_return_to_the_baseline()
    {
        var churn = Prerequisites.RepoRoot() is { } root
            ? Path.Combine(root, "tests", "BasinChurn", "bin", new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent!.Name, "net10.0", "basinchurn")
            : null;
        Assert.SkipUnless(churn is not null && File.Exists(churn), "basinchurn has not been built");
        using var session = new Session(Prerequisites.Compositor());
        var display = await session.DisplayAsync();
        await Task.Delay(1000, TestContext.Current.CancellationToken);
        var surfaces = await session.StateLineAsync("SURFACES ");

        var start = new ProcessStartInfo(churn!) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var word in new[] { "--socket", display, "--seed", "7", "--windows", "6", "--rate", "20", "--seconds", "8" })
        {
            start.ArgumentList.Add(word);
        }

        start.Environment["WAYLAND_DISPLAY"] = display;
        using (var process = Process.Start(start)!)
        {
            _ = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            _ = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            Assert.True(process.WaitForExit(60_000), "basinchurn finished");
            Assert.Equal(0, process.ExitCode);
        }

        Assert.NotNull(await session.WaitForAsync(line => line == surfaces, poke: "where", fresh: true));
        var state = await session.StateAsync();
        Assert.DoesNotContain(state, line => line.StartsWith("WINDOW ", StringComparison.Ordinal));
        Assert.Contains(state, line => line.StartsWith("FRONT finder ", StringComparison.Ordinal));
        await session.QuitCleanlyAsync();
    }
}
