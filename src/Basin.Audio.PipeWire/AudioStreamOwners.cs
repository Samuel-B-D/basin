using System.Buffers;
using Basin.Capabilities;

namespace Basin.Audio.PipeWire;

public sealed class AudioStreamOwners : IToplevelObserver, IDisposable
{
    private const int MaxAncestors = 16;

    private readonly IToplevelModel _toplevels;
    private readonly Func<long, int>? _launchGroup;
    private readonly IAudioProcessTable _processes;
    private readonly Dictionary<ulong, long> _mapped = [];
    private readonly Dictionary<ulong, long> _focused = [];
    private readonly HashSet<ulong> _active = [];
    private long _clock;
    private bool _disposed;

    public AudioStreamOwners(IToplevelModel toplevels, Func<long, int>? launchGroup = null, IAudioProcessTable? processes = null)
    {
        ArgumentNullException.ThrowIfNull(toplevels);
        _toplevels = toplevels;
        _launchGroup = launchGroup;
        _processes = processes ?? AudioProcessTable.Instance;
        var buffer = ArrayPool<ToplevelInfo>.Shared.Rent(64);
        var count = toplevels.Enumerate(buffer);
        for (var i = 0; i < Math.Max(0, count); i++)
        {
            _mapped[buffer[i].Id] = ++_clock;
        }

        ArrayPool<ToplevelInfo>.Shared.Return(buffer, clearArray: true);
        toplevels.AddObserver(this);
    }

    public AudioStreamOwner Match(AudioStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var candidates = Candidates();
        try
        {
            if (candidates.Count == 0)
            {
                return default;
            }

            var native = !stream.ViaPulse && stream.VerifiedPid > 0;
            var reported = stream.ViaPulse && stream.ReportedPid > 0 && stream.FlatpakAppId is not { Length: > 0 };
            if (native && Pick(candidates, info => info.Pid == stream.VerifiedPid) is { } bySecPid)
            {
                return new AudioStreamOwner(bySecPid, AudioMatchStep.SecPid);
            }

            if (reported && Pick(candidates, info => info.Pid == stream.ReportedPid) is { } byReported)
            {
                return new AudioStreamOwner(byReported, AudioMatchStep.ReportedPid);
            }

            if (native && Ancestor(candidates, stream.VerifiedPid) is { } byAncestor)
            {
                return new AudioStreamOwner(byAncestor, AudioMatchStep.Ancestor);
            }

            if (reported && Ancestor(candidates, stream.ReportedPid) is { } byReportedAncestor)
            {
                return new AudioStreamOwner(byReportedAncestor, AudioMatchStep.ReportedPid);
            }

            if (stream.FlatpakAppId is { Length: > 0 } app &&
                Pick(candidates, info => _processes.FlatpakAppOf((int)info.Pid) == app) is { } byFlatpak)
            {
                return new AudioStreamOwner(byFlatpak, AudioMatchStep.Flatpak);
            }

            if (stream.LaunchId > 0 && _launchGroup?.Invoke(stream.LaunchId) is > 0 and var group &&
                Pick(candidates, info => _processes.GroupOf((int)info.Pid) == group) is { } byTag)
            {
                return new AudioStreamOwner(byTag, AudioMatchStep.LaunchTag);
            }

            return default;
        }
        finally
        {
            candidates.Clear();
        }
    }

    public void OnToplevelAdded(ulong toplevelId)
    {
        _mapped[toplevelId] = ++_clock;
        OnToplevelChanged(toplevelId);
    }

    public void OnToplevelChanged(ulong toplevelId)
    {
        if (!_toplevels.TryGet(toplevelId, out var info))
        {
            return;
        }

        if ((info.State & ToplevelState.Activated) != 0)
        {
            if (_active.Add(toplevelId))
            {
                _focused[toplevelId] = ++_clock;
            }
        }
        else
        {
            _active.Remove(toplevelId);
        }
    }

    public void OnToplevelRemoved(ulong toplevelId)
    {
        _mapped.Remove(toplevelId);
        _focused.Remove(toplevelId);
        _active.Remove(toplevelId);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _toplevels.RemoveObserver(this);
    }

    private readonly List<ToplevelInfo> _candidates = [];

    private List<ToplevelInfo> Candidates()
    {
        var length = 64;
        while (true)
        {
            var buffer = ArrayPool<ToplevelInfo>.Shared.Rent(length);
            var count = _toplevels.Enumerate(buffer);
            if (count < 0)
            {
                length = buffer.Length * 2;
                ArrayPool<ToplevelInfo>.Shared.Return(buffer, clearArray: true);
                continue;
            }

            for (var i = 0; i < count; i++)
            {
                if (buffer[i].Pid > 0)
                {
                    _candidates.Add(buffer[i]);
                }
            }

            ArrayPool<ToplevelInfo>.Shared.Return(buffer, clearArray: true);
            return _candidates;
        }
    }

    private ulong? Ancestor(List<ToplevelInfo> candidates, int pid)
    {
        var current = pid;
        for (var step = 0; step < MaxAncestors; step++)
        {
            current = _processes.ParentOf(current);
            if (current <= 1)
            {
                return null;
            }

            var parent = current;
            if (Pick(candidates, info => info.Pid == parent) is { } owner)
            {
                return owner;
            }
        }

        return null;
    }

    private ulong? Pick(List<ToplevelInfo> candidates, Func<ToplevelInfo, bool> owns)
    {
        ulong? best = null;
        var bestFocus = long.MinValue;
        var bestMapped = long.MaxValue;
        foreach (var info in candidates)
        {
            if (!owns(info))
            {
                continue;
            }

            var focus = _focused.TryGetValue(info.Id, out var focusedAt) ? focusedAt : long.MinValue;
            var mapped = _mapped.TryGetValue(info.Id, out var mappedAt) ? mappedAt : long.MaxValue - 1;
            if (focus > bestFocus || (focus == bestFocus && mapped < bestMapped))
            {
                best = info.Id;
                bestFocus = focus;
                bestMapped = mapped;
            }
        }

        return best;
    }
}
