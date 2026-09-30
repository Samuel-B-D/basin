using Basin.WindowManager;
using Basin.WindowManager.Skia;
using Basin.WindowManager.Skia.Protocol;
using SkiaSharp;
using Wayland;

namespace DeskbarWm;

internal sealed class PlacementOutline : IDisposable
{
    private readonly OverlayRectSurface _overlay;

    internal PlacementOutline(ManagerSurfaceHost host, WlOutput wlOutput, WmOutput output)
    {
        _overlay = new OverlayRectSurface(host, wlOutput, output, ZwlrLayerShellV1.Layer.Overlay, "deskbar-outline");
        _overlay.Paint += Draw;
    }

    public WmOutput Output => _overlay.Output;

    public void Show(Rect frame, int scale) =>
        _overlay.Show(new Rect(frame.X - Output.Area.X, frame.Y - Output.Area.Y, frame.Width, frame.Height), scale);

    public void Dispose() => _overlay.Dispose();

    private static void Draw(SKCanvas canvas, int width, int height, int scale)
    {
        canvas.Scale(scale);
        canvas.Clear(SKColors.Transparent);
        using var paint = new SKPaint();
        paint.IsAntialias = false;
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1;
        paint.Color = SKColors.Black;
        using var dash = SKPathEffect.CreateDash([2f, 2f], 0f);
        paint.PathEffect = dash;
        canvas.DrawRect(0, 0, Math.Max(width - 1, 1), Math.Max(height - 1, 1), paint);
        paint.PathEffect = null;
    }
}
