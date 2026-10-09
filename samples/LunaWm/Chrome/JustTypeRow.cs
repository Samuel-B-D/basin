using Avalonia.Media;

namespace LunaWm;

public sealed class JustTypeRow : ObservableModel
{
    private bool _selected;

    public required string Kind { get; init; }

    public required string Label { get; init; }

    public string Detail { get; init; } = "";

    public string Key { get; init; } = "";

    public bool IsHeader { get; init; }

    public bool IsResult => !IsHeader;

    public IImage? Icon { get; init; }

    public bool HasIcon => Icon is not null;

    public bool Selected
    {
        get => _selected;
        set => Set(ref _selected, value);
    }
}
