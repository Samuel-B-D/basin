using System.Runtime.InteropServices;
using Basin.Backend.Wayland.Protocol;
using Wayland;

namespace Basin.Backend.Wayland;

public sealed class WaylandKeyboardDevice
{
    [DllImport("libc", SetLastError = true)]
    private static extern unsafe void* mmap(void* addr, nuint length, int prot, int flags, int fd, long offset);

    [DllImport("libc", SetLastError = true)]
    private static extern unsafe int munmap(void* addr, nuint length);

    [DllImport("libc")]
    private static extern int close(int fd);

    private readonly List<uint> _held = [];

    internal WaylandKeyboardDevice(WaylandBackend backend, WlKeyboard keyboard)
    {
        keyboard.Keymap += (_, e) =>
        {
            var bytes = ReadKeymap(e.Fd, e.Size);
            backend.ParentDisplay?.CloseFd(e.Fd);
            if (bytes is not null)
            {
                Keymap?.Invoke(bytes);
            }
        };
        keyboard.Enter += (_, e) =>
        {
            backend.LastKeyboardSerial = e.Serial;
            var output = backend.FindOutput(e.Surface);
            if (output is not null)
            {
                Enter?.Invoke(output);
            }

            var time = (uint)Environment.TickCount64;
            for (var i = 0; i + 4 <= e.Keys.Length; i += 4)
            {
                var key = BitConverter.ToUInt32(e.Keys, i);
                if (!_held.Contains(key))
                {
                    _held.Add(key);
                    Key?.Invoke(time, key, true);
                }
            }
        };
        keyboard.Leave += (_, _) =>
        {
            var time = (uint)Environment.TickCount64;
            for (var i = _held.Count - 1; i >= 0; i--)
            {
                var key = _held[i];
                _held.RemoveAt(i);
                Key?.Invoke(time, key, false);
            }

            Leave?.Invoke();
        };
        keyboard.Key += (_, e) =>
        {
            backend.LastKeyboardSerial = e.Serial;
            var pressed = e.State == WlKeyboard.KeyState.Pressed;
            if (pressed)
            {
                if (!_held.Contains(e.Key))
                {
                    _held.Add(e.Key);
                }
            }
            else
            {
                _held.Remove(e.Key);
            }

            Key?.Invoke(e.Time, e.Key, pressed);
        };
        keyboard.Modifiers += (_, e) => Modifiers?.Invoke(e.ModsDepressed, e.ModsLatched, e.ModsLocked, e.Group);
        keyboard.RepeatInfo += (_, e) => RepeatInfo?.Invoke(e.Rate, e.Delay);
    }

    public event Action<int, int>? RepeatInfo;

    public event Action<byte[]>? Keymap;

    public event Action<WaylandOutput>? Enter;

    public event Action? Leave;

    public event Action<uint, uint, bool>? Key;

    public event Action<uint, uint, uint, uint>? Modifiers;

    private static unsafe byte[]? ReadKeymap(int fd, uint size)
    {
        var map = mmap(null, size, 1 , 2 , fd, 0);
        if ((nint)map == -1)
        {
            return null;
        }

        var span = new ReadOnlySpan<byte>(map, (int)size);
        var length = span.IndexOf((byte)0);
        var bytes = span[..(length < 0 ? (int)size : length)].ToArray();
        munmap(map, size);
        return bytes;
    }
}
