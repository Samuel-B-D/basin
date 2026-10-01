namespace Basin.Frames.MsStyles;

public sealed class MsStylesBitmap
{
    internal MsStylesBitmap(int handle, int width, int height, byte[] pixels, bool hasAlpha, bool hasDefaultKey)
    {
        Handle = handle;
        Width = width;
        Height = height;
        Pixels = pixels;
        HasAlpha = hasAlpha;
        HasDefaultKey = hasDefaultKey;
    }

    public int Handle { get; }

    public int Width { get; }

    public int Height { get; }

    public byte[] Pixels { get; }

    public bool HasAlpha { get; }

    public bool HasDefaultKey { get; }

    public uint PixelAt(int x, int y)
    {
        var i = (y * Width + x) * 4;
        return (uint)(Pixels[i] | (Pixels[i + 1] << 8) | (Pixels[i + 2] << 16) | (Pixels[i + 3] << 24));
    }
}
