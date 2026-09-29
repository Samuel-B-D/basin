namespace DungeonWm;

internal sealed record FrameSettings(
    bool Enabled = true,
    float Width = 0.06f,
    float Depth = 0.04f,
    float Recess = 0.015f,
    string[]? Styles = null,
    bool Plaque = true,
    float PlaqueHeight = 0.07f)
{
    public static readonly string[] AllStyles = ["oak", "gilt", "iron", "stone"];

    public string[] StyleNames => Styles is { Length: > 0 } styles ? styles : AllStyles;

    public bool SameGeometry(FrameSettings other) =>
        Enabled == other.Enabled && Width == other.Width && Depth == other.Depth && Recess == other.Recess &&
        Plaque == other.Plaque && PlaqueHeight == other.PlaqueHeight && StyleNames.SequenceEqual(other.StyleNames);
}
