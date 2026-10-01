using System.Diagnostics;
using Xunit;

namespace Basin.Tests;

public sealed class AudioDaemonFixture : IDisposable
{
    public const string SinkName = "basin-audio-test-sink";

    private readonly string _runtimeDirectory;
    private Process? _daemon;

    public AudioDaemonFixture()
    {
        _runtimeDirectory = Path.Combine(Path.GetTempPath(), $"basin-audio-tests-{Environment.ProcessId}");
        SocketPath = Path.Combine(_runtimeDirectory, "pipewire-0");
        try
        {
            Directory.CreateDirectory(_runtimeDirectory);
            var config = Path.Combine(_runtimeDirectory, "pipewire.conf");
            File.WriteAllText(config, Configuration);
            _daemon = Start(config);
            IsAvailable = _daemon is not null && WaitForSocket(TimeSpan.FromSeconds(10));
            if (!IsAvailable)
            {
                SkipReason = _daemon is null
                    ? "the 'pipewire' binary is not installed"
                    : $"the private PipeWire daemon created no socket in {_runtimeDirectory}";
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            SkipReason = $"the private PipeWire daemon could not start: {error.Message}";
        }
    }

    public bool IsAvailable { get; }

    public string SkipReason { get; } = string.Empty;

    public string SocketPath { get; }

    public string RuntimeDirectory => _runtimeDirectory;

    public void SkipIfUnavailable() => Assert.SkipUnless(IsAvailable, SkipReason);

    public ProcessStartInfo Client(string program, params string[] arguments)
    {
        var info = new ProcessStartInfo(program)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        info.Environment["PIPEWIRE_REMOTE"] = SocketPath;
        info.Environment.Remove("PULSE_SERVER");
        return info;
    }

    public void Dispose()
    {
        if (_daemon is not null)
        {
            try
            {
                if (!_daemon.HasExited)
                {
                    _daemon.Kill(entireProcessTree: true);
                    _daemon.WaitForExit(5000);
                }
            }
            catch (InvalidOperationException)
            {
            }

            _daemon.Dispose();
            _daemon = null;
        }

        try
        {
            Directory.Delete(_runtimeDirectory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private const string Configuration = $$"""
        context.properties = {
            core.daemon = true
            core.name = pipewire-0
            link.max-buffers = 16
            default.clock.rate = 48000
            default.clock.quantum = 1024
            default.clock.min-quantum = 1024
            default.clock.max-quantum = 1024
        }

        context.spa-libs = {
            audio.convert.* = audioconvert/libspa-audioconvert
            support.* = support/libspa-support
        }

        context.modules = [
            { name = libpipewire-module-access }
            { name = libpipewire-module-protocol-native }
            { name = libpipewire-module-client-node }
            { name = libpipewire-module-adapter }
            { name = libpipewire-module-link-factory }
            { name = libpipewire-module-metadata }
            { name = libpipewire-module-spa-node-factory }
        ]

        context.objects = [
            { factory = spa-node-factory
              args = {
                  factory.name = support.node.driver
                  node.name = Dummy-Driver
                  priority.driver = 20000
              }
            }
            { factory = metadata
              args = {
                  metadata.name = default
              }
            }
            { factory = adapter
              args = {
                  factory.name = support.null-audio-sink
                  node.name = {{SinkName}}
                  media.class = Audio/Sink
                  object.linger = true
                  audio.position = [ FL FR ]
                  monitor.channel-volumes = true
              }
            }
        ]
        """;

    private Process? Start(string config)
    {
        var info = new ProcessStartInfo("pipewire")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add(config);
        info.Environment["PIPEWIRE_RUNTIME_DIR"] = _runtimeDirectory;
        info.Environment["PIPEWIRE_LOG_LEVEL"] = "1";
        try
        {
            var process = Process.Start(info);
            if (process is not null)
            {
                process.OutputDataReceived += (_, _) => { };
                process.ErrorDataReceived += (_, _) => { };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }

            return process;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private bool WaitForSocket(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(SocketPath))
            {
                return true;
            }

            if (_daemon?.HasExited == true)
            {
                return false;
            }

            Thread.Sleep(50);
        }

        return false;
    }
}
