using Basin.Freedesktop;
using SkiaSharp;
using Svg.Skia;
using System7.Avalonia.Rendering;

namespace System7Wm;

internal sealed class IconCache
{
    private readonly Dictionary<(string, bool), System7IconFamily?> _cache = [];
    private readonly IconSearch _apps;
    private readonly IconSearch _names = new() { ReadDesktopEntry = false };

    public IconCache(DesktopEntries entries)
    {
        _apps = new IconSearch { Entries = entries };
    }

    public System7IconFamily Family(string? key, string generic, bool appId = false)
    {
        if (key is { Length: > 0 } && Load(key, appId) is { } family)
        {
            return family;
        }

        return System7IconFamily.Generic(generic) ?? System7IconFamily.Generic("document")!;
    }

    public System7IconFamily? Load(string key, bool appId)
    {
        if (_cache.TryGetValue((key, appId), out var cached))
        {
            return cached;
        }

        System7IconFamily? family = null;
        try
        {
            var path = key.StartsWith('/') ? key : (appId ? _apps : _names).Find(key);
            if (path is not null && File.Exists(path) && Pixels(path, 32) is { } large && Pixels(path, 16) is { } small)
            {
                family = System7IconFamily.FromArgb(large, small);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
        }

        _cache[(key, appId)] = family;
        return family;
    }

    public void Clear()
    {
        _cache.Clear();
        _apps.Invalidate();
        _names.Invalidate();
    }

    private static unsafe uint[]? Pixels(string path, int size)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            if (path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                using var svg = new SKSvg();
                if (svg.Load(path) is not { } picture || picture.CullRect.Width <= 0 || picture.CullRect.Height <= 0)
                {
                    return null;
                }

                var bounds = picture.CullRect;
                var scale = Math.Min(size / bounds.Width, size / bounds.Height);
                canvas.Translate((size - (bounds.Width * scale)) / 2f, (size - (bounds.Height * scale)) / 2f);
                canvas.Scale(scale);
                canvas.Translate(-bounds.Left, -bounds.Top);
                canvas.DrawPicture(picture);
            }
            else
            {
                using var source = SKBitmap.Decode(path);
                if (source is null || source.Width <= 0)
                {
                    return null;
                }

                var fit = Math.Min(size / (float)source.Width, size / (float)source.Height);
                var width = source.Width * fit;
                var height = source.Height * fit;
                using var image = SKImage.FromBitmap(source);
                canvas.DrawImage(image, new SKRect((size - width) / 2f, (size - height) / 2f, (size + width) / 2f, (size + height) / 2f),
                    new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
            }

            canvas.Flush();
        }

        var result = new uint[size * size];
        var pixels = (uint*)bitmap.GetPixels();
        for (var i = 0; i < result.Length; i++)
        {
            var premul = pixels[i];
            var alpha = premul >> 24;
            if (alpha == 0)
            {
                continue;
            }

            uint Channel(int shift) => Math.Min(255u, ((premul >> shift) & 0xff) * 255 / alpha);
            result[i] = (alpha << 24) | (Channel(16) << 16) | (Channel(8) << 8) | Channel(0);
        }

        return result;
    }
}
