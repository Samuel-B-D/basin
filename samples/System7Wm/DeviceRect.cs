using Basin;
using Basin.Scene;

namespace System7Wm;

internal sealed class DeviceRect : IDisposable
{
    private readonly SceneBuffer _node;
    private readonly MemoryBuffer _pixel;

    public DeviceRect(SceneTree parent, uint argb)
    {
        _pixel = new MemoryBuffer(1, 1, DrmFormat.Argb8888);
        Fill(argb);
        _node = new SceneBuffer(parent) { InputEnabled = false, IsOpaque = (argb >> 24) == 0xff };
        _node.SetBuffer(_pixel);
        _node.SourceBox = new FBox(0, 0, 1, 1);
    }

    public SceneBuffer Node => _node;

    public bool Enabled
    {
        get => _node.Enabled;
        set => _node.Enabled = value;
    }

    public void Place(int originX, int originY, double scale, in Box device)
    {
        if (device.Width <= 0 || device.Height <= 0)
        {
            _node.Enabled = false;
            return;
        }

        var (x, nudgeX) = DevicePlacement.FromDevice(device.X, scale);
        var (y, nudgeY) = DevicePlacement.FromDevice(device.Y, scale);
        _node.SetPosition(originX + x, originY + y);
        _node.DestinationWidth = DevicePlacement.ToLogicalSpan(device.Width + nudgeX, scale) + 1;
        _node.DestinationHeight = DevicePlacement.ToLogicalSpan(device.Height + nudgeY, scale) + 1;
        _node.DeviceNudge = (nudgeX, nudgeY);
        _node.DevicePixels = (device.Width, device.Height);
        _node.DeviceScale = scale;
    }

    public unsafe void Fill(uint argb)
    {
        if (_pixel.BeginDataAccess(BufferDataAccess.Write, out var view))
        {
            *(uint*)view.Data = argb;
            _pixel.EndDataAccess();
        }

        _node?.NotifyContentChanged();
    }

    public void Dispose()
    {
        if (!_node.IsDestroyed)
        {
            _node.SetBuffer(null);
            _node.Destroy();
        }

        _pixel.Destroy();
    }
}
