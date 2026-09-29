using System.Runtime.InteropServices;

namespace Basin.Pty;

internal static unsafe class PtyNative
{
    public const int ORdWr = 0x2;
    public const int ONoCtty = 0x100;
    public const int ONonBlock = 0x800;
    public const int OCloExec = 0x80000;

    public const int EIntr = 4;
    public const int EIo = 5;
    public const int EAgain = 11;

    public const int SignalHangup = 1;
    public const int SignalKill = 9;
    public const int SignalPipe = 13;

    private const short SpawnSetSigDefault = 0x04;
    private const short SpawnSetSigMask = 0x08;
    private const short SpawnSetSid = 0x80;
    private const int AttrBytes = 512;
    private const int ActionBytes = 256;
    private const int SigsetBytes = 128;
    private const int TermiosBytes = 64;
    private const uint InputUtf8 = 0x4000;
    private const nuint SetWindowSize = 0x5414;
    private const long PidfdOpen = 434;
    private const int WaitNoHang = 1;

    public static int OpenMaster(out string? slave, out string? error)
    {
        slave = null;
        var fd = posix_openpt(ORdWr | ONoCtty | OCloExec | ONonBlock);
        if (fd < 0)
        {
            error = $"posix_openpt: {Marshal.GetPInvokeErrorMessage(Marshal.GetLastPInvokeError())}";
            return -1;
        }

        if (grantpt(fd) != 0 || unlockpt(fd) != 0)
        {
            error = $"grantpt/unlockpt: {Marshal.GetPInvokeErrorMessage(Marshal.GetLastPInvokeError())}";
            _ = close(fd);
            return -1;
        }

        var name = stackalloc byte[128];
        var result = ptsname_r(fd, name, 128);
        if (result != 0)
        {
            error = $"ptsname_r: {Marshal.GetPInvokeErrorMessage(result)}";
            _ = close(fd);
            return -1;
        }

        slave = Marshal.PtrToStringUTF8((nint)name);
        error = null;
        return fd;
    }

    public static void SetUtf8(string slave)
    {
        var fd = open(slave, ORdWr | ONoCtty | OCloExec);
        if (fd < 0)
        {
            return;
        }

        var termios = stackalloc byte[TermiosBytes];
        if (tcgetattr(fd, termios) == 0)
        {
            *(uint*)termios |= InputUtf8;
            _ = tcsetattr(fd, 0, termios);
        }

        _ = close(fd);
    }

    public static bool SetWindow(int fd, int columns, int rows, int pixelWidth, int pixelHeight)
    {
        var size = stackalloc ushort[4];
        size[0] = (ushort)Math.Clamp(rows, 1, ushort.MaxValue);
        size[1] = (ushort)Math.Clamp(columns, 1, ushort.MaxValue);
        size[2] = (ushort)Math.Clamp(pixelWidth, 0, ushort.MaxValue);
        size[3] = (ushort)Math.Clamp(pixelHeight, 0, ushort.MaxValue);
        return ioctl(fd, SetWindowSize, size) == 0;
    }

    public static int Spawn(IReadOnlyList<string> argv, IReadOnlyList<string> environment, string? cwd, string slave, out string? error)
    {
        var attributes = Marshal.AllocHGlobal(AttrBytes);
        var actions = Marshal.AllocHGlobal(ActionBytes);
        var mask = Marshal.AllocHGlobal(SigsetBytes);
        var defaults = Marshal.AllocHGlobal(SigsetBytes);
        var argvBlock = Block(argv);
        var envBlock = Block(environment);
        try
        {
            _ = posix_spawnattr_init(attributes);
            _ = posix_spawn_file_actions_init(actions);
            _ = sigemptyset(mask);
            _ = sigfillset(defaults);
            _ = sigdelset(defaults, SignalKill);
            _ = sigdelset(defaults, 19);
            _ = posix_spawnattr_setsigmask(attributes, mask);
            _ = posix_spawnattr_setsigdefault(attributes, defaults);
            _ = posix_spawnattr_setflags(attributes, (short)(SpawnSetSid | SpawnSetSigMask | SpawnSetSigDefault));
            if (cwd is not null && posix_spawn_file_actions_addchdir_np(actions, cwd) != 0)
            {
                error = $"cannot change to '{cwd}'";
                return -1;
            }

            if (posix_spawn_file_actions_addopen(actions, 0, slave, ORdWr, 0) != 0
                || posix_spawn_file_actions_adddup2(actions, 0, 1) != 0
                || posix_spawn_file_actions_adddup2(actions, 0, 2) != 0)
            {
                error = $"cannot open '{slave}' in the child";
                return -1;
            }

            var result = posix_spawnp(out var pid, argv[0], actions, attributes, argvBlock, envBlock);
            if (result != 0)
            {
                error = $"{argv[0]}: {Marshal.GetPInvokeErrorMessage(result)}";
                return -1;
            }

            error = null;
            return pid;
        }
        finally
        {
            _ = posix_spawn_file_actions_destroy(actions);
            _ = posix_spawnattr_destroy(attributes);
            Marshal.FreeHGlobal(attributes);
            Marshal.FreeHGlobal(actions);
            Marshal.FreeHGlobal(mask);
            Marshal.FreeHGlobal(defaults);
            Free(argvBlock);
            Free(envBlock);
        }
    }

