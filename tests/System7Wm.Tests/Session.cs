using System.Diagnostics;
using Xunit;

namespace System7Wm.Tests;

internal sealed class Session : IDisposable
{
    private readonly Process _process;
    private readonly List<string> _lines = [];
    private readonly Lock _gate = new();
    private readonly DirectoryInfo _home;

    public Session(string path, string renderer = "pixman", IReadOnlyDictionary<string, string>? environment = null, params string[] extra)
    {
        _home = Directory.CreateTempSubdirectory("system7-wm-tests-");
        Home = _home.FullName;
        Directory.CreateDirectory(Path.Combine(Home, "Desktop"));
        var info = new ProcessStartInfo(path)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        info.Environment["HOME"] = Home;
        info.Environment["XDG_CONFIG_HOME"] = Path.Combine(Home, ".config");
        info.Environment["XDG_DATA_HOME"] = Path.Combine(Home, ".local", "share");
        info.Environment["XDG_STATE_HOME"] = Path.Combine(Home, ".local", "state");
        info.Environment["XDG_CACHE_HOME"] = Path.Combine(Home, ".cache");
        info.Environment.Remove("WAYLAND_DISPLAY");
        info.Environment.Remove("DISPLAY");
        foreach (var (name, value) in environment ?? new Dictionary<string, string>())
        {
            info.Environment[name] = value;
        }

        info.ArgumentList.Add("--backend");
        info.ArgumentList.Add("headless");
        info.ArgumentList.Add("--renderer");
        info.ArgumentList.Add(renderer);
        if (Array.IndexOf(extra, "--config") < 0)
        {
            info.ArgumentList.Add("--config");
            info.ArgumentList.Add("false");
        }

        foreach (var argument in extra)
        {
            info.ArgumentList.Add(argument);
        }

        _process = Process.Start(info)!;
        _process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (_gate)
                {
                    _lines.Add(e.Data);
                }
            }
        };
        _process.ErrorDataReceived += (_, _) => { };
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    public string Home { get; }

    public int Id => _process.Id;

    public static string? Locate(string name)
    {
        var directory = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && directory is not null; i++)
        {
            var candidate = Path.Combine(directory, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = Path.GetDirectoryName(directory.TrimEnd(Path.DirectorySeparatorChar));
        }

        return null;
    }

    public static string? WlClient(string name)
    {
        var directory = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && directory is not null; i++)
        {
            var clients = Path.Combine(directory, "scripts", "wlclients");
            if (File.Exists(Path.Combine(clients, "Makefile")))
            {
                var binary = Path.Combine(clients, "bin", name);
                if (!File.Exists(binary))
                {
                    var info = new ProcessStartInfo("make")
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WorkingDirectory = clients,
                    };
                    info.ArgumentList.Add($"bin/{name}");
                    try
                    {
                        using var make = Process.Start(info)!;
                        make.WaitForExit(120_000);
                    }
                    catch (Exception error) when (error is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
                    {
                        return null;
                    }
                }

                return File.Exists(binary) ? binary : null;
            }

            directory = Path.GetDirectoryName(directory.TrimEnd(Path.DirectorySeparatorChar));
        }

        return null;
    }

    public static string? Which(string name)
    {
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(':'))
        {
            if (directory.Length > 0 && File.Exists(Path.Combine(directory, name)))
            {
                return Path.Combine(directory, name);
            }
        }

        return null;
    }

    public Client StartClient(string path, string display, IReadOnlyDictionary<string, string>? environment = null, params string[] arguments)
    {
        var info = new ProcessStartInfo(path)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        info.Environment["WAYLAND_DISPLAY"] = display;
        info.Environment["HOME"] = Home;
        foreach (var (name, value) in environment ?? new Dictionary<string, string>())
        {
            info.Environment[name] = value;
        }

        return new Client(Process.Start(info)!);
    }

    public int Mark()
    {
        lock (_gate)
        {
            return _lines.Count;
        }
    }

    public IReadOnlyList<string> LinesSince(int mark)
    {
        lock (_gate)
        {
            return _lines.Skip(mark).ToList();
        }
    }

    public async Task<string> DisplayAsync()
    {
        var socket = await WaitForAsync(line => line.StartsWith("SOCKET ", StringComparison.Ordinal));
        return socket is null ? throw new InvalidOperationException("the compositor printed no SOCKET line") : socket.Split(' ')[1];
    }

    public async Task<string> IpcPathAsync()
    {
        var ipc = await WaitForAsync(line => line.StartsWith("IPC ", StringComparison.Ordinal));
        return ipc is null ? throw new InvalidOperationException("the compositor printed no IPC line") : ipc[4..];
    }

    public async Task SendAsync(string command)
    {
        await _process.StandardInput.WriteLineAsync(command);
        await _process.StandardInput.FlushAsync();
    }

    public async Task<string?> WaitForAsync(Func<string, bool> predicate, string? poke = null, bool fresh = false, int? since = null, int attempts = 100)
    {
        var floor = since ?? (fresh ? Mark() : 0);
        for (var i = 0; i < attempts; i++)
        {
            if (poke is not null)
            {
                await SendAsync(poke);
            }

            lock (_gate)
            {
                for (var j = _lines.Count - 1; j >= floor; j--)
                {
                    if (predicate(_lines[j]))
                    {
                        return _lines[j];
                    }
                }
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        return null;
    }

    public Task<string?> WaitForLineAsync(string exact, int? since = null) => WaitForAsync(line => line == exact, since: since);

    public Task<string?> WaitForPrefixAsync(string prefix, int? since = null, int attempts = 100) =>
        WaitForAsync(line => line.StartsWith(prefix, StringComparison.Ordinal), since: since, attempts: attempts);

    public async Task<List<string>> StateAsync()
    {
        var mark = Mark();
        await SendAsync("where");
        var end = await WaitForAsync(line => line.StartsWith("POPUPS ", StringComparison.Ordinal), since: mark);
        if (end is null)
        {
            return [];
        }

        lock (_gate)
        {
            return _lines.Skip(mark).ToList();
        }
    }

    public async Task<string?> StateLineAsync(string prefix) =>
        (await StateAsync()).LastOrDefault(line => line.StartsWith(prefix, StringComparison.Ordinal));

    public async Task QuitCleanlyAsync()
    {
        await SendAsync("quit");
        var frames = await WaitForPrefixAsync("FRAMES ");
        Assert.NotNull(frames);
        var fields = frames!.Split(' ');
        Assert.Equal("LIVE", fields[2]);
        if (fields[3] != "untracked")
        {
            Assert.Equal("0", fields[3]);
        }

        Assert.True(_process.WaitForExit(10_000), "the compositor exited after quit");
        Assert.Equal(0, _process.ExitCode);
    }

    public void Dispose()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }

            _process.WaitForExit(5000);
        }
        catch (InvalidOperationException)
        {
        }

        _process.Dispose();
        try
        {
            _home.Delete(recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
