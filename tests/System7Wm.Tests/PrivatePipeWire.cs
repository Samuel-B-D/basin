using System.Diagnostics;
using Xunit;

namespace System7Wm.Tests;

internal sealed class PrivatePipeWire : IDisposable
{
    public const string SinkName = "system7-test-sink";

    private readonly Process _daemon;

    private PrivatePipeWire(Process daemon, string directory)
    {
        _daemon = daemon;
        Directory = directory;
    }

    public string Directory { get; }

    public string Socket => Path.Combine(Directory, "pipewire-0");

    public static PrivatePipeWire Start()
    {
        Assert.SkipUnless(
            (Environment.GetEnvironmentVariable("PATH") ?? "").Split(':', StringSplitOptions.RemoveEmptyEntries).Any(d => File.Exists(Path.Combine(d, "pipewire"))),
            "the 'pipewire' binary is not installed");
        var directory = Path.Combine("/tmp", $"s7pw-{Environment.ProcessId}-{Guid.NewGuid().ToString("N")[..8]}");
        System.IO.Directory.CreateDirectory(directory);
        var config = Path.Combine(directory, "pipewire.conf");
        File.WriteAllText(config, Configuration);
        var info = new ProcessStartInfo("pipewire")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add(config);
        info.Environment["PIPEWIRE_RUNTIME_DIR"] = directory;
        info.Environment["PIPEWIRE_LOG_LEVEL"] = "1";
        info.Environment.Remove("PIPEWIRE_REMOTE");
        var daemon = Process.Start(info) ?? throw new InvalidOperationException("pipewire did not start");
        daemon.OutputDataReceived += (_, _) => { };
        daemon.ErrorDataReceived += (_, _) => { };
        daemon.BeginOutputReadLine();
        daemon.BeginErrorReadLine();
        var pipewire = new PrivatePipeWire(daemon, directory);
        for (var i = 0; i < 200 && !File.Exists(pipewire.Socket); i++)
        {
            if (daemon.HasExited)
            {
                break;
            }

            Thread.Sleep(50);
        }

        if (!File.Exists(pipewire.Socket))
        {
            pipewire.Dispose();
            throw new InvalidOperationException($"the private PipeWire daemon made no socket in {directory}");
        }

        return pipewire;
    }

    public void Dispose()
    {
        if (!_daemon.HasExited)
        {
            _daemon.Kill(entireProcessTree: true);
            _daemon.WaitForExit(5000);
        }

        _daemon.Dispose();
        try
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private const string Configuration = $$"""
        context.properties = {
            core.daemon = true
            core.name = pipewire-0
            default.clock.rate = 48000
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
                  metadata.values = [
                      { key = default.audio.sink type = "Spa:String:JSON" value = { "name": "{{SinkName}}" } }
                  ]
              }
            }
            { factory = adapter
              args = {
                  factory.name = support.null-audio-sink
                  node.name = {{SinkName}}
                  media.class = Audio/Sink
                  object.linger = true
                  audio.position = [ FL FR ]
              }
            }
        ]
        """;
}
