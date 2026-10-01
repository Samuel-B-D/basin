using System.Diagnostics;
using Basin.Audio.PipeWire;
using PipeWire;
using PipeWire.Native;
using PipeWire.Spa;
using Xunit;
using static PipeWire.Native.Pipewire;

namespace Basin.Tests;

internal sealed class AudioRig : IDisposable
{
    private readonly AudioDaemonFixture _daemon;
    private readonly List<Process> _processes = [];
    private readonly List<PipeWireProxy> _links = [];

    public AudioRig(AudioDaemonFixture daemon)
    {
        _daemon = daemon;
        Loop = new EpollEventLoop();
        Session = AudioSession.TryCreate(Loop, out var reason, daemon.SocketPath, "basin-test")
            ?? throw new InvalidOperationException($"no session on the private daemon: {reason}");
        Directory = Path.Combine(daemon.RuntimeDirectory, $"rig-{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(Directory);
    }

    public EpollEventLoop Loop { get; }

    public AudioSession Session { get; }

    public string Directory { get; }

    public PipeWireGlobal Node(string name)
    {
        PipeWireGlobal? found = null;
        Assert.True(Session.WaitFor(() => (found = Session.FindNode(name)) is not null, 5000), $"no node {name}");
        return found!;
    }

    public Process Play(string wav, string nodeName) =>
        StartClient("pw-play", "--properties", $"{{ node.name = {nodeName} application.name = {nodeName} }}", wav);

    public Process Record(string wav, string nodeName) =>
        StartClient("pw-record", "--format", "f32", "--rate", "48000", "--channels", "2",
            "--properties", $"{{ node.name = {nodeName} }}", wav);

    public void Configure(uint nodeId, bool output, bool monitor = false)
    {
        using var builder = new SpaPodBuilder(512);
        using (builder.PushObject(SPA_TYPE_OBJECT_ParamPortConfig, (uint)spa_param_type.SPA_PARAM_PortConfig))
        {
            builder.AddProperty((uint)spa_param_port_config.SPA_PARAM_PORT_CONFIG_direction);
            builder.AddId((uint)(output ? spa_direction.SPA_DIRECTION_OUTPUT : spa_direction.SPA_DIRECTION_INPUT));
            builder.AddProperty((uint)spa_param_port_config.SPA_PARAM_PORT_CONFIG_mode);
            builder.AddId((uint)spa_param_port_config_mode.SPA_PARAM_PORT_CONFIG_MODE_dsp);
            builder.AddProperty((uint)spa_param_port_config.SPA_PARAM_PORT_CONFIG_monitor);
            builder.AddBool(monitor);
            builder.AddProperty((uint)spa_param_port_config.SPA_PARAM_PORT_CONFIG_format);
            using (builder.PushObject(SPA_TYPE_OBJECT_Format, (uint)spa_param_type.SPA_PARAM_Format))
            {
                builder.AddProperty((uint)spa_format.SPA_FORMAT_mediaType);
                builder.AddId((uint)spa_media_type.SPA_MEDIA_TYPE_audio);
                builder.AddProperty((uint)spa_format.SPA_FORMAT_mediaSubtype);
                builder.AddId((uint)spa_media_subtype.SPA_MEDIA_SUBTYPE_raw);
                builder.AddProperty((uint)spa_format.SPA_FORMAT_AUDIO_format);
                builder.AddId((uint)spa_audio_format.SPA_AUDIO_FORMAT_F32P);
                builder.AddProperty((uint)spa_format.SPA_FORMAT_AUDIO_rate);
                builder.AddInt(48000);
                builder.AddProperty((uint)spa_format.SPA_FORMAT_AUDIO_channels);
                builder.AddInt(2);
                builder.AddProperty((uint)spa_format.SPA_FORMAT_AUDIO_position);
                builder.AddIdArray([(uint)spa_audio_channel.SPA_AUDIO_CHANNEL_FL, (uint)spa_audio_channel.SPA_AUDIO_CHANNEL_FR]);
            }
        }

        for (var attempt = 0; attempt < 4; attempt++)
        {
            using var node = Session.Registry.BindNode(nodeId);
            node.SetParam(spa_param_type.SPA_PARAM_PortConfig, builder.WrittenSpan);
            Assert.True(Session.Roundtrip());
            if (Session.WaitFor(() => Ports(nodeId, output ? "out" : "in", false).Count >= 2, 1500))
            {
                return;
            }
        }

        Assert.Fail($"node {nodeId} ({Session.Globals.GetValueOrDefault(nodeId)?.GetProperty("node.name")}) made no ports for its port config");
    }

    public void Link(uint output, uint input, bool monitor = false)
    {
        Assert.True(Session.Roundtrip());
        var outputs = Ports(output, "out", monitor);
        var inputs = Ports(input, "in", false);
        Assert.True(Session.WaitFor(() =>
        {
            outputs = Ports(output, "out", monitor);
            inputs = Ports(input, "in", false);
            return outputs.Count >= 2 && inputs.Count >= 2;
        }, 5000), $"node {output} has {outputs.Count} output ports and node {input} has {inputs.Count} input ports: " + string.Join(" | ", Session.Globals.Values.Where(g => g.IsPort && (g.GetProperty("node.id") == output.ToString() || g.GetProperty("node.id") == input.ToString())).Select(g => string.Join(",", g.Properties.Select(p => $"{p.Key}={p.Value}")))));
        foreach (var (channel, port) in outputs)
        {
            if (!inputs.TryGetValue(channel, out var target))
            {
                continue;
            }

            _links.Add(Session.Core.CreateObject("link-factory", PipeWireInterfaces.Link, 3,
            [
                new("link.output.node", output.ToString()),
                new("link.output.port", port.ToString()),
                new("link.input.node", input.ToString()),
                new("link.input.port", target.ToString()),
            ]));
        }

        Assert.True(Session.Roundtrip());
    }

    public void Pump(TimeSpan duration)
    {
        var deadline = Environment.TickCount64 + (long)duration.TotalMilliseconds;
        while (Environment.TickCount64 < deadline)
        {
            Session.Pump(10);
            Loop.Dispatch(0);
        }
    }

    public static void Stop(Process process)
    {
        if (!process.HasExited)
        {
            _ = Kill(process.Id, 2);
            if (!process.WaitForExit(3000))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(3000);
            }
        }
    }

    public void Dispose()
    {
        foreach (var process in _processes)
        {
            Stop(process);
            process.Dispose();
        }

        foreach (var link in _links)
        {
            link.Dispose();
        }

        Session.Dispose();
        Loop.Dispose();
        try
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "kill")]
    private static extern int Kill(int pid, int signal);

    private Process StartClient(string program, params string[] arguments)
    {
        var process = Process.Start(_daemon.Client(program, arguments)) ?? throw new InvalidOperationException($"{program} did not start");
        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        _processes.Add(process);
        return process;
    }

    private Dictionary<string, uint> Ports(uint node, string direction, bool monitor)
    {
        var ports = new Dictionary<string, uint>(StringComparer.Ordinal);
        foreach (var global in Session.Globals.Values)
        {
            if (!global.IsPort || global.GetProperty("node.id") != node.ToString() || global.GetProperty("port.direction") != direction)
            {
                continue;
            }

            if ((global.GetProperty("port.monitor") == "true") != monitor)
            {
                continue;
            }

            if (global.GetProperty("audio.channel") is { } channel)
            {
                ports[channel] = global.Id;
            }
        }

        return ports;
    }
}
