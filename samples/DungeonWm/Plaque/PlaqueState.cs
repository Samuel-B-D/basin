namespace DungeonWm;

internal readonly record struct PlaqueState(
    string Title, bool Focused, PlaqueButton Hover, PlaqueButton Pressed, int Width, int Height);
