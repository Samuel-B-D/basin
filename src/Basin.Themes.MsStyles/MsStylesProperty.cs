namespace Basin.Themes.MsStyles;

internal sealed class MsStylesProperty
{
    public MsStylesProperty(int id, ThemePrimitive primitive, PropertyOrigin origin, int format)
    {
        Id = id;
        Primitive = primitive;
        Origin = origin;
        Format = format;
    }

    public int Id { get; }

    public ThemePrimitive Primitive { get; }

    public PropertyOrigin Origin { get; }

    public int Format { get; }

    public int[] Values { get; set; } = [];

    public string? Text { get; set; }

    public MsStylesFont Font { get; set; }

    public bool HasFont { get; set; }

    public MsStylesImage Image { get; set; }

    public bool HasImage { get; set; }

    public int ResourceId { get; set; }
}
