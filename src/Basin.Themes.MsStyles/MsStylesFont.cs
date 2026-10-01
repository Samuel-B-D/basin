namespace Basin.Themes.MsStyles;

public readonly record struct MsStylesFont(string Face, int Points, int Weight, bool Italic, bool Underline, bool StrikeOut)
{
    public const int Regular = 400;

    public const int Bold = 700;

    public double PixelSize(double dpi) => Points * dpi / 72.0;
}
