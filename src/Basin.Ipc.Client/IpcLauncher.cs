using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Basin.Ipc;

public sealed class IpcLauncher : IAsyncDisposable
{
    private const int SigTerm = 15;

    private readonly Process _process;
    private readonly Task _output;
    private readonly Task _errors;
    private bool _disposed;

    private IpcLauncher(string directory, string socketPath, Process process, Stream output)
    {
        Directory = directory;
        SocketPath = socketPath;
        _process = process;
        _output = process.StandardOutput.BaseStream.CopyToAsync(output);
        _errors = process.StandardError.BaseStream.CopyToAsync(output);
        Exited = process.WaitForExitAsync();
    }

    public static TimeSpan StopGrace { get; } = TimeSpan.FromSeconds(5);

    public string Directory { get; }

    public string SocketPath { get; }

    public int Pid => _process.Id;

    public Task Exited { get; }

    public bool HasExited => _process.HasExited;

    public int ExitCode => _process.HasExited ? _process.ExitCode : 0;

    public static IpcLauncher? Start(IReadOnlyList<string> command, string directoryName, Stream output, out string? error)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrEmpty(directoryName);
        ArgumentNullException.ThrowIfNull(output);
        if (command.Count == 0 || command[0].Length == 0)
        {
            error = "the launch needs a command";
            return null;
        }

        var runtime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        if (string.IsNullOrEmpty(runtime) || !Path.IsPathRooted(runtime))
        {
            error = "the launch needs XDG_RUNTIME_DIR for its private socket directory";
            return null;
        }

        var directory = Path.Combine(runtime, directoryName);
        try
        {
            if (System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.Delete(directory, recursive: true);
            }

            _ = System.IO.Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = $"cannot make '{directory}': {exception.Message}";
            return null;
        }

        var socketPath = Path.Combine(directory, "basin.sock");
        var start = new ProcessStartInfo(command[0])
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        for (var i = 1; i < command.Count; i++)
        {
            start.ArgumentList.Add(command[i]);
        }

        start.Environment[IpcProtocol.PathVariable] = socketPath;
        _ = start.Environment.Remove(IpcProtocol.SocketVariable);
        Process process;
        try
        {
            process = Process.Start(start) ?? throw new InvalidOperationException("the process did not start");
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            TryRemove(directory);
            error = $"{command[0]}: {exception.Message}";
            return null;
        }

        process.StandardInput.Close();
        error = null;
        return new IpcLauncher(directory, socketPath, process, output);
    }

    public async Task<bool> WaitForSocketAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;
        while (Environment.TickCount64 < deadline && !cancellationToken.IsCancellationRequested)
        {
            if (File.Exists(SocketPath))
            {
                return true;
            }

            if (_process.HasExited)
            {
                return false;
            }

            await Task.WhenAny(Exited, Task.Delay(20, cancellationToken)).ConfigureAwait(false);
        }

        return File.Exists(SocketPath);
    }

    public async Task<bool> StopAsync()
    {
        if (_disposed)
        {
            return false;
        }

        _disposed = true;
        var killed = false;
        if (!_process.HasExited)
        {
            _ = Kill(_process.Id, SigTerm);
            TryRemove(Directory);
            if (await Task.WhenAny(Exited, Task.Delay(StopGrace)).ConfigureAwait(false) != Exited)
            {
                killed = true;
                _process.Kill();
                await Exited.ConfigureAwait(false);
            }
        }

        try
        {
            await Task.WhenAll(_output, _errors).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
        }

        _process.Dispose();
        TryRemove(Directory);
        return killed;
    }

    public async ValueTask DisposeAsync() => _ = await StopAsync().ConfigureAwait(false);

    private static void TryRemove(string directory)
    {
        try
        {
            System.IO.Directory.Delete(directory, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int Kill(int pid, int signal);
}
