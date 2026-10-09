using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Basin.Freedesktop;
using Basin.Icons.Skia;

namespace LunaWm;

internal sealed class LunaIcons : IDisposable
{
    private readonly Dictionary<(string Name, int Size), WriteableBitmap?> _cache = [];
    private readonly IconSearch _search = new()
    {
        OverrideDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "luna-wm", "icons"),
    };

    public IImage? Load(string? name, int sizePx)
    {
        if (name is not { Length: > 0 } || sizePx <= 0)
        {
            return null;
        }

        var key = (name, sizePx);
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        WriteableBitmap? image = null;
        try
        {
            var path = Path.IsPathRooted(name) && File.Exists(name) ? name : _search.Find(name);
            if (path is not null)
            {
                image = new WriteableBitmap(new PixelSize(sizePx, sizePx), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
                bool drawn;
                using (var frame = image.Lock())
                {
                    drawn = IconRasterizer.Draw(path, frame.Address, frame.RowBytes, sizePx);
                }

                if (!drawn)
                {
                    image.Dispose();
                    image = null;
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            image = null;
        }

        _cache[key] = image;
        return image;
    }

    public string? PathOf(string? name)
    {
        if (name is not { Length: > 0 })
        {
            return null;
        }

        try
        {
            return Path.IsPathRooted(name) && File.Exists(name) ? name : _search.Find(name);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public Basin.MemoryBuffer? Raster(string? name, int sizePx, out uint averageArgb)
    {
        averageArgb = 0;
        if (PathOf(name) is not { } path || sizePx <= 0)
        {
            return null;
        }

        var buffer = new Basin.MemoryBuffer(sizePx, sizePx, Basin.DrmFormat.Argb8888);
        var drawn = false;
        if (buffer.BeginDataAccess(Basin.BufferDataAccess.Write, out var view))
        {
            try
            {
                drawn = IconRasterizer.Draw(path, view.Data, view.Stride, sizePx);
                if (drawn)
                {
                    averageArgb = Average(view.Data, view.Stride, sizePx);
                }
            }
            finally
            {
                buffer.EndDataAccess();
            }
        }

        if (!drawn)
        {
            buffer.Destroy();
            return null;
        }

        return buffer;
    }

    private static unsafe uint Average(nint data, int stride, int size)
    {
        long r = 0, g = 0, b = 0, weight = 0;
        for (var y = 0; y < size; y++)
        {
            var row = (byte*)data + (y * stride);
            for (var x = 0; x < size; x++)
            {
                var a = row[(x * 4) + 3];
                if (a < 128)
                {
                    continue;
                }

                b += row[(x * 4) + 0] * 255 / a;
                g += row[(x * 4) + 1] * 255 / a;
                r += row[(x * 4) + 2] * 255 / a;
                weight++;
            }
        }

        if (weight == 0)
        {
            return 0;
        }

        return 0xff000000 | ((uint)(r / weight) << 16) | ((uint)(g / weight) << 8) | (uint)(b / weight);
    }

    public void Clear()
    {
        foreach (var image in _cache.Values)
        {
            image?.Dispose();
        }

        _cache.Clear();
    }

    public void Dispose() => Clear();
}
