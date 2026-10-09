using System.Collections.ObjectModel;

namespace LunaWm;

public sealed class LauncherTabModel : ObservableModel
{
    private bool _selected;

    public required string Name { get; init; }

    public ObservableCollection<LauncherIconModel> Icons { get; } = [];

    public bool Selected
    {
        get => _selected;
        set => Set(ref _selected, value);
    }
}
