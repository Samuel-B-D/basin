using Avalonia.Media;

namespace LunaWm;

public sealed class LauncherIconModel : ObservableModel
{
    private bool _dragging;
    private bool _reordering;

    public required string Id { get; init; }

    public required string Name { get; init; }

    public IImage? Icon { get; init; }

    public bool HasIcon => Icon is not null;

    public string Letter => Name.Length > 0 ? char.ToUpperInvariant(Name[0]).ToString() : "?";

    public IBrush Tile { get; init; } = Brushes.SlateGray;

    public bool Dragging
    {
        get => _dragging;
        set
        {
            if (Set(ref _dragging, value))
            {
                Raise(nameof(Opacity));
            }
        }
    }

    public bool Reordering
    {
        get => _reordering;
        set => Set(ref _reordering, value);
    }

    public double Opacity => _dragging ? 0.3 : 1;
}
