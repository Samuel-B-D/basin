using Basin.Diagnostics;
using PipeWire;
using PipeWire.Native;
using static Basin.Audio.PipeWire.AudioLog;

namespace Basin.Audio.PipeWire;

public sealed unsafe class AudioSession : IDisposable
{
    private const string DefaultMetadataName = "default";

    private readonly ICompositorEventLoop _loop;
    private readonly PipeWireLoop _pwLoop;
    private readonly PipeWireContext _context;
    private readonly PipeWireCore _core;
    private readonly PipeWireRegistry _registry;
    private readonly IEventSource _fdSource;
    private readonly Dictionary<uint, PipeWireGlobal> _globals = [];
    private readonly HashSet<string> _voiceNodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _defaults = new(StringComparer.Ordinal);
    private PipeWireMetadata? _metadata;
    private uint _metadataId = uint.MaxValue;
    private int _pendingSync = -1;
    private bool _synced;
    private bool _disposed;

    private AudioSession(ICompositorEventLoop loop, PipeWireLoop pwLoop, PipeWireContext context, PipeWireCore core, string remote)
    {
        _loop = loop;
        _pwLoop = pwLoop;
        _context = context;
        _core = core;
        Remote = remote;
        _core.Done += (_, done) =>
        {
            if (done.Sequence == _pendingSync)
            {
                _synced = true;
            }
        };
        _core.Error += (_, error) => Log.Warn($"PipeWire error on object {error.Id}: {error.Message} ({error.Result})");
        _registry = _core.GetRegistry();
        _registry.GlobalAdded += (_, global) => OnGlobalAdded(global);
        _registry.GlobalRemoved += (_, id) => OnGlobalRemoved(id);
        _fdSource = loop.AddFd(pwLoop.Fd, FdReadiness.Readable, (_, _) => Pump(0));
        BasinCounters.Track();
    }

    public static AudioSession? TryCreate(ICompositorEventLoop loop, out string? reason, string? remote = null, string? clientName = null)
    {
        ArgumentNullException.ThrowIfNull(loop);
        if (!LibraryLoads(out reason))
        {
            return null;
        }

        remote ??= Environment.GetEnvironmentVariable("PIPEWIRE_REMOTE") is { Length: > 0 } fromEnvironment ? fromEnvironment : "pipewire-0";
        PipeWireLoop? pwLoop = null;
        PipeWireContext? context = null;
        try
        {
            PipeWireLibrary.Init();
            pwLoop = PipeWireLoop.Create();
            context = new PipeWireContext(pwLoop);
            using var properties = PipeWireProperties.From(
                "remote.name", remote,
                "application.name", clientName ?? "basin",
                "node.name", clientName ?? "basin");
            var core = context.Connect(properties);
            var session = new AudioSession(loop, pwLoop, context, core, remote);
            if (!session.Roundtrip())
            {
                session.Dispose();
                reason = $"the PipeWire daemon at {remote} did not answer";
                return null;
            }

            reason = null;
            return session;
        }
        catch (Exception error) when (error is DllNotFoundException or PipeWireException or EntryPointNotFoundException)
        {
            context?.Dispose();
            pwLoop?.Dispose();
            reason = error.Message;
            return null;
        }
    }

    public string Remote { get; }

    public ICompositorEventLoop Loop => _loop;

    public PipeWireContext Context => _context;

    public PipeWireCore Core => _core;

    public PipeWireRegistry Registry => _registry;

    public PipeWireMetadata? DefaultMetadata => _metadata;

    public IReadOnlyDictionary<uint, PipeWireGlobal> Globals => _globals;

    public event Action<PipeWireGlobal>? GlobalAdded;

    public event Action<uint>? GlobalRemoved;

    public event Action<PipeWireMetadataProperty>? MetadataChanged;

    public void Pump(int timeoutMilliseconds)
    {
        if (_disposed)
        {
            return;
        }

        _pwLoop.Enter();
        try
        {
            _pwLoop.Iterate(timeoutMilliseconds);
        }
        finally
        {
            _pwLoop.Leave();
        }
    }

    public bool Roundtrip(int timeoutMilliseconds = 2000)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _synced = false;
        _pendingSync = _core.Sync();
        var deadline = Environment.TickCount64 + timeoutMilliseconds;
        while (!_synced && Environment.TickCount64 < deadline)
        {
            Pump(20);
        }

        return _synced;
    }

    public bool WaitFor(Func<bool> condition, int timeoutMilliseconds = 2000)
    {
        ArgumentNullException.ThrowIfNull(condition);
        var deadline = Environment.TickCount64 + timeoutMilliseconds;
        while (!condition())
        {
            if (Environment.TickCount64 >= deadline)
            {
                return false;
            }

            Pump(20);
        }

        return true;
    }

    public string? DefaultProperty(string key) => _defaults.GetValueOrDefault(key);

    public bool IsVoiceNode(string? nodeName) => nodeName is not null && _voiceNodes.Contains(nodeName);

    internal void AddVoiceNodes(string captureName, string playbackName)
    {
        _voiceNodes.Add(captureName);
        _voiceNodes.Add(playbackName);
    }

    internal void RemoveVoiceNodes(string captureName, string playbackName)
    {
        _voiceNodes.Remove(captureName);
        _voiceNodes.Remove(playbackName);
    }

    internal void AddOwnNode(string name) => _voiceNodes.Add(name);

    internal void RemoveOwnNode(string name) => _voiceNodes.Remove(name);

    public PipeWireGlobal? FindNode(string nodeName)
    {
        foreach (var global in _globals.Values)
        {
            if (global.IsNode && global.GetProperty("node.name") == nodeName)
            {
                return global;
            }
        }

        return null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _fdSource.Remove();
        _metadata?.Dispose();
        _metadata = null;
        _registry.Dispose();
        _core.Dispose();
        _context.Dispose();
        _pwLoop.Dispose();
        _globals.Clear();
        _disposed = true;
        BasinCounters.Untrack();
    }

    private static bool LibraryLoads(out string? reason)
    {
        foreach (var candidate in (ReadOnlySpan<string>)["libpipewire-0.3.so.0", "libpipewire-0.3.so"])
        {
            if (System.Runtime.InteropServices.NativeLibrary.TryLoad(candidate, out var handle))
            {
                System.Runtime.InteropServices.NativeLibrary.Free(handle);
                reason = null;
                return true;
            }
        }

        reason = "libpipewire-0.3.so.0 is not installed";
        return false;
    }

    private void OnGlobalAdded(PipeWireGlobal global)
    {
        _globals[global.Id] = global;
        if (_metadata is null && global.Type == PipeWireInterfaces.Metadata && global.GetProperty("metadata.name") == DefaultMetadataName)
        {
            _metadataId = global.Id;
            _metadata = _registry.BindMetadata(global.Id);
            _metadata.PropertyChanged += (_, property) =>
            {
                if (property.Subject == 0 && property.Key is { } key)
                {
                    if (property.Value is { } value)
                    {
                        _defaults[key] = value;
                    }
                    else
                    {
                        _defaults.Remove(key);
                    }
                }
                else if (property.Subject == 0 && property.Key is null)
                {
                    _defaults.Clear();
                }

                MetadataChanged?.Invoke(property);
            };
        }

        GlobalAdded?.Invoke(global);
    }

    private void OnGlobalRemoved(uint id)
    {
        _globals.Remove(id);
        if (id == _metadataId)
        {
            _metadata?.Dispose();
            _metadata = null;
            _metadataId = uint.MaxValue;
        }

        GlobalRemoved?.Invoke(id);
    }
}
