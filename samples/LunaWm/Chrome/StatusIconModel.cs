using Avalonia.Media;

namespace LunaWm;

public sealed class StatusIconModel
{
    public required string Key { get; init; }

    public required string Letter { get; init; }

    public IImage? Icon { get; init; }

    public bool HasIcon => Icon is not null;
}
