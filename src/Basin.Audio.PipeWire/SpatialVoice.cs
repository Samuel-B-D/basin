using System.Diagnostics;
using System.Globalization;
using Basin.Diagnostics;
using PipeWire;
using PipeWire.Native;
using PipeWire.Spa;
using static Basin.Audio.PipeWire.AudioLog;
using static PipeWire.Native.Pipewire;

namespace Basin.Audio.PipeWire;

public sealed unsafe class SpatialVoice : IDisposable
{
    public const float AzimuthQuantum = 2;

    public const float ElevationQuantum = 5;

    public const float GainQuantumDb = 0.25f;

    public const float CutoffQuantumOctaves = 1f / 12;

    public const float RadiusQuantum = 0.1f;

    private const string TargetObject = "target.object";

    private const string TargetNode = "target.node";

    private const int Freq = 0;
    private const int Gain = 1;
    private const int Left = 2;
    private const int Right = 3;
    private const int Azimuth = 4;
    private const int Elevation = 5;
    private const int Radius = 6;
    private const int WetLeft = 7;
    private const int WetRight = 8;

    private static readonly string[] ControlNames =
    [
        "occlude:Freq", "gain:Mult", "left:Mult", "right:Mult",
        "hrtf:Azimuth", "hrtf:Elevation", "hrtf:Radius", "mixL:Gain 2", "mixR:Gain 2",
    ];

    private readonly AudioSession _session;
    private readonly PipeWireModule _module;
    private readonly PipeWireNode _node;
    private readonly SpaPodBuilder _builder = new(512);
    private readonly IEventSource _timer;
    private readonly HashSet<uint> _streams = [];
    private readonly HashSet<uint> _awaitingEcho = [];
    private readonly float[] _sent = new float[ControlNames.Length];
    private readonly float[] _wanted = new float[ControlNames.Length];
    private readonly bool[] _dirty = new bool[ControlNames.Length];
    private readonly string _serialText;
    private readonly string _nodeText;
    private long _lastHrtf;
    private bool _hrtfPending;
    private bool _timerArmed;
    private bool _disposed;

    private SpatialVoice(AudioSession session, VoiceGraph graph, string name, PipeWireModule module, PipeWireNode node, uint nodeId, ulong serial)
    {
        _session = session;
        Graph = graph;
        Name = name;
        _module = module;
        _node = node;
        CaptureNodeId = nodeId;
        CaptureSerial = serial;
        _serialText = serial.ToString(CultureInfo.InvariantCulture);
        _nodeText = nodeId.ToString(CultureInfo.InvariantCulture);
        Array.Fill(_sent, float.NaN);
        _timer = session.Loop.AddTimer(OnHrtfTimer);
        session.MetadataChanged += OnMetadataChanged;
        BasinCounters.Track();
    }

    public static SpatialVoice? Create(AudioSession session, VoiceGraph graph, string name, out string? error, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (graph.Mode == VoiceMode.Hrtf && (graph.HrtfFile is not { Length: > 0 } file || !File.Exists(file)))
        {
            error = $"the HRTF file {graph.HrtfFile} does not exist";
            return null;
        }

        PipeWireModule module;
        session.AddVoiceNodes(name, name + ".output");
        try
        {
            module = session.Context.LoadModule("libpipewire-module-filter-chain", graph.Arguments(name, session.Remote, description ?? name));
        }
        catch (PipeWireException failure)
        {
            session.RemoveVoiceNodes(name, name + ".output");
            error = failure.Message;
            return null;
        }

        PipeWireGlobal? capture = null;
        if (!session.WaitFor(() => (capture = session.FindNode(name)) is not null))
        {
            module.Dispose();
            session.RemoveVoiceNodes(name, name + ".output");
            error = "the filter-chain module created no capture node";
            return null;
        }

        var serial = ulong.TryParse(capture!.GetProperty("object.serial"), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        var node = session.Registry.BindNode(capture.Id);
        error = null;
        return new SpatialVoice(session, graph, name, module, node, capture.Id, serial);
    }

    public string Name { get; }

    public VoiceGraph Graph { get; }

    public uint CaptureNodeId { get; }

    public ulong CaptureSerial { get; }

    public IReadOnlyCollection<uint> Streams => _streams;

    public VoiceParams Current { get; private set; } = VoiceParams.Centered;

    public TimeSpan MinHrtfInterval { get; set; } = TimeSpan.FromSeconds(1.0 / 30);

    public long ParamWrites { get; private set; }

    public long HrtfWrites { get; private set; }

    public event Action<SpatialVoice, uint>? Released;

    public bool Attach(AudioStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return Attach(stream.NodeId);
    }

    public bool Attach(uint streamNodeId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_session.DefaultMetadata is not { } metadata)
        {
            return false;
        }

        _streams.Add(streamNodeId);
        _awaitingEcho.Add(streamNodeId);
        metadata.SetProperty(streamNodeId, TargetObject, "Spa:Id", _serialText);
        return true;
    }

