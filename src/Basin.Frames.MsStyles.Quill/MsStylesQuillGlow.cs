using System.Drawing;
using Basin.Themes.MsStyles;
using Prowl.Quill;
using Prowl.Vector;
using Prowl.Vector.Spatial;
using SkiaSharp;

namespace Basin.Frames.MsStyles.Quill;

internal sealed class MsStylesQuillGlow
{
    private const int Step = 64;

    private object? _texture;
    private int _capacityWidth;
    private int _capacityHeight;
    private byte[] _pixels = [];
    private Key _key;
    private Rectangle _area;

    private readonly record struct Key(string? Text, RectangleF Destination, MsStylesFont Font, float Pixels, uint Color, float Size, float Radius, HorizontalAlign Align);

    public void Draw(Canvas canvas, ICanvasRenderer renderer, MsStylesSkiaText raster, in MsStylesDrawOp op, float scale)
    {
        var key = new Key(op.Text, op.Destination, op.Font, op.FontPixels, op.Color, op.Size, op.Radius, op.Align);
        if (_texture is null || key != _key)
        {
            if (!Rasterize(renderer, raster, op))
            {
                return;
            }

            _key = key;
        }

        var area = _area;
        if (area.Width <= 0 || area.Height <= 0)
        {
            return;
        }

        var x = area.X / scale;
        var y = area.Y / scale;
        var width = area.Width / scale;
        var height = area.Height / scale;
        canvas.SetBrushTexture(_texture);
        canvas.SetBrushTextureTransform(Transform2D.CreateTranslation(x, y)
            * Transform2D.CreateScale(_capacityWidth / scale, _capacityHeight / scale));
        canvas.RectFilled(x, y, width, height, new Color32(255, 255, 255, 255));
        canvas.ClearBrushTexture();
    }

    private bool Rasterize(ICanvasRenderer renderer, MsStylesSkiaText raster, in MsStylesDrawOp op)
    {
        var destination = op.Destination;
        var spread = op.Size;
        var left = (int)MathF.Floor(destination.Left - (2 * spread));
        var top = (int)MathF.Floor(destination.Top - destination.Height);
        var width = (int)MathF.Ceiling(destination.Width + (4 * spread));
        var height = (int)MathF.Ceiling(3 * destination.Height);
        if (width <= 0 || height <= 0)
        {
            _area = default;
            return true;
        }

        if (_texture is null || width > _capacityWidth || height > _capacityHeight)
        {
            _capacityWidth = Math.Max(_capacityWidth, (width + Step - 1) / Step * Step);
            _capacityHeight = Math.Max(_capacityHeight, (height + Step - 1) / Step * Step);
            _texture = renderer.CreateTexture((uint)_capacityWidth, (uint)_capacityHeight);
            _pixels = new byte[_capacityWidth * _capacityHeight * 4];
        }

        var info = new SKImageInfo(_capacityWidth, _capacityHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        Array.Clear(_pixels);
        unsafe
        {
            fixed (byte* pixels = _pixels)
            {
                using var surface = SKSurface.Create(info, (nint)pixels, info.RowBytes);
                if (surface is null)
                {
                    return false;
                }

                surface.Canvas.Translate(-left, -top);
                raster.Draw(surface.Canvas, op);
                surface.Canvas.Flush();
            }
        }

        renderer.SetTextureData(_texture!, new IntRect(0, 0, _capacityWidth, _capacityHeight), _pixels);
        _area = new Rectangle(left, top, width, height);
        return true;
    }
}
