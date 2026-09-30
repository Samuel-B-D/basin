using Basin;
using Basin.Scene;

namespace System7Wm;

internal sealed class GrayOutline : IDisposable
{
    private readonly SceneTree _layer;
    private readonly List<SceneBuffer> _lines = [];
    private MemoryBuffer? _horizontal;
    private MemoryBuffer? _vertical;
    private int _chrome;

    public GrayOutline(SceneTree layer)
    {
        _layer = layer;
    }

    public bool Visible { get; private set; }

    public Box Device { get; private set; }

    public void Show(int originX, int originY, double scale, int outputWidth, int outputHeight, in Box device, int titleHeight)
    {
        var chrome = DevicePlacement.ChromeScale(scale);
        Ensure(outputWidth, outputHeight, chrome);
        Device = device;
        Visible = true;
        var rows = new List<Box>
        {
            new(device.X, device.Y, device.Width, chrome),
            new(device.X, device.Bottom - chrome, device.Width, chrome),
            new(device.X, device.Y, chrome, device.Height),
            new(device.Right - chrome, device.Y, chrome, device.Height),
        };
        if (titleHeight > 0 && device.Height > titleHeight)
        {
            rows.Add(new Box(device.X, device.Y + titleHeight - chrome, device.Width, chrome));
        }

        while (_lines.Count < rows.Count)
        {
            _lines.Add(new SceneBuffer(_layer) { InputEnabled = false });
        }

        for (var i = 0; i < _lines.Count; i++)
        {
            var line = _lines[i];
            if (i >= rows.Count)
            {
                line.Enabled = false;
                continue;
            }

            var box = rows[i].Intersect(new Box(0, 0, outputWidth, outputHeight));
            if (box.IsEmpty)
            {
                line.Enabled = false;
                continue;
            }

            var horizontal = box.Width >= box.Height;
            var source = horizontal ? _horizontal! : _vertical!;
            line.SetBuffer(source);
            var phaseX = horizontal ? box.X : box.X % (2 * chrome);
            var phaseY = horizontal ? box.Y % (2 * chrome) : box.Y;
            line.SourceBox = new FBox(phaseX, phaseY, box.Width, box.Height);
            var (x, nudgeX) = DevicePlacement.FromDevice(box.X, scale);
            var (y, nudgeY) = DevicePlacement.FromDevice(box.Y, scale);
            line.SetPosition(originX + x, originY + y);
            line.DestinationWidth = DevicePlacement.ToLogicalSpan(box.Width + nudgeX, scale) + 1;
            line.DestinationHeight = DevicePlacement.ToLogicalSpan(box.Height + nudgeY, scale) + 1;
            line.DeviceNudge = (nudgeX, nudgeY);
            line.DevicePixels = (box.Width, box.Height);
            line.DeviceScale = scale;
            line.Enabled = true;
            line.NotifyContentChanged();
        }
    }

    public void Hide()
    {
        Visible = false;
        foreach (var line in _lines)
        {
            line.Enabled = false;
        }
    }

    public void Dispose()
    {
        foreach (var line in _lines)
        {
            line.SetBuffer(null);
            line.Destroy();
        }

        _lines.Clear();
        _horizontal?.Destroy();
        _vertical?.Destroy();
    }

    private void Ensure(int width, int height, int chrome)
    {
        if (_horizontal is { } h && h.Width == width + (2 * chrome) && _chrome == chrome && _vertical!.Height == height + (2 * chrome))
        {
            return;
        }

        foreach (var line in _lines)
        {
            line.SetBuffer(null);
        }

        _horizontal?.Destroy();
        _vertical?.Destroy();
        _chrome = chrome;
        _horizontal = Checker(width + (2 * chrome), 3 * chrome, chrome);
        _vertical = Checker(3 * chrome, height + (2 * chrome), chrome);
    }

    private static unsafe MemoryBuffer Checker(int width, int height, int chrome)
    {
        var buffer = new MemoryBuffer(width, height, DrmFormat.Argb8888);
        if (buffer.BeginDataAccess(BufferDataAccess.Write, out var view))
        {
            for (var y = 0; y < height; y++)
            {
                var row = (uint*)((byte*)view.Data + (y * view.Stride));
                for (var x = 0; x < width; x++)
                {
                    row[x] = (((x / chrome) + (y / chrome)) & 1) == 0 ? 0xff000000u : 0xffffffffu;
                }
            }

            buffer.EndDataAccess();
        }

        return buffer;
    }
}
