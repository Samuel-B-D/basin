using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Platform;

namespace Basin.Avalonia;

[SupportedOSPlatform("windows")]
internal static class Win32WindowLevel
{
    private const int GwlExStyle = -20;
    private const int WsExNoActivate = 0x08000000;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;

    private static readonly nint HwndTopmost = -1;
    private static readonly nint HwndBottom = 1;

    internal static void Apply(IPlatformHandle handle, HostStackingBand band, bool takesKeyboard)
    {
        var hwnd = handle.Handle;
        if (hwnd == 0)
        {
            return;
        }

        if (HostStacking.IsTopmost(band))
        {
            if (band == HostStackingBand.Overlay)
            {
                _ = SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoActivate);
            }

            return;
        }

        if (!takesKeyboard)
        {
            var style = GetWindowLongPtrW(hwnd, GwlExStyle);
            _ = SetWindowLongPtrW(hwnd, GwlExStyle, style | WsExNoActivate);
        }

        _ = SetWindowPos(hwnd, HwndBottom, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoActivate);
    }

    // Windows 11 rounds and outlines every top-level window, borderless ones
    // included; a layer surface (panel, menu, full-screen overlay) draws its own
    // edges, so ask DWM for square corners and no border.
    internal static void RemoveFrame(IPlatformHandle handle)
    {
        if (handle.Handle == 0)
        {
            return;
        }

        var corners = DwmCornerDoNotRound;
        _ = DwmSetWindowAttribute(handle.Handle, DwmWindowCornerPreference, ref corners, sizeof(int));
        var border = DwmColorNone;
        _ = DwmSetWindowAttribute(handle.Handle, DwmBorderColor, ref border, sizeof(uint));
    }

    private const int DwmWindowCornerPreference = 33;
    private const int DwmBorderColor = 34;
    private const int DwmCornerDoNotRound = 1;
    private const uint DwmColorNone = 0xFFFFFFFE;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref uint value, int size);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint GetWindowLongPtrW(nint hwnd, int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowLongPtrW(nint hwnd, int index, nint value);
}
