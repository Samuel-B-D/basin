using Basin.WindowManager;
using Basin.WindowManager.Skia;
using Basin.WindowManager.Skia.Protocol;
using SkiaSharp;
using Wayland;

namespace DeskbarWm;

internal sealed class DropHighlight : IDisposable
{
    private readonly OverlayRectSurface _overlay;

    internal DropHighlight(ManagerSurfaceHost host, WlOutput wlOutput, WmOutput output)
    {
        _overlay = new OverlayRectSurface(host, wlOutput, output, ZwlrLayerShellV1.Layer.Overlay, "deskbar-drop");
        _overlay.Paint += Draw;
    }

    public WmOutput Output => _overlay.Output;

    public void Show(Rect region, int scale) =>
        _overlay.Show(new Rect(region.X - Output.Area.X, region.Y - Output.Area.Y, region.Width, region.Height), scale);

    public void Dispose() => _overlay.Dispose();

    private static void Draw(SKCanvas canvas, int width, int height, int scale)
    {
        canvas.Scale(scale);
        canvas.Clear(SKColors.Transparent);
        using var paint = new SKPaint();
        paint.IsAntialias = false;
        paint.Color = new SKColor(51, 102, 152, 90);
        canvas.DrawRect(0, 0, width, height, paint);
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 2;
        paint.Color = new SKColor(51, 102, 152);
        canvas.DrawRect(1, 1, width - 2, height - 2, paint);
    }
}