    public bool Detach(uint streamNodeId)
    {
        if (!_streams.Remove(streamNodeId))
        {
            return false;
        }

        _awaitingEcho.Remove(streamNodeId);

        if (_session.Globals.ContainsKey(streamNodeId))
        {
            _session.DefaultMetadata?.SetProperty(streamNodeId, TargetObject, null, null);
        }

        return true;
    }

    public void Forget(uint streamNodeId)
    {
        _streams.Remove(streamNodeId);
        _awaitingEcho.Remove(streamNodeId);
    }

    public void Update(in VoiceParams wanted)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var q = Quantize(wanted);
        Current = q;
        if (Graph.Mode == VoiceMode.Stereo)
        {
            var level = Graph.Reverb ? Linear(q.WetDb) : 0;
            _wanted[WetLeft] = level;
            _wanted[WetRight] = level;
            if (Graph.Reverb)
            {
                Mark(WetLeft);
                Mark(WetRight);
            }

            Send();
            return;
        }

        _wanted[Freq] = q.CutoffHz;
        _wanted[Gain] = Linear(q.GainDb);
        var wet = Graph.Reverb ? Linear(q.WetDb) : 0;
        _wanted[WetLeft] = wet;
        _wanted[WetRight] = wet;
        if (Graph.Mode == VoiceMode.Hrtf)
        {
            _wanted[Azimuth] = q.Azimuth;
            _wanted[Elevation] = q.Elevation;
            _wanted[Radius] = q.Radius;
        }
        else
        {
            var lateral = MathF.Sin(q.Azimuth * MathF.PI / 180);
            var theta = (1 - lateral) * 0.25f * MathF.PI;
            _wanted[Left] = MathF.Round(MathF.Cos(theta), 4);
            _wanted[Right] = MathF.Round(MathF.Sin(theta), 4);
        }

        Mark(Freq);
        Mark(Gain);
        if (Graph.Reverb)
        {
            Mark(WetLeft);
            Mark(WetRight);
        }

        if (Graph.Mode == VoiceMode.Hrtf)
        {
            if (Differs(Azimuth) || Differs(Elevation) || Differs(Radius))
            {
                if (HrtfDue())
                {
                    MarkHrtf();
                }
                else
                {
                    _hrtfPending = true;
                    ArmTimer();
                }
            }
        }
        else
        {
            Mark(Left);
            Mark(Right);
        }

        Send();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _session.MetadataChanged -= OnMetadataChanged;
        if (_session.DefaultMetadata is { } metadata)
        {
            foreach (var stream in _streams)
            {
                if (_session.Globals.ContainsKey(stream))
                {
                    metadata.SetProperty(stream, TargetObject, null, null);
                }
            }
        }

