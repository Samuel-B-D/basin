using Basin.WindowManager;
using Basin.WindowManager.Skia;
using Basin.WindowManager.Skia.Protocol;
using Wayland;

namespace RetroWm;

internal sealed class PreviewSurface : IDisposable
{
    private const byte FillAlpha = 90;

    private readonly OverlayRectSurface _overlay;
    private uint _color;

    internal PreviewSurface(ManagerSurfaceHost host, WlOutput wlOutput, WmOutput output)
    {
        _overlay = new OverlayRectSurface(host, wlOutput, output, ZwlrLayerShellV1.Layer.Top, "retro-wm-drop-preview");
        _overlay.Paint += (canvas, _, _, _) => canvas.Clear(Theme.Color(_color).WithAlpha(FillAlpha));
    }

    public WmOutput Output => _overlay.Output;

    public void Show(Rect rect, int scale)
    {
        if (Theme.DropPreview != _color)
        {
            _color = Theme.DropPreview;
            _overlay.Invalidate();
        }

        _overlay.Show(rect, scale);
    }

    public void Dispose() => _overlay.Dispose();
}
