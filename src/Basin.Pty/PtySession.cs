using System.Buffers;
using System.Collections;
using Basin.Diagnostics;
using static Basin.Pty.PtyLog;

namespace Basin.Pty;

public sealed class PtySession : IDisposable
{
    public const int ReadBudget = 64 * 1024;

    private const int ExitDrainMs = 100;
    private const int ReapPollMs = 20;

    private readonly ICompositorEventLoop _loop;
    private readonly byte[] _buffer;
    private int _master;
    private int _pidfd;
    private IEventSource? _masterWatch;
    private IEventSource? _pidWatch;
    private IEventSource? _timer;
    private byte[] _pending = [];
    private int _pendingStart;
    private int _pendingLength;
    private bool _masterEof;
    private PtyExit? _exit;
    private bool _finished;
    private bool _disposed;

    private PtySession(ICompositorEventLoop loop, int master, int pid)
    {
        _loop = loop;
        _master = master;
        _pidfd = -1;
        ProcessId = pid;
        _buffer = ArrayPool<byte>.Shared.Rent(ReadBudget);
        BasinCounters.Track();
    }

    public int ProcessId { get; }

    public bool Running => !_finished && !_disposed;

    public PtyExit? ExitState => _finished ? _exit : null;

    public int QueuedBytes => _pendingLength;

    public static int Orphans => PtyReaper.Pending;

    public event Action<ReadOnlySpan<byte>>? Output;

    public event Action<PtyExit>? Exited;

    public static PtySession? TryStart(ICompositorEventLoop loop, PtyStartInfo start, out string? failure)
    {
        ArgumentNullException.ThrowIfNull(loop);
        ArgumentNullException.ThrowIfNull(start);
        if (start.Argv.Count == 0 || string.IsNullOrEmpty(start.Argv[0]))
        {
            failure = "no program to run";
            return null;
        }

        var master = PtyNative.OpenMaster(out var slave, out failure);
        if (master < 0)
        {
            return null;
        }

        PtyNative.SetUtf8(slave!);
        _ = PtyNative.SetWindow(master, start.Columns, start.Rows, start.PixelWidth, start.PixelHeight);
        var pid = PtyNative.Spawn(start.Argv, EnvironmentOf(start.Environment), start.WorkingDirectory, slave!, out failure);
        if (pid < 0)
        {
            _ = PtyNative.Close(master);
            return null;
        }

        var session = new PtySession(loop, master, pid);
        session.Watch();
        return session;
    }

    public static void CollectOrphans(TimeSpan grace) => PtyReaper.Collect(grace);

    public void Write(ReadOnlySpan<byte> data)
    {
        if (_disposed || _finished || data.IsEmpty)
        {
            return;
        }

        if (_pendingLength == 0)
        {
            var written = PtyNative.Write(_master, data, out var errno);
            if (written < 0)
            {
                if (errno != PtyNative.EAgain)
                {
                    Log.Debug($"write to pid {ProcessId}'s terminal failed: errno {errno}");
                    return;
                }

                written = 0;
            }

            data = data[written..];
            if (data.IsEmpty)
            {
                return;
            }
        }

        Queue(data);
        _masterWatch?.UpdateFd(FdReadiness.Readable | FdReadiness.Writable);
    }

    public void Resize(int columns, int rows, int pixelWidth, int pixelHeight)
    {
        if (_disposed || _master < 0)
        {
            return;
        }

        _ = PtyNative.SetWindow(_master, columns, rows, pixelWidth, pixelHeight);
    }

    public void Signal(int signal)
    {
        if (_disposed || _exit is not null)
        {
            return;
        }

        _ = PtyNative.Kill(-ProcessId, signal);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _masterWatch?.Remove();
        _masterWatch = null;
        _pidWatch?.Remove();
        _pidWatch = null;
        _timer?.Remove();
        _timer = null;
        if (_exit is null)
        {
            _ = PtyNative.Kill(-ProcessId, PtyNative.SignalHangup);
            _ = PtyNative.Kill(ProcessId, PtyNative.SignalHangup);
        }

        _ = PtyNative.Close(_master);
        _master = -1;
        if (_exit is null && !PtyNative.TryReap(ProcessId, out _))
        {
            PtyReaper.Adopt(_loop, ProcessId, _pidfd);
        }
        else if (_pidfd >= 0)
        {
            _ = PtyNative.Close(_pidfd);
        }

        _pidfd = -1;
        ArrayPool<byte>.Shared.Return(_buffer);
        BasinCounters.Untrack();
    }