        _streams.Clear();
        _timer.Remove();
        _node.Dispose();
        _session.Roundtrip(500);
        _module.Dispose();
        _session.RemoveVoiceNodes(Name, Name + ".output");
        _builder.Dispose();
        BasinCounters.Untrack();
    }

    internal static VoiceParams Quantize(in VoiceParams wanted)
    {
        var azimuth = Step(wanted.Azimuth, AzimuthQuantum) % 360;
        if (azimuth < 0)
        {
            azimuth += 360;
        }

        var gain = wanted.GainDb <= VoiceParams.SilentDb ? VoiceParams.SilentDb : MathF.Min(20, Step(wanted.GainDb, GainQuantumDb));
        var wet = wanted.WetDb <= VoiceParams.SilentDb ? VoiceParams.SilentDb : MathF.Min(20, Step(wanted.WetDb, GainQuantumDb));
        var cutoff = Math.Clamp(wanted.CutoffHz, 20, VoiceParams.OpenCutoffHz);
        cutoff = VoiceParams.OpenCutoffHz * MathF.Pow(2, MathF.Min(0, Step(MathF.Log2(cutoff / VoiceParams.OpenCutoffHz), CutoffQuantumOctaves)));
        return new VoiceParams(
            azimuth,
            Math.Clamp(Step(wanted.Elevation, ElevationQuantum), -90, 90),
            MathF.Max(0, Step(wanted.Radius, RadiusQuantum)),
            gain,
            MathF.Round(cutoff, 1),
            wet);
    }

    private static float Step(float value, float quantum) => MathF.Round(value / quantum) * quantum;

    private static float Linear(float db) => db <= VoiceParams.SilentDb ? 0 : MathF.Pow(10, db / 20);

    private bool Differs(int control) => !(_sent[control] == _wanted[control]);

    private void Mark(int control) => _dirty[control] |= Differs(control);

    private void MarkHrtf()
    {
        var clamped = Math.Clamp(_wanted[Radius], Graph.HrtfRadiusMin, Graph.HrtfRadiusMax);
        _wanted[Radius] = clamped;
        Mark(Azimuth);
        Mark(Elevation);
        Mark(Radius);
        if (_dirty[Azimuth] || _dirty[Elevation] || _dirty[Radius])
        {
            _lastHrtf = Stopwatch.GetTimestamp();
            HrtfWrites++;
        }

        _hrtfPending = false;
    }

    private bool HrtfDue() =>
        Stopwatch.GetElapsedTime(_lastHrtf) >= MinHrtfInterval;

    private void ArmTimer()
    {
        if (_timerArmed)
        {
            return;
        }

        var remaining = MinHrtfInterval - Stopwatch.GetElapsedTime(_lastHrtf);
        _timer.UpdateTimer(Math.Max(1, (int)Math.Ceiling(remaining.TotalMilliseconds)));
        _timerArmed = true;
    }

    private void OnHrtfTimer()
    {
        _timerArmed = false;
        if (_disposed || !_hrtfPending)
        {
            return;
        }

        MarkHrtf();
        Send();
    }

    private void Send()
    {
        var any = false;
        for (var i = 0; i < _dirty.Length; i++)
        {
            any |= _dirty[i];
        }

        if (!any)
        {
            return;
        }

        _builder.Reset();
        using (_builder.PushObject(SPA_TYPE_OBJECT_Props, (uint)spa_param_type.SPA_PARAM_Props))
        {
            _builder.AddProperty((uint)spa_prop.SPA_PROP_params);
            using (_builder.PushStruct())
            {
                for (var i = 0; i < _dirty.Length; i++)
                {
                    if (!_dirty[i])
                    {
                        continue;
                    }

                    _builder.AddString(ControlNames[i]);
                    _builder.AddFloat(_wanted[i]);
                    _sent[i] = _wanted[i];
                    _dirty[i] = false;
                }
            }
        }

        if (!SpaInterface.TryGetMethods((spa_interface*)_node.Handle, PW_VERSION_NODE_METHODS, out pw_node_methods* methods, out var data) ||
            methods->set_param is null)
        {
            return;
        }

        fixed (byte* pod = _builder.WrittenSpan)
        {
            var result = methods->set_param(data, (uint)spa_param_type.SPA_PARAM_Props, 0, (spa_pod*)pod);
            if (result < 0)
            {
                Log.Warn($"voice {Name}: set_param failed ({result})");
            }
        }

        ParamWrites++;
    }

    private void OnMetadataChanged(PipeWireMetadataProperty property)
    {
        if (!_streams.Contains(property.Subject))
        {
            return;
        }

        if (_awaitingEcho.Contains(property.Subject))
        {
            if (property.Key == TargetObject && property.Value == _serialText)
            {
                _awaitingEcho.Remove(property.Subject);
            }

            return;
        }

        var ours = property.Key switch
        {
            TargetObject => property.Value == _serialText,
            TargetNode => property.Value is null || property.Value == _nodeText,
            null => true,
            _ => true,
        };
        if (ours)
        {
            return;
        }

        _streams.Remove(property.Subject);
        _awaitingEcho.Remove(property.Subject);
        Released?.Invoke(this, property.Subject);
    }
}
