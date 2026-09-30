using System7.Avalonia.Rendering;
using Basin.Desktop;
using Basin.Scene;
using Basin;

namespace System7Wm;

internal sealed class System7Cursors
{
    private static readonly (string Name, Func<System7Cursor> Cursor)[] Names =
    [
        ("default", () => System7Cursor.Arrow),
        ("left_ptr", () => System7Cursor.Arrow),
        ("arrow", () => System7Cursor.Arrow),
        ("text", () => System7Cursor.IBeam),
        ("xterm", () => System7Cursor.IBeam),
        ("crosshair", () => System7Cursor.Crosshair),
        ("cross", () => System7Cursor.Crosshair),
        ("cell", () => System7Cursor.Plus),
        ("plus", () => System7Cursor.Plus),
    ];

    private static readonly string[] WatchNames = ["wait", "watch", "progress", "left_ptr_watch"];

    private readonly CursorController _cursor;
    private readonly Dictionary<(System7CursorFrame, int), XcursorImage> _images = [];
    private int _watchFrame;

    public System7Cursors(CursorController cursor)
    {
        _cursor = cursor;
        foreach (var (name, cursorOf) in Names)
        {
            var frame = cursorOf().Frames[0];
            cursor.Define(name, scale => Image(frame, scale));
        }

        DefineWatch();
    }

    public TimeSpan WatchInterval => System7Cursor.Watch.FrameInterval;

    public void AdvanceWatch()
    {
        _watchFrame = (_watchFrame + 1) % System7Cursor.Watch.Frames.Count;
        DefineWatch();
    }

    private void DefineWatch()
    {
        var frame = System7Cursor.Watch.Frames[_watchFrame];
        foreach (var name in WatchNames)
        {
            _cursor.Define(name, scale => Image(frame, scale));
        }
    }

    private XcursorImage Image(System7CursorFrame frame, double scale)
    {
        var n = DevicePlacement.ChromeScale(scale);
        if (_images.TryGetValue((frame, n), out var cached))
        {
            return cached;
        }

        var size = System7CursorFrame.Size * n;
        var pixels = new byte[size * size * 4];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var argb = frame.Argb(x / n, y / n);
                var offset = ((y * size) + x) * 4;
                pixels[offset] = (byte)argb;
                pixels[offset + 1] = (byte)(argb >> 8);
                pixels[offset + 2] = (byte)(argb >> 16);
                pixels[offset + 3] = (byte)(argb >> 24);
            }
        }

        var image = new XcursorImage
        {
            Width = size,
            Height = size,
            HotspotX = frame.HotSpotX * n,
            HotspotY = frame.HotSpotY * n,
            DelayMs = 0,
            Pixels = pixels,
        };
        _images[(frame, n)] = image;
        return image;
    }
}