    public static int Read(int fd, Span<byte> buffer, out int errno)
    {
        while (true)
        {
            nint count;
            fixed (byte* data = buffer)
            {
                count = read(fd, data, (nuint)buffer.Length);
            }

            if (count >= 0)
            {
                errno = 0;
                return (int)count;
            }

            errno = Marshal.GetLastPInvokeError();
            if (errno != EIntr)
            {
                return -1;
            }
        }
    }

    public static int Write(int fd, ReadOnlySpan<byte> data, out int errno)
    {
        while (true)
        {
            nint count;
            fixed (byte* bytes = data)
            {
                count = write(fd, bytes, (nuint)data.Length);
            }

            if (count >= 0)
            {
                errno = 0;
                return (int)count;
            }

            errno = Marshal.GetLastPInvokeError();
            if (errno != EIntr)
            {
                return -1;
            }
        }
    }

    public static int PidfdOpenOf(int pid) => (int)syscall(PidfdOpen, pid, 0);

    public static bool TryReap(int pid, out int status)
    {
        var raw = 0;
        var reaped = waitpid(pid, &raw, WaitNoHang);
        status = raw;
        return reaped == pid;
    }

    public static bool Kill(int pid, int signal) => kill(pid, signal) == 0;

    public static int Close(int fd) => close(fd);

    private static nint[] Block(IReadOnlyList<string> entries)
    {
        var block = new nint[entries.Count + 1];
        for (var i = 0; i < entries.Count; i++)
        {
            block[i] = Marshal.StringToCoTaskMemUTF8(entries[i]);
        }

        return block;
    }

    private static void Free(nint[] block)
    {
        foreach (var entry in block)
        {
            if (entry != 0)
            {
                Marshal.FreeCoTaskMem(entry);
            }
        }
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int posix_openpt(int flags);

    [DllImport("libc", SetLastError = true)]
    private static extern int grantpt(int fd);

    [DllImport("libc", SetLastError = true)]
    private static extern int unlockpt(int fd);

    [DllImport("libc")]
    private static extern int ptsname_r(int fd, byte* buffer, nuint length);

    [DllImport("libc", SetLastError = true)]
    private static extern int open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);

    [DllImport("libc", SetLastError = true)]
    private static extern int close(int fd);

    [DllImport("libc", SetLastError = true)]
    private static extern nint read(int fd, byte* buffer, nuint count);

    [DllImport("libc", SetLastError = true)]
    private static extern nint write(int fd, byte* buffer, nuint count);

    [DllImport("libc", SetLastError = true)]
    private static extern int ioctl(int fd, nuint request, void* argument);

    [DllImport("libc", SetLastError = true)]
    private static extern int tcgetattr(int fd, byte* termios);

    [DllImport("libc", SetLastError = true)]
    private static extern int tcsetattr(int fd, int actions, byte* termios);

    [DllImport("libc")]
    private static extern int posix_spawnp(
        out int pid, [MarshalAs(UnmanagedType.LPUTF8Str)] string file, nint actions, nint attributes, nint[] argv, nint[] envp);

    [DllImport("libc")]
    private static extern int posix_spawnattr_init(nint attributes);

    [DllImport("libc")]
    private static extern int posix_spawnattr_destroy(nint attributes);

    [DllImport("libc")]
    private static extern int posix_spawnattr_setflags(nint attributes, short flags);

    [DllImport("libc")]
    private static extern int posix_spawnattr_setsigmask(nint attributes, nint mask);

    [DllImport("libc")]
    private static extern int posix_spawnattr_setsigdefault(nint attributes, nint mask);

    [DllImport("libc")]
    private static extern int posix_spawn_file_actions_init(nint actions);

    [DllImport("libc")]
    private static extern int posix_spawn_file_actions_destroy(nint actions);

    [DllImport("libc")]
    private static extern int posix_spawn_file_actions_addchdir_np(nint actions, [MarshalAs(UnmanagedType.LPUTF8Str)] string path);

    [DllImport("libc")]
    private static extern int posix_spawn_file_actions_addopen(
        nint actions, int fd, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags, uint mode);

    [DllImport("libc")]
    private static extern int posix_spawn_file_actions_adddup2(nint actions, int fd, int target);

    [DllImport("libc")]
    private static extern int sigemptyset(nint set);

    [DllImport("libc")]
    private static extern int sigfillset(nint set);

    [DllImport("libc")]
    private static extern int sigdelset(nint set, int signal);

    [DllImport("libc", SetLastError = true)]
    private static extern long syscall(long number, long pid, long flags);

    [DllImport("libc", SetLastError = true)]
    private static extern int waitpid(int pid, int* status, int options);

    [DllImport("libc", SetLastError = true)]
    private static extern int kill(int pid, int signal);
}
