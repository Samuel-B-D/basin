using Basin.WindowManager.Skia.Protocol;
using SkiaSharp;
using Wayland;

namespace Basin.WindowManager.Skia;

public sealed class OverlayRectSurface : IDisposable
{
    private readonly ManagerSurface _surface;
    private Rect _rect;
    private int _lastScale = -1;
    private bool _started;
    private bool _drawn;

    public OverlayRectSurface(
        ManagerSurfaceHost host,
        WlOutput wlOutput,
        WmOutput output,
        ZwlrLayerShellV1.Layer layer,
        string scope)
    {
        ArgumentNullException.ThrowIfNull(host);
        Output = output;
        _surface = host.Create(wlOutput, layer, scope);
        _surface.SetAnchor(ZwlrLayerSurfaceV1.Anchor.Top | ZwlrLayerSurfaceV1.Anchor.Left);
        _surface.Configured += host.Manager.RequestManage;
    }

    public WmOutput Output { get; }

    public event Action<SKCanvas, int, int, int>? Paint;

    public void Invalidate() => _drawn = false;

    public void Show(Rect rect, int scale)
    {
        scale = Math.Max(scale, 1);
        if (rect.IsEmpty)
        {
            return;
        }

        if (!_started)
        {
            _started = true;
            _rect = rect;
            _surface.SetSize(rect.Width, rect.Height);
            _surface.SetMargin(rect.Y, rect.X);
            _surface.CommitInitial();
            return;
        }

        if (!_surface.IsConfigured)
        {
            _rect = rect;
            return;
        }

        var redraw = !_drawn || rect.Width != _rect.Width || rect.Height != _rect.Height || scale != _lastScale;
        _rect = rect;
        if (redraw)
        {
            _surface.SetSize(rect.Width, rect.Height);
            var pixels = _surface.Prepare(rect.Width, rect.Height, scale);
            if (pixels == 0)
            {
                return;
            }

            using var surface = _surface.CreateCanvas(pixels);
            if (surface is null)
            {
                return;
            }

            Paint?.Invoke(surface.Canvas, rect.Width, rect.Height, scale);
            surface.Canvas.Flush();
            _surface.SetInputRegion(Rect.Empty);
            _drawn = true;
            _lastScale = scale;
        }

        _surface.SetMargin(rect.Y, rect.X);
        _surface.Commit();
    }

    public void Dispose() => _surface.Dispose();
}
