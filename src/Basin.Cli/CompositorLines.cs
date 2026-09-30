using Basin.Diagnostics;
using Basin.Scene;

namespace Basin.Cli;

public static class CompositorLines
{
    public static string Socket(string? name) =>
        string.IsNullOrEmpty(name) ? "SOCKET (inherited)" : $"SOCKET {name}";

    public static string Frames(long rendered) =>
        $"FRAMES {rendered} LIVE {(BasinCounters.Enabled ? BasinCounters.LiveObjects.ToString() : "untracked")}";

    public static string? Census() =>
        BasinCounters.Enabled && (BasinCounters.LiveObjects != 0 || BasinCounters.PendingFrees != 0)
            ? BasinCounters.CensusReport()
            : null;

    public static string Luts(int attached) => $"COLOR luts={attached}";

    public static string Scanout(string output, ScanoutChoice choice, int modifiers) => choice switch
    {
        ScanoutChoice.DeviceBuffers => $"SCANOUT {output} device modifiers={modifiers}",
        ScanoutChoice.DumbLinear => $"SCANOUT {output} dumb linear; every frame reads the framebuffer back",
        _ => $"SCANOUT {output} device buffers refused by the plane; falling back to dumb linear",
    };

    public static string ShotRaw(ScreenshotOutcome outcome, IBuffer? buffer, string path) => outcome switch
    {
        ScreenshotOutcome.NoFrame => "SHOTRAW none",
        ScreenshotOutcome.Unreadable => $"SHOTRAW unreadable {buffer!.Width}x{buffer.Height}",
        _ => $"SHOTRAW {path} {buffer!.Width}x{buffer.Height}",
    };
}
