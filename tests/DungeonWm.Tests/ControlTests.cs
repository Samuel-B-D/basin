using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace DungeonWm.Tests;

public sealed class ControlTests : IDisposable
{
    private readonly DirectoryInfo _runtime = Directory.CreateTempSubdirectory("dungeon-wm-control-");

    public void Dispose() => _runtime.Delete(recursive: true);

    [Fact]
    public async Task Basinctl_lists_the_dungeon_wm_methods_and_calls_one()
    {
        var (compositor, client) = ProcessTests.Prerequisites("gl", "ssdwin");
        var basinctl = Tool("basinctl");
        var socket = Path.Combine(_runtime.FullName, "dungeon-wm.sock");
        using var session = new ProcessTests.Session(compositor, "gl", client, "--ipc", socket);
        Assert.NotNull(await session.WaitForAsync(line => line.StartsWith("MAPPED ssdwin", StringComparison.Ordinal)));

        var methods = Run(basinctl, "--socket", socket, "methods");
        foreach (var name in new[] { "dungeonwm/camera", "dungeonwm/walk", "dungeonwm/look", "dungeonwm/teleport", "dungeonwm/hang", "dungeonwm/throw", "dungeonwm/physics-state", "dungeonwm/world", "dungeonwm/reload" })
        {
            Assert.Contains(name, methods, StringComparison.Ordinal);
        }

        var camera = Run(basinctl, "--socket", socket, "--json", "dungeonwm/camera");
        using (var reply = JsonDocument.Parse(camera))
        {
            Assert.Equal(JsonValueKind.Object, reply.RootElement.ValueKind);
        }

        await session.Quit();
    }

    [Fact]
    public void Basin_mcp_publishes_every_dungeon_wm_method_as_a_tool_with_a_description()
    {
        var (compositor, _) = ProcessTests.Prerequisites("gl", "ssdwin");
        var start = new ProcessStartInfo(Tool("basin-mcp"))
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.Environment["XDG_RUNTIME_DIR"] = _runtime.FullName;
        foreach (var word in new[] { "--launch", "--", compositor, "--backend", "headless", "--renderer", "gl", "--config", "false" })
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
        while (!tools.ContainsKey("dungeonwm_camera") && Environment.TickCount64 < deadline)
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

            if (!tools.ContainsKey("dungeonwm_camera"))
            {
                Thread.Sleep(250);
            }
        }

        foreach (var name in new[] { "dungeonwm_camera", "dungeonwm_walk", "dungeonwm_look", "dungeonwm_teleport", "dungeonwm_hang", "dungeonwm_throw", "dungeonwm_physics-state", "dungeonwm_world", "dungeonwm_reload" })
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
        var path = Path.Combine(ProcessTests.Repository(), "samples", project, "bin", framework.Parent!.Name, framework.Name, name);
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
