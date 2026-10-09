using Basin.Frames.Metacity;

namespace PortComp;

public static class PortCompTheme
{
    private const string ResourceName = "port-comp-metacity-theme-1.xml";

    public const string Name = "PortComp";

    public static MetacityTheme? Load(string name)
    {
        using var stream = typeof(PortCompTheme).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"the embedded theme '{ResourceName}' is missing from the sample");
        return MetacityTheme.Parse(stream, directory: ".", majorVersion: 1, name: Name);
    }
}
