using Basin.Shell.Xdg;

namespace Basin.Avalonia;

public static class HostStacking
{
    public static HostStackingBand BandFor(LayerKind layer) => layer switch
    {
        LayerKind.Background => HostStackingBand.Background,
        LayerKind.Bottom => HostStackingBand.Below,
        LayerKind.Overlay => HostStackingBand.Overlay,
        _ => HostStackingBand.Above,
    };

    public static bool IsTopmost(HostStackingBand band) =>
        band is HostStackingBand.Above or HostStackingBand.Overlay;

    // Drops the frame the host desktop draws around every window (Windows 11's
    // rounded corners and border), for windows that draw their own edges.
    internal static void RemoveFrame(global::Avalonia.Controls.Window window)
    {
        if (!OperatingSystem.IsWindows() || window.TryGetPlatformHandle() is not { } handle)
        {
            return;
        }

        try
        {
            Win32WindowLevel.RemoveFrame(handle);
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    internal static void Apply(global::Avalonia.Controls.Window window, HostStackingBand band, bool takesKeyboard)
    {
        if (window.TryGetPlatformHandle() is not { } handle)
        {
            return;
        }

        try
        {
            if (OperatingSystem.IsMacOS())
            {
                MacWindowLevel.Apply(handle, band);
            }
            else if (OperatingSystem.IsWindows())
            {
                Win32WindowLevel.Apply(handle, band, takesKeyboard);
            }
            else if (OperatingSystem.IsLinux())
            {
                X11WindowLevel.Apply(handle, band);
            }
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }
}