    private static List<string> EnvironmentOf(IReadOnlyDictionary<string, string?> overrides)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in System.Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string key && entry.Value is string value)
            {
                values[key] = value;
            }
        }

        foreach (var (key, value) in overrides)
        {
            if (value is null)
            {
                values.Remove(key);
            }
            else
            {
                values[key] = value;
            }
        }

        var entries = new List<string>(values.Count);
        foreach (var (key, value) in values)
        {
            entries.Add($"{key}={value}");
        }

        return entries;
    }

    private void Watch()
    {
        _masterWatch = _loop.AddFd(_master, FdReadiness.Readable, OnMaster);
        _pidfd = PtyNative.PidfdOpenOf(ProcessId);
        if (_pidfd < 0)
        {
            Log.Debug($"pidfd_open({ProcessId}) failed; the child is reaped after its terminal closes");
            return;
        }

        _pidWatch = _loop.AddFd(_pidfd, FdReadiness.Readable, OnPidfd);
    }

    private void OnMaster(int fd, FdReadiness readiness)
    {
        try
        {
            if ((readiness & FdReadiness.Writable) != 0)
            {
                Flush();
            }

            if ((readiness & (FdReadiness.Readable | FdReadiness.Hangup | FdReadiness.Error)) != 0)
            {
                Drain(ReadBudget);
            }
        }
        catch (Exception exception)
        {
            Log.Error($"a terminal observer failed: {exception}");
        }
    }

    private void OnPidfd(int fd, FdReadiness readiness)
    {
        try
        {
            if (!TryReap())
            {
                return;
            }

            if (_masterEof)
            {
                Finish();
            }
            else
            {
                _timer ??= _loop.AddTimer(OnTimer);
                _timer.UpdateTimer(ExitDrainMs);
            }
        }
        catch (Exception exception)
        {
            Log.Error($"a terminal observer failed: {exception}");
        }
    }

    private void OnTimer()
    {
        try
        {
            if (_disposed || _finished)
            {
                return;
            }

            if (_exit is not null)
            {
                Drain(ReadBudget);
                Finish();
                return;
            }

            if (TryReap())
            {
                Finish();
            }
            else
            {
                _timer!.UpdateTimer(ReapPollMs);
            }
        }
        catch (Exception exception)
        {
            Log.Error($"a terminal observer failed: {exception}");
        }
    }

    private bool TryReap()
    {
        if (_exit is not null)
        {
            return true;
        }

        if (!PtyNative.TryReap(ProcessId, out var status))
        {
            return false;
        }

        _exit = PtyExit.FromWaitStatus(status);
        _pidWatch?.Remove();
        _pidWatch = null;
        if (_pidfd >= 0)
        {
            _ = PtyNative.Close(_pidfd);
            _pidfd = -1;
        }

        return true;
    }

    private void Drain(int budget)
    {
        while (budget > 0 && !_masterEof && !_disposed)
        {
            var count = PtyNative.Read(_master, _buffer.AsSpan(0, Math.Min(budget, ReadBudget)), out var errno);
            if (count > 0)
            {
                budget -= count;
                Output?.Invoke(_buffer.AsSpan(0, count));
                continue;
            }

            if (count < 0 && errno == PtyNative.EAgain)
            {
                return;
            }

            if (count < 0 && errno != PtyNative.EIo)
            {
                Log.Debug($"read from pid {ProcessId}'s terminal failed: errno {errno}");
            }

            OnEof();
        }
    }

    private void OnEof()
    {
        _masterEof = true;
        _masterWatch?.Remove();
        _masterWatch = null;
        if (TryReap())
        {
            Finish();
            return;
        }

        if (_pidWatch is null)
        {
            _timer ??= _loop.AddTimer(OnTimer);
            _timer.UpdateTimer(ReapPollMs);
        }
    }

    private void Finish()
    {
        if (_finished || _disposed || _exit is not { } exit)
        {
            return;
        }

        _finished = true;
        _masterWatch?.Remove();
        _masterWatch = null;
        _timer?.Remove();
        _timer = null;
        _pendingLength = 0;
        Exited?.Invoke(exit);
    }

    private void Queue(ReadOnlySpan<byte> data)
    {
        if (_pendingStart + _pendingLength + data.Length > _pending.Length)
        {
            if (_pendingLength + data.Length <= _pending.Length)
            {
                _pending.AsSpan(_pendingStart, _pendingLength).CopyTo(_pending);
            }
            else
            {
                var grown = new byte[Math.Max(_pending.Length * 2, _pendingLength + data.Length)];
                _pending.AsSpan(_pendingStart, _pendingLength).CopyTo(grown);
                _pending = grown;
            }

            _pendingStart = 0;
        }

        data.CopyTo(_pending.AsSpan(_pendingStart + _pendingLength));
        _pendingLength += data.Length;
    }

    private void Flush()
    {
        while (_pendingLength > 0)
        {
            var written = PtyNative.Write(_master, _pending.AsSpan(_pendingStart, _pendingLength), out var errno);
            if (written < 0)
            {
                if (errno == PtyNative.EAgain)
                {
                    return;
                }

                Log.Debug($"write to pid {ProcessId}'s terminal failed: errno {errno}");
                _pendingLength = 0;
                break;
            }

            _pendingStart += written;
            _pendingLength -= written;
        }

        _pendingStart = 0;
        _masterWatch?.UpdateFd(FdReadiness.Readable);
    }
}
