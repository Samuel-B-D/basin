using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace LunaWm.Tests;

public sealed class ControlTests : IDisposable
{
    private static readonly string[] Methods =
        [
            "lunawm/cards", "lunawm/card-view", "lunawm/gesture", "lunawm/launcher", "lunawm/notify", "lunawm/lock",
            "lunawm/keyboard", "lunawm/keyboard-tap", "lunawm/keyboard-type", "lunawm/keyboard-hold", "lunawm/keyboard-choose",
            "lunawm/keyboard-candidate", "lunawm/keyboard-trace", "lunawm/keyboard-size", "lunawm/keyboard-layout",
        ];

    private readonly DirectoryInfo _runtime = Directory.CreateTempSubdirectory("luna-wm-control-");

    public void Dispose() => _runtime.Delete(recursive: true);

    [Fact]
    public async Task Basinctl_lists_the_lunawm_methods_and_calls_them()
    {
        var compositor = LunaSession.Compositor();
        var basinctl = Tool("basinctl");
        var socket = Path.Combine(_runtime.FullName, "luna-wm.sock");
        using var config = new TempConfig();
        using var session = new LunaSession(compositor, config.Path, config.Environment, "--ipc", socket);
        Assert.NotNull(await session.WaitForAsync(line => line.StartsWith("SOCKET ", StringComparison.Ordinal)));
        for (var i = 0; i < 50 && !File.Exists(socket); i++)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        var methods = Run(basinctl, "--socket", socket, "methods");
        foreach (var name in Methods)
        {
            Assert.Contains(name, methods, StringComparison.Ordinal);
        }

        Assert.Contains("LAUNCHER on", Run(basinctl, "--socket", socket, "lunawm/launcher"), StringComparison.Ordinal);
        Assert.Contains("VIEW HEADLESS-1 ", Run(basinctl, "--socket", socket, "lunawm/cards"), StringComparison.Ordinal);
        Assert.Contains("ACTION dashboard", Run(basinctl, "--socket", socket, "lunawm/gesture", "action=dashboard"), StringComparison.Ordinal);
        Assert.Contains("KEYBOARD off 0 ", Run(basinctl, "--socket", socket, "lunawm/keyboard"), StringComparison.Ordinal);
        Assert.Contains("lunawm/keyboard", Run(basinctl, "--socket", socket, "events"), StringComparison.Ordinal);
        Assert.Contains("LOCKED", Run(basinctl, "--socket", socket, "lunawm/lock"), StringComparison.Ordinal);
        var windows = Run(basinctl, "--socket", socket, "--json", "windows");
        using (var reply = JsonDocument.Parse(windows))
        {
            Assert.NotEqual(JsonValueKind.Undefined, reply.RootElement.ValueKind);
        }

        await session.SendAsync("quit");
        Assert.NotNull(await session.WaitForAsync(line => line.StartsWith("FRAMES ", StringComparison.Ordinal)));
    }

    [Fact]
    public void Basin_mcp_publishes_the_lunawm_methods_as_tools_with_descriptions()
    {
        var compositor = LunaSession.Compositor();
        var home = Directory.CreateDirectory(Path.Combine(_runtime.FullName, "home"));
        var start = new ProcessStartInfo(Tool("basin-mcp"))
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.Environment["XDG_RUNTIME_DIR"] = _runtime.FullName;
        start.Environment["HOME"] = home.FullName;
        start.Environment["XDG_CONFIG_HOME"] = Path.Combine(home.FullName, ".config");
        start.Environment["XDG_DATA_HOME"] = Path.Combine(home.FullName, ".local", "share");
        start.Environment["XDG_STATE_HOME"] = Path.Combine(home.FullName, ".local", "state");
        start.Environment.Remove("WAYLAND_DISPLAY");
        foreach (var word in new[] { "--launch", "--", compositor, "--backend", "headless", "--renderer", "pixman", "--config", "false" })
        {
            start.ArgumentList.Add(word);
        }

        using var process = Process.Start(start)!;
        _ = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        process.StandardInput.WriteLine("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"t","version":"1"}}}""");
        process.StandardInput.WriteLine("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
        process.StandardInput.Flush();
        Assert.NotNull(ReadLine(process));

        Dictionary<string, string> tools = [];
        var deadline = Environment.TickCount64 + 30_000;
        while (!tools.ContainsKey("lunawm_cards") && Environment.TickCount64 < deadline)
        {
            process.StandardInput.WriteLine("""{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}""");
            process.StandardInput.Flush();
            using var reply = JsonDocument.Parse(ReadLine(process)!);
            tools.Clear();
            if (reply.RootElement.TryGetProperty("result", out var result))
            {
                foreach (var tool in result.GetProperty("tools").EnumerateArray())
                {
                    tools[tool.GetProperty("name").GetString()!] = tool.TryGetProperty("description", out var text) ? text.GetString() ?? string.Empty : string.Empty;
                }
            }

            if (!tools.ContainsKey("lunawm_cards"))
            {
                Thread.Sleep(250);
            }
        }

        foreach (var name in Methods.Select(m => m.Replace('/', '_')))
        {
            Assert.True(tools.TryGetValue(name, out var description), $"{name} is not published: {string.Join(' ', tools.Keys)}");
            Assert.False(string.IsNullOrWhiteSpace(description), $"{name} has no description");
        }

        process.StandardInput.Close();
        Assert.True(process.WaitForExit(20_000));
    }

    private static string Tool(string name)
    {
        var project = name == "basinctl" ? "BasinCtl" : "BasinMcp";
        var framework = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var path = Path.Combine(RepoRoot(), "samples", project, "bin", framework.Parent!.Name, framework.Name, name);
        Assert.SkipUnless(File.Exists(path), $"{name} has not been built beside the tests");
        return path;
    }

    private static string RepoRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (directory is not null && !File.Exists(Path.Combine(directory, "basin.slnx")))
        {
            directory = Path.GetDirectoryName(directory.TrimEnd(Path.DirectorySeparatorChar));
        }

        Assert.SkipWhen(directory is null, "the repository root is not above the tests");
        return directory!;
    }

    private static string? ReadLine(Process process)
    {
        var line = process.StandardOutput.ReadLineAsync(TestContext.Current.CancellationToken).AsTask();
        Assert.True(line.Wait(30_000), "no reply from basin-mcp");
        return line.Result;
    }

    private static string Run(string path, params string[] arguments)
    {
        var start = new ProcessStartInfo(path)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var errors = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        Assert.True(process.WaitForExit(20_000), $"{Path.GetFileName(path)} did not exit");
        Assert.True(process.ExitCode == 0, $"{Path.GetFileName(path)} {string.Join(' ', arguments)} exited {process.ExitCode}: {errors.Result}");
        return output.Result;
    }
}
