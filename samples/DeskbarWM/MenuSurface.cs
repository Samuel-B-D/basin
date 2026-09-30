using Basin.WindowManager;
using Basin.WindowManager.Skia;
using Basin.WindowManager.Skia.Protocol;
using SkiaSharp;
using Wayland;

namespace DeskbarWm;

internal sealed class MenuSurface : IDisposable
{
    private const int PaddingX = 10;
    private const int PaddingY = 3;
    private const int SeparatorHeight = 7;
    private const int CheckWidth = 14;
    private const int ArrowWidth = 12;

    private readonly ManagerSurface _surface;
    private readonly WmMenuModel _model;

    internal MenuSurface(
        ManagerSurfaceHost host,
        WlOutput wlOutput,
        WmOutput output,
        RiverWindowManager wm,
        IReadOnlyList<MenuItemEntry> items,
        Point origin,
        bool alignRight = false,
        bool alignBottom = false)
    {
        Output = output;
        Items = items;
        OpensLeft = alignRight;
        using (var font = new SKFont(Fonts.Sans, Theme.FontSize))
        {
            SurfaceSize = Measure(font);
        }

        _model = new WmMenuModel(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            _model.SetRow(i, items[i].Separator ? SeparatorHeight : ItemHeight, !items[i].Separator);
        }

        _model.Content = new Rect(1, 1, SurfaceSize.Width - 2, _model.RowsHeight());

        if (alignRight)
        {
            origin = origin with { X = origin.X - SurfaceSize.Width };
        }

        if (alignBottom)
        {
            origin = origin with { Y = origin.Y - SurfaceSize.Height };
        }

        var area = output.Area;
        Origin = WmMenuModel.ClampInto(origin, SurfaceSize, new Size(area.Width, area.Height));

        _surface = host.Create(wlOutput, ZwlrLayerShellV1.Layer.Overlay, "deskbar-menu");
        _surface.SetExclusiveZone(-1);
        _surface.SetAnchor(ZwlrLayerSurfaceV1.Anchor.Top | ZwlrLayerSurfaceV1.Anchor.Left);
        _surface.SetSize(SurfaceSize.Width, SurfaceSize.Height);
        _surface.SetMargin(Origin.Y, Origin.X);
        _surface.Configured += wm.RequestManage;
        _surface.CommitInitial();
    }

    public WmOutput Output { get; }

    public IReadOnlyList<MenuItemEntry> Items { get; }

    public Point Origin { get; }

    public bool OpensLeft { get; }

    public Size SurfaceSize { get; }

    public uint SurfaceId => _surface.SurfaceId;

    public int Hovered => _model.Hovered;

    private static int ItemHeight => (int)MathF.Ceiling(Theme.FontSize) + (PaddingY * 2) + 4;

    public int? ItemAt(int x, int y) => _model.SelectableAt(x, y);

    public Rect ItemRect(int index)
    {
        if (index < 0 || index >= Items.Count)
        {
            return Rect.Empty;
        }

        var row = _model.RowRect(index);
        return new Rect(0, row.Y, SurfaceSize.Width, row.Height);
    }

    public bool UpdateHover(int x, int y) => _model.Hover(x, y);

    public bool Render(int scale)
    {
        if (!_surface.IsConfigured || _surface.ConfiguredSize.IsEmpty)
        {
            return false;
        }

        if (!_model.NeedsRender(scale))
        {
            return false;
        }

        var size = _surface.ConfiguredSize;
        var pixels = _surface.Prepare(size.Width, size.Height, scale);
        if (pixels == 0)
        {
            return false;
        }

        using var canvas = _surface.CreateCanvas(pixels);
        if (canvas is null)
        {
            return false;
        }

        canvas.Canvas.Scale(scale);
        Draw(canvas.Canvas, size);
        canvas.Canvas.Flush();
        _surface.SetInputRegion(new Rect(0, 0, size.Width, size.Height));
        _model.Rendered(scale);
        return true;
    }

    public void Commit() => _surface.Commit();

    public void Dispose() => _surface.Dispose();

    private Size Measure(SKFont font)
    {
        var width = 60f;
        var height = 2;
        foreach (var item in Items)
        {
            if (item.Separator)
            {
                height += SeparatorHeight;
                continue;
            }

            var itemWidth = font.MeasureText(item.Label) + (PaddingX * 2) + CheckWidth;
            if (item.Children is not null)
            {
                itemWidth += ArrowWidth;
            }

            width = MathF.Max(width, itemWidth);
            height += ItemHeight;
        }

        return new Size((int)MathF.Ceiling(width), height);
    }

    private void Draw(SKCanvas canvas, Size size)
    {
        var panel = new SKColor(216, 216, 216);
        using var paint = new SKPaint();
        paint.IsAntialias = false;
        canvas.Clear(SKColors.Transparent);
        paint.Color = panel;
        canvas.DrawRect(0, 0, size.Width, size.Height, paint);
        paint.Color = Theme.Tint(panel, Theme.Darken2);
        canvas.DrawRect(0, 0, size.Width, 1, paint);
        canvas.DrawRect(0, size.Height - 1, size.Width, 1, paint);
        canvas.DrawRect(0, 0, 1, size.Height, paint);
        canvas.DrawRect(size.Width - 1, 0, 1, size.Height, paint);

        using var font = new SKFont(Fonts.Sans, Theme.FontSize);
        var metrics = font.Metrics;
        var y = 1;
        for (var i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            if (item.Separator)
            {
                paint.Color = Theme.Tint(panel, Theme.Darken2);
                canvas.DrawRect(4, y + (SeparatorHeight / 2), size.Width - 8, 1, paint);
                paint.Color = Theme.Tint(panel, Theme.Lighten2);
                canvas.DrawRect(4, y + (SeparatorHeight / 2) + 1, size.Width - 8, 1, paint);
                y += SeparatorHeight;
                continue;
            }

            if (i == Hovered && item.Enabled)
            {
                paint.Color = Theme.Tint(panel, Theme.DarkenHalf);
                canvas.DrawRect(1, y, size.Width - 2, ItemHeight, paint);
            }

            paint.IsAntialias = true;
            paint.Color = item.Enabled ? SKColors.Black : new SKColor(150, 150, 150);
            var baseline = y + ((ItemHeight - metrics.Ascent - metrics.Descent) / 2f);
            var centerY = y + (ItemHeight / 2f);
            if (item.Checked)
            {
                paint.StrokeWidth = 1.6f;
                paint.Style = SKPaintStyle.Stroke;
                canvas.DrawLine(PaddingX - 4, centerY, PaddingX - 1, centerY + 3, paint);
                canvas.DrawLine(PaddingX - 1, centerY + 3, PaddingX + 5, centerY - 4, paint);
                paint.Style = SKPaintStyle.Fill;
            }

            canvas.DrawText(item.Label, PaddingX + CheckWidth - 4, baseline, SKTextAlign.Left, font, paint);
            if (item.Children is not null)
            {
                var builder = new SKPathBuilder();
                var ax = size.Width - ArrowWidth + 2;
                builder.MoveTo(ax, centerY - 4);
                builder.LineTo(ax + 5, centerY);
                builder.LineTo(ax, centerY + 4);
                builder.Close();
                using var arrow = builder.Detach();
                canvas.DrawPath(arrow, paint);
            }

            paint.IsAntialias = false;
            y += ItemHeight;
        }
    }
}
