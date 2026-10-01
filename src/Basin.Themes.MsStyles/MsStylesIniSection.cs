namespace Basin.Themes.MsStyles;

internal sealed class MsStylesIniSection
{
    public MsStylesIniSection(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public List<(string Name, string Value)> Values { get; } = [];
}
