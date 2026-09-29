namespace Basin.Scene3D;

public sealed class WorldImage
{
    public WorldImage(int width, int height, byte[] rgba)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(width, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(height, 0);
        ArgumentNullException.ThrowIfNull(rgba);
        if (rgba.Length != width * height * 4)
        {
            throw new ArgumentException("RGBA data does not match the size.", nameof(rgba));
        }

        Width = width;
        Height = height;
        Rgba = rgba;
    }

    public int Width { get; }

    public int Height { get; }

    public byte[] Rgba { get; }
}
