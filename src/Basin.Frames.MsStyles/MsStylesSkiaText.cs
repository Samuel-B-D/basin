using Basin.Render.Skia;
using Basin.UI.Skia;
using SkiaSharp;

namespace Basin.Frames.MsStyles;

public sealed class MsStylesSkiaText : IMsStylesTextMetrics, IDisposable
{
    private const int MaxFaces = 32;

    private readonly Dictionary<(SKTypeface Typeface, float Pixels), (SKFont Font, SkiaShapedTextCache Cache)> _faces = [];
    private readonly SKPaint _paint;
    private SKMaskFilter? _glow;
    private float _glowSigma;
    private bool _disposed;

    public MsStylesSkiaText(MsStylesFonts fonts)
    {
        ArgumentNullException.ThrowIfNull(fonts);
        Fonts = fonts;
        _paint = SkiaCensus.Track(new SKPaint { IsAntialias = true });
    }

    public MsStylesFonts Fonts { get; }

    public void Draw(SKCanvas canvas, in MsStylesDrawOp op)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (op.Kind is not (MsStylesDrawOpKind.Text or MsStylesDrawOpKind.Glow) || op.Text is not { Length: > 0 } text)
        {
            return;
        }

        var (font, cache) = Face(op);
        var destination = op.Destination;
        if (!cache.TryGetBlob(text, font, destination.Width, out var blob, out var width))
        {
            return;
        }

        var metrics = font.Metrics;
        var ascent = -metrics.Ascent;
        var descent = metrics.Descent;
        var baseline = MathF.Round(destination.Top + (destination.Height - (ascent + descent)) / 2 + ascent);
        var x = op.Align switch
        {
            Basin.Themes.MsStyles.HorizontalAlign.Center => destination.Left + MathF.Floor((destination.Width - width) / 2),
            Basin.Themes.MsStyles.HorizontalAlign.Right => destination.Right - width,
            _ => destination.Left,
        };
        x = MathF.Round(x);
        canvas.Save();
        var spread = op.Kind == MsStylesDrawOpKind.Glow ? op.Size : 0;
        canvas.ClipRect(new SKRect(destination.Left - spread, destination.Top - destination.Height, destination.Right + Math.Max(0, op.ShadowOffset.X) + spread, destination.Bottom + destination.Height));
        if (op.Kind == MsStylesDrawOpKind.Glow)
        {
            var sigma = MathF.Max(0.5f, op.Size / 3f);
            if (_glow is null || Math.Abs(_glowSigma - sigma) > 0.001f)
            {
                SkiaCensus.Release(_glow);
                _glow = SkiaCensus.Track(SKMaskFilter.CreateBlur(SKBlurStyle.Normal, sigma));
                _glowSigma = sigma;
            }

            _paint.MaskFilter = _glow;
            _paint.Color = new SKColor(op.Color).WithAlpha((byte)Math.Clamp(MathF.Round(255 * MathF.Min(1f, op.Radius)), 0, 255));
            canvas.DrawText(blob, x, baseline, _paint);
            _paint.MaskFilter = null;
            canvas.Restore();
            return;
        }

        if ((op.ShadowColor >> 24) != 0)
        {
            _paint.Color = new SKColor(op.ShadowColor);
            canvas.DrawText(blob, x + op.ShadowOffset.X, baseline + op.ShadowOffset.Y, _paint);
        }

        _paint.Color = new SKColor(op.Color);
        canvas.DrawText(blob, x, baseline, _paint);
        canvas.Restore();
    }

    public float Measure(in MsStylesDrawOp op)
    {
        if (op.Text is not { Length: > 0 } text)
        {
            return 0;
        }

        var (font, cache) = Face(op);
        return cache.TryGetBlob(text, font, out _, out var width) ? width : 0;
    }

    public float Measure(string text, in Basin.Themes.MsStyles.MsStylesFont font, float pixels)
    {
        var op = new MsStylesDrawOp { Text = text, Font = font, FontPixels = pixels };
        return Measure(op);
    }

    private (SKFont Font, SkiaShapedTextCache Cache) Face(in MsStylesDrawOp op)
    {
        var typeface = Fonts.Resolve(op.Font);
        var key = (typeface, op.FontPixels);
        if (_faces.TryGetValue(key, out var face))
        {
            return face;
        }

        if (_faces.Count >= MaxFaces)
        {
            Clear();
        }

        var font = SkiaCensus.Track(new SKFont(typeface, op.FontPixels) { Subpixel = true, Edging = SKFontEdging.Antialias });
        if (op.Font.Weight >= Basin.Themes.MsStyles.MsStylesFont.Bold && typeface.FontWeight < (int)SKFontStyleWeight.SemiBold)
        {
            font.Embolden = true;
        }

        face = (font, new SkiaShapedTextCache(typeface));
        _faces[key] = face;
        return face;
    }

    private void Clear()
    {
        foreach (var (font, cache) in _faces.Values)
        {
            cache.Dispose();
            SkiaCensus.Release(font);
        }

        _faces.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Clear();
        SkiaCensus.Release(_glow);
        SkiaCensus.Release(_paint);
    }
}
