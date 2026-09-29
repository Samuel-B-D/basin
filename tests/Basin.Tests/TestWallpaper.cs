using Basin.Shell.Xdg.Protocol;
using Wayland;

namespace Basin.Tests;

internal sealed class TestWallpaper : IDisposable
{
    private readonly ShmTestClient _client;
    private readonly WlSurface _surface;
    private readonly uint _argb;

    public TestWallpaper(string socketPath, uint argb)
    {
        _argb = argb;
        _client = new ShmTestClient(WlDisplay.Connect(socketPath));
        _client.BindGlobals(() => _client.Display.Roundtrip());
        var shell = _client.BindAt<ZwlrLayerShellV1>("zwlr_layer_shell_v1", 4);
        _surface = _client.Compositor.CreateSurface();
        var layer = shell.GetLayerSurface(_surface, _client.Outputs[0], ZwlrLayerShellV1.Layer.Background, "wallpaper");
        layer.SetAnchor(
            ZwlrLayerSurfaceV1.Anchor.Top | ZwlrLayerSurfaceV1.Anchor.Bottom |
            ZwlrLayerSurfaceV1.Anchor.Left | ZwlrLayerSurfaceV1.Anchor.Right);
        layer.SetExclusiveZone(-1);
        layer.Configure += (_, e) =>
        {
            layer.AckConfigure(e.Serial);
            Draw((int)e.Width, (int)e.Height);
        };
        _surface.Commit();
        for (var i = 0; i < 20 && Width == 0; i++)
        {
            _client.Display.Roundtrip();
        }
    }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public void Pump()
    {
        _client.Display.Roundtrip();
        _client.Display.Roundtrip();
    }

    public void Dispose() => _client.Dispose();

    private void Draw(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        Width = width;
        Height = height;
        var buffer = _client.CreateBuffer(width, height, Fill.Solid(width, height, _argb));
        _surface.Attach(buffer.Proxy, 0, 0);
        _surface.Damage(0, 0, width, height);
        _surface.Commit();
    }
}
