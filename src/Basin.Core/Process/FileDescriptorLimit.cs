using System.Runtime.InteropServices;
using Basin.Diagnostics;

namespace Basin;

public static class FileDescriptorLimit
{
    private const int ResourceOpenFiles = 7;

    private const ulong DesiredOpenFiles = 4096;

    private static Limit? _original;

    [StructLayout(LayoutKind.Sequential)]
    private struct Limit
    {
        public ulong Current;

        public ulong Maximum;
    }

    public static bool IsRaised => _original is not null;

    public static void Raise(BasinLogger log)
    {
        if (getrlimit(ResourceOpenFiles, out var original) != 0)
        {
            log.Error($"getrlimit failed, using the system default file descriptor limit");
            return;
        }

        _original = original;
        var raised = Raised(original);
        if (setrlimit(ResourceOpenFiles, in raised) != 0)
        {
            log.Error($"setrlimit failed, using the system default file descriptor limit of {original.Current}");
            _original = null;
            return;
        }

        log.Info($"raised the file descriptor limit of the compositor process to {raised.Current}");
    }

    public static bool LowerForSpawn() =>
        _original is { } original && setrlimit(ResourceOpenFiles, in original) == 0;

    public static void RaiseAfterSpawn(BasinLogger log)
    {
        if (_original is not { } original)
        {
            return;
        }

        var raised = Raised(original);
        if (setrlimit(ResourceOpenFiles, in raised) != 0)
        {
            log.Error($"failed to raise the file descriptor limit of the compositor process again");
        }
    }

    private static Limit Raised(Limit original) =>
        new() { Current = Math.Min(DesiredOpenFiles, original.Maximum), Maximum = original.Maximum };

    [DllImport("libc", SetLastError = true)]
    private static extern int getrlimit(int resource, out Limit limit);

    [DllImport("libc", SetLastError = true)]
    private static extern int setrlimit(int resource, in Limit limit);
}
