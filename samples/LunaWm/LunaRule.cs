using Basin.Config;

namespace LunaWm;

internal sealed class LunaRule : WindowRule
{
    public StackChoice Stack { get; init; }

    public string? Output { get; init; }

    public bool? Fullscreen { get; init; }
}
