using Basin.Config;

namespace System7Wm;

internal sealed class System7Rule : WindowRule
{
    public Modifiers EditChords { get; init; } = Modifiers.Ctrl;

    public string Decorations { get; init; } = "auto";
}
