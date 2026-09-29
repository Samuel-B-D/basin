using System.Runtime.InteropServices;
using Basin;

namespace System7Wm;

internal sealed partial class DirectoryWatch : IDisposable
{
    private const int NonBlock = 0x800;
    private const int CloseOnExec = 0x80000;
    private const uint Mask = 0x2 | 0x4 | 0x8 | 0x40 | 0x80 | 0x100 | 0x200 | 0x400 | 0x800;

    private readonly ICompositorEventLoop _loop;
    private readonly int _fd;
    private readonly IEventSource? _source;
    private readonly Dictionary<int, string> _watches = [];
    private readonly Dictionary<string, int> _paths = new(StringComparer.Ordinal);
    private readonly HashSet<string> _dirty = new(StringComparer.Ordinal);
    private IEventSource? _flush;

    public DirectoryWatch(ICompositorEventLoop loop)
    {
        _loop = loop;
        _fd = inotify_init1(NonBlock | CloseOnExec);
        if (_fd >= 0)
        {
            _source = loop.AddFd(_fd, FdReadiness.Readable, OnReadable);
        }
    }

    public event Action<string>? Changed;

    public bool Add(string path)
    {
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

    public void Dispose()
    {
        _flush?.Remove();
        _source?.Remove();
        if (_fd >= 0)
        {
            _ = close(_fd);
        }
    }

    private unsafe void OnReadable(int fd, FdReadiness readiness)
    {
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

        if (_dirty.Count > 0 && _flush is null)
        {
            _flush = _loop.AddTimer(Flush);
            _flush.UpdateTimer(100);
        }
    }

    private void Flush()
    {
        _flush?.Remove();
        _flush = null;
        var paths = _dirty.ToList();
        _dirty.Clear();
        foreach (var path in paths)
        {
            Changed?.Invoke(path);
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
