using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Basin.Icons.Skia;

namespace Basin.Portal.Prompts.Avalonia;

public static class PromptIcon
{
    public static Bitmap? Load(string path, int sizePx)
    {
        if (string.IsNullOrEmpty(path) || sizePx <= 0)
        {
            return null;
        }

        var bitmap = new WriteableBitmap(new PixelSize(sizePx, sizePx), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        bool drawn;
        using (var frame = bitmap.Lock())
        {
            drawn = IconRasterizer.Draw(path, frame.Address, frame.RowBytes, sizePx);
        }

        if (!drawn)
        {
            bitmap.Dispose();
            return null;
        }

        return bitmap;
    }
}
