using Basin.Icons.Skia;
using Basin.Render.Skia;
using SkiaSharp;

namespace Basin.WindowManager.Skia;

public sealed class IconRaster : IDisposable
{
    private readonly Func<string, string?> _resolve;
    private readonly Dictionary<(string Key, int SizePx), SKImage?> _cache = [];

    public IconRaster(Func<string, string?> resolve)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        _resolve = resolve;
    }

    public SKImage? Load(string key, int sizePx)
    {
        var cacheKey = (key, sizePx);
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        SKImage? image = null;
        try
        {
            image = _resolve(key) is { } path ? IconRasterizer.Image(path, sizePx) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
        }

        _cache[cacheKey] = image;
        return image;
    }

    public void Dispose()
    {
        foreach (var image in _cache.Values)
        {
            SkiaCensus.Release(image);
        }

        _cache.Clear();
    }
}
