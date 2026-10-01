namespace Basin.Themes.MsStyles;

public readonly record struct MsStylesRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;

    public int Height => Bottom - Top;
}
