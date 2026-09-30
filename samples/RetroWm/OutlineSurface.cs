using Basin.WindowManager;
using Basin.WindowManager.Skia;
using Basin.WindowManager.Skia.Protocol;
using SkiaSharp;
using Wayland;

namespace RetroWm;

internal sealed class OutlineSurface : IDisposable
{
    private readonly OverlayRectSurface _overlay;

    internal OutlineSurface(ManagerSurfaceHost host, WlOutput wlOutput, WmOutput output)
    {
        _overlay = new OverlayRectSurface(host, wlOutput, output, ZwlrLayerShellV1.Layer.Overlay, "retro-wm-outline");
        _overlay.Paint += Draw;
    }

    public WmOutput Output => _overlay.Output;

    public void Show(Rect rect, int scale) => _overlay.Show(rect, scale);

    public void Dispose() => _overlay.Dispose();

    private static void Draw(SKCanvas canvas, int width, int height, int scale)
    {
        var w = width * scale;
        var h = height * scale;
        var thickness = 2 * scale;
        var dash = 8 * scale;
        var period = 12 * scale;
        using var paint = new SKPaint();
        paint.IsAntialias = false;
        paint.Color = Theme.Color(Theme.OutlineColor);

        for (var x = 0; x < w; x += period)
        {
            var run = Math.Min(dash, w - x);
            canvas.DrawRect(x, 0, run, thickness, paint);
            canvas.DrawRect(x, h - thickness, run, thickness, paint);
        }

        for (var y = 0; y < h; y += period)
        {
            var run = Math.Min(dash, h - y);
            canvas.DrawRect(0, y, thickness, run, paint);
            canvas.DrawRect(w - thickness, y, thickness, run, paint);
        }
    }
}
