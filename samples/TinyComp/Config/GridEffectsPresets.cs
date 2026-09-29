namespace TinyComp;

internal static class GridEffectsPresets
{
    public const string CurvedName = "curved";

    public static GridEffectsSetting Curved { get; } = new()
    {
        Glow = 0.45,
        HighlightDragged = 1.0,
        HighlightFocused = 0.4,
        HighlightWindows = 0.15,
        HighlightPointer = 0.3,
        Density = true,
        Pinch = -0.8,
    };

    public static GridEffectsSetting? Named(string name) => name switch
    {
        CurvedName => Curved,
        _ => null,
    };
}
