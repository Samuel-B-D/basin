namespace Basin.Frames.MsStyles;

public readonly struct MsStylesDrawContext
{
    public MsStylesDrawContext(MsStylesImageCache images, double dpi, float opacity = 1f)
    {
        Images = images;
        Dpi = dpi;
        Opacity = opacity;
    }

    public MsStylesImageCache Images { get; }

    public double Dpi { get; }

    public double Scale => Dpi / 96.0;

    public float Opacity { get; }
}
