namespace Basin.Themes.MsStyles;

public sealed class MsStylesSysMetrics
{
    internal MsStylesSysMetrics(MsStylesClass? metrics)
    {
        Class = metrics;
    }

    public MsStylesClass? Class { get; }

    public bool FlatMenus => Class is not null && Class.TryGetBool(0, 0, ThemeProperty.FlatMenus, out var flat) && flat;

    public bool TryGetColor(SysColor color, out MsStylesColor value)
    {
        value = default;
        return Class is not null && Class.TryGetColor(0, 0, (ThemeProperty)color, out value);
    }

    public bool TryGetSize(SysSize size, out int value)
    {
        value = 0;
        return Class is not null && Class.TryGetSize(0, 0, (ThemeProperty)size, out value);
    }

    public bool TryGetFont(SysFont font, out MsStylesFont value)
    {
        value = default;
        return Class is not null && Class.TryGetFont(0, 0, (ThemeProperty)font, out value);
    }
}
