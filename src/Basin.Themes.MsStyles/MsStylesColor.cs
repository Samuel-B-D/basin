namespace Basin.Themes.MsStyles;

public readonly record struct MsStylesColor(byte R, byte G, byte B)
{
    public static MsStylesColor FromColorRef(int colorRef) =>
        new((byte)(colorRef & 0xff), (byte)((colorRef >> 8) & 0xff), (byte)((colorRef >> 16) & 0xff));

    public int ToColorRef() => R | (G << 8) | (B << 16);

    public uint ToArgb(byte alpha = 255) => ((uint)alpha << 24) | ((uint)R << 16) | ((uint)G << 8) | B;
}
