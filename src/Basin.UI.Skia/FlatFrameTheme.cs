using Basin.Render.Skia;
using SkiaSharp;

namespace Basin.UI.Skia;

public sealed class FlatFrameTheme : IDisposable
{
    private const string BundledFont = "Selawik-Semibold.ttf";

    private readonly bool _ownsTypeface;
    private readonly Dictionary<IBuffer, SKImage?> _pixelIcons = [];
    private bool _disposed;

    public FlatFrameTheme(SKTypeface typeface, float fontSize = 13, bool ownsTypeface = false)
    {
        ArgumentNullException.ThrowIfNull(typeface);
        Typeface = typeface;
        _ownsTypeface = ownsTypeface;
        FontSize = fontSize;
        TitleFont = SkiaCensus.Track(new SKFont(typeface, fontSize) { Subpixel = true });
        BadgeFont = SkiaCensus.Track(new SKFont(typeface, MathF.Round(fontSize * 11 / 14)) { Subpixel = true });
        Text = new SkiaShapedTextCache(typeface);
        Badges = new SkiaShapedTextCache(typeface);
        Fill = SkiaCensus.Track(new SKPaint { IsAntialias = true });
        Stroke = SkiaCensus.Track(new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
        });
    }

    public static FlatFrameTheme Default(float fontSize = 13)
    {
        using var stream = typeof(FlatFrameTheme).Assembly.GetManifestResourceStream(BundledFont)
            ?? throw new InvalidOperationException($"Basin.UI.Skia is missing {BundledFont}.");
        using var data = SKData.Create(stream);
        var typeface = SkiaCensus.Track(SKTypeface.FromData(data))
            ?? throw new InvalidOperationException($"{BundledFont} did not load.");
        return new FlatFrameTheme(typeface, fontSize, ownsTypeface: true);
    }

    public float FontSize { get; }

    public SKTypeface Typeface { get; }

    public SKFont TitleFont { get; }

    public SKFont BadgeFont { get; }

    public SkiaShapedTextCache Text { get; }

    public SkiaShapedTextCache Badges { get; }

    public SKPaint Fill { get; }

    public SKPaint Stroke { get; }

    public Func<string, int, SKImage?>? Icons { get; set; }

    public SKImage? IconFor(string name, int scale) => Icons?.Invoke(name, scale);

    public SKImage? ImageFor(IBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (_pixelIcons.TryGetValue(buffer, out var cached))
        {
            return cached;
        }

        SKImage? image = null;
        if (SkiaRenderer.TryImageInfo(buffer.Width, buffer.Height, DrmFormat.Argb8888, out var info) &&
            buffer.BeginDataAccess(BufferDataAccess.Read, out var view))
        {
            var copied = SKImage.FromPixelCopy(info, view.Data, view.Stride);
            buffer.EndDataAccess();
            if (copied is not null)
            {
                image = SkiaCensus.Track(copied);
            }
        }

        _pixelIcons[buffer] = image;
        buffer.Destroyed += () =>
        {
            if (_pixelIcons.Remove(buffer, out var dead) && dead is not null)
            {
                SkiaCensus.Release(dead);
            }
        };
        return image;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var icon in _pixelIcons.Values)
        {
            if (icon is not null)
            {
                SkiaCensus.Release(icon);
            }
        }

        _pixelIcons.Clear();
        Text.Dispose();
        Badges.Dispose();
        SkiaCensus.Release(Fill);
        SkiaCensus.Release(Stroke);
        SkiaCensus.Release(TitleFont);
        SkiaCensus.Release(BadgeFont);
        if (_ownsTypeface)
        {
            SkiaCensus.Release(Typeface);
        }
    }
}
