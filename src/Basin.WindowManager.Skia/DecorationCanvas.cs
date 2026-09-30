using SkiaSharp;

namespace Basin.WindowManager.Skia;

public static class DecorationCanvas
{
    public static SKSurface? Create(DecorationSurface surface, nint pixels)
    {
        ArgumentNullException.ThrowIfNull(surface);
        var info = new SKImageInfo(surface.BufferWidth, surface.BufferHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
        return SKSurface.Create(info, pixels, surface.BufferWidth * 4);
    }
}
