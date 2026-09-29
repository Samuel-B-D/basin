using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace System7Wm.Tests;

public sealed class ControlTests : IDisposable
{
    private static readonly string[] Methods =
        ["system7wm/where", "system7wm/menu", "system7wm/menus", "system7wm/hide", "system7wm/show-all", "system7wm/shade", "system7wm/zoom", "system7wm/panel", "system7wm/finder", "system7wm/trash", "system7wm/depth", "system7wm/shot"];

    private readonly DirectoryInfo _runtime = Directory.CreateTempSubdirectory("system7-wm-control-");

    public void Dispose() => _runtime.Delete(recursive: true);

    [Fact]
    public async Task Basinctl_lists_the_system7_methods_and_calls_them()
    {
        var compositor = Prerequisites.Compositor();
        var basinctl = Tool("basinctl");
        var socket = Path.Combine(_runtime.FullName, "system7-wm.sock");
        using var session = new Session(compositor, "pixman", null, "--ipc", socket);
        await session.DisplayAsync();
        for (var i = 0; i < 50 && !File.Exists(socket); i++)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        var methods = Run(basinctl, "--socket", socket, "methods");
        foreach (var name in Methods)
        {
            Assert.Contains(name, methods, StringComparison.Ordinal);
        }

        Assert.Contains("PANEL Sound open", Run(basinctl, "--socket", socket, "system7wm/panel", "name=Sound"), StringComparison.Ordinal);
        Assert.Contains("SHELL ", Run(basinctl, "--socket", socket, "system7wm/where"), StringComparison.Ordinal);
        var windows = Run(basinctl, "--socket", socket, "--json", "windows");
        using (var reply = JsonDocument.Parse(windows))
        {
            Assert.NotEqual(JsonValueKind.Undefined, reply.RootElement.ValueKind);
        }

        Assert.Contains("DEPTH bw", Run(basinctl, "--socket", socket, "system7wm/depth", "depth=bw"), StringComparison.Ordinal);
        await session.QuitCleanlyAsync();
    }

    [Fact]
    public void Basin_mcp_publishes_the_system7_methods_as_tools_with_descriptions()
    {
        var compositor = Prerequisites.Compositor();
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
        while (!tools.ContainsKey("system7wm_where") && Environment.TickCount64 < deadline)
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

            if (!tools.ContainsKey("system7wm_where"))
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
        var path = Path.Combine(Prerequisites.RepoRoot()!, "samples", project, "bin", framework.Parent!.Name, framework.Name, name);
        Assert.SkipUnless(File.Exists(path), $"{name} has not been built beside the tests");
        return path;
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
