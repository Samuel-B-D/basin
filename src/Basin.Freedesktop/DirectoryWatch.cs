using System.Runtime.InteropServices;

namespace Basin.Freedesktop;

public sealed partial class DirectoryWatch : IDisposable
{
    public const int CoalesceMillis = 100;

    private const int NonBlock = 0x800;
    private const int CloseOnExec = 0x80000;
    private const uint Mask = 0x2 | 0x4 | 0x8 | 0x40 | 0x80 | 0x100 | 0x200 | 0x400 | 0x800;

    private readonly ICompositorEventLoop? _loop;
    private readonly int _fd;
    private readonly IEventSource? _source;
    private readonly Dictionary<int, string> _watches = [];
    private readonly Dictionary<string, int> _paths = new(StringComparer.Ordinal);
    private readonly HashSet<string> _dirty = new(StringComparer.Ordinal);
    private readonly List<string> _flushing = [];
    private IEventSource? _flush;

    public DirectoryWatch(ICompositorEventLoop? loop = null)
    {
        _loop = loop;
        _fd = OperatingSystem.IsLinux() ? inotify_init1(NonBlock | CloseOnExec) : -1;
        if (_fd >= 0 && loop is not null)
        {
            _source = loop.AddFd(_fd, FdReadiness.Readable, OnReadable);
        }
    }

    public event Action<string>? Changed;

    public int Fd => _fd;

    public bool Available => _fd >= 0;

    public bool Add(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (_fd < 0 || _paths.ContainsKey(path))
        {
            return _fd >= 0;
        }

        var wd = inotify_add_watch(_fd, path, Mask);
        if (wd < 0)
        {
            return false;
        }

        _watches[wd] = path;
        _paths[path] = wd;
        return true;
    }

    public void Remove(string path)
    {
        if (_paths.Remove(path, out var wd))
        {
            _watches.Remove(wd);
            _ = inotify_rm_watch(_fd, wd);
        }
    }

    public unsafe bool Read()
    {
        if (_fd < 0)
        {
            return false;
        }

        var buffer = stackalloc byte[4096];
        while (true)
        {
            var read = read_fd(_fd, buffer, 4096);
            if (read <= 0)
            {
                break;
            }

            var offset = 0;
            while (offset + 16 <= read)
            {
                var wd = *(int*)(buffer + offset);
                var length = *(uint*)(buffer + offset + 12);
                if (_watches.TryGetValue(wd, out var path))
                {
                    _dirty.Add(path);
                }

                offset += 16 + (int)length;
            }
        }

        return _dirty.Count > 0;
    }

    public void Flush()
    {
        _flush?.Remove();
        _flush = null;
        _flushing.Clear();
        _flushing.AddRange(_dirty);
        _dirty.Clear();
        foreach (var path in _flushing)
        {
            Changed?.Invoke(path);
        }

        _flushing.Clear();
    }

    public void Dispose()
    {
        _flush?.Remove();
        _flush = null;
        _source?.Remove();
        if (_fd >= 0)
        {
            _ = close(_fd);
        }
    }

    private void OnReadable(int fd, FdReadiness readiness)
    {
        if (Read() && _flush is null && _loop is not null)
        {
            _flush = _loop.AddTimer(Flush);
            _flush.UpdateTimer(CoalesceMillis);
        }
    }

    [LibraryImport("libc", SetLastError = true)]
    private static partial int inotify_init1(int flags);

    [LibraryImport("libc", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int inotify_add_watch(int fd, string path, uint mask);

    [LibraryImport("libc", SetLastError = true)]
    private static partial int inotify_rm_watch(int fd, int wd);

    [LibraryImport("libc", EntryPoint = "read", SetLastError = true)]
    private static unsafe partial nint read_fd(int fd, byte* buffer, nint count);

    [LibraryImport("libc", SetLastError = true)]
    private static partial int close(int fd);
}
