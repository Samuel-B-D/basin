namespace DungeonWm;

internal static class PlaqueLayout
{
    public static readonly PlaqueButton[] Buttons = [PlaqueButton.Rehang, PlaqueButton.Throw, PlaqueButton.Takeover, PlaqueButton.Close];

    public static float Side(int height) => height * 0.62f;

    public static float Gap(int height) => height * 0.14f;

    public static float Margin(int height) => height * 0.2f;

    public static (float X, float Y, float Size) Rect(PlaqueButton button, int width, int height)
    {
        var side = Side(height);
        var gap = Gap(height);
        var index = Array.IndexOf(Buttons, button);
        var fromRight = Buttons.Length - 1 - index;
        var x = width - Margin(height) - side - (fromRight * (side + gap));
        return (x, (height - side) * 0.5f, side);
    }

    public static float TitleRight(int width, int height) =>
        Rect(Buttons[0], width, height).X - Gap(height);

    public static PlaqueButton At(double x, double y, int width, int height)
    {
        foreach (var button in Buttons)
        {
            var (left, top, size) = Rect(button, width, height);
            var slack = Gap(height) * 0.5f;
            if (x >= left - slack && x < left + size + slack && y >= top - slack && y < top + size + slack)
            {
                return button;
            }
        }

        return PlaqueButton.None;
    }

    public static string Name(PlaqueButton button) => button.ToString().ToLowerInvariant();

    public static PlaqueButton Parse(string name) => name switch
    {
        "close" => PlaqueButton.Close,
        "takeover" => PlaqueButton.Takeover,
        "rehang" => PlaqueButton.Rehang,
        "throw" => PlaqueButton.Throw,
        _ => PlaqueButton.None,
    };
}
