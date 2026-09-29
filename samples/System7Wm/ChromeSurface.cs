using Avalonia;
using Avalonia.Controls;
using Basin;
using Basin.Scene;
using Basin.UI.Avalonia;
using System7.Avalonia.Rendering;

namespace System7Wm;

internal sealed class ChromeSurface : IDisposable
{
    private readonly UISurfaceNode _node;
    private readonly Func<Control> _content;
    private Control? _root;
    private bool _disposed;

    public ChromeSurface(SceneTree parent, AvaloniaUIHost host, UISurfaceIndex index, Func<Control> content)
    {
        _node = new UISurfaceNode(parent, host, index) { PreciseDamage = false };
        _content = content;
    }

    public UISurfaceNode Node => _node;

    public AvaloniaUISurface? Surface => _node.Surface as AvaloniaUISurface;

    public Control? Root => _root;

    public int ChromeScale { get; private set; } = 1;

    public Box Device { get; private set; }

    public int DipWidth { get; private set; }

    public int DipHeight { get; private set; }

    public bool Enabled
    {
        get => _node.Enabled;
        set => _node.Enabled = value;
    }

    public bool Place(int originX, int originY, double scale, in Box device, int? dipWidth = null, int? dipHeight = null)
    {
        if (_disposed || device.Width <= 0 || device.Height <= 0)
        {
            return false;
        }

        var chrome = DevicePlacement.Chrome(scale);
        var (x, nudgeX) = DevicePlacement.FromDevice(device.X, scale);
        var (y, nudgeY) = DevicePlacement.FromDevice(device.Y, scale);
        var width = dipWidth ?? (int)Math.Ceiling(device.Width / (double)chrome);
        var height = dipHeight ?? (int)Math.Ceiling(device.Height / (double)chrome);
        _node.SetPosition(originX + x, originY + y);
        _node.OutputScale = scale;
        _node.InputScale = scale / chrome;
        _node.DeviceNudge = (nudgeX, nudgeY);
        _node.DevicePixels = (device.Width, device.Height);
        if (!_node.Configure(width, height, chrome))
        {
            return false;
        }

        ChromeScale = chrome;
        Device = device;
        DipWidth = width;
        DipHeight = height;
        if (Surface is { } surface)
        {
            surface.SetPosition(device.X / (double)chrome, device.Y / (double)chrome);
            if (_root is null)
            {
                _root = _content();
                surface.Content = _root;
            }

            System7Pattern.SetScreenOffset(_root, new PixelPoint(device.X / chrome, device.Y / chrome));
        }

        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _node.Dispose();
    }
}
