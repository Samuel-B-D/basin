using System.Collections.ObjectModel;

namespace LunaWm;

public sealed class QuickLaunchModel : ObservableModel
{
    public const double SlotWidth = 100;
    public const double Height = 80;

    private int _highlight = -1;

    public ObservableCollection<LauncherIconModel> Icons { get; } = [];

    public int Highlight
    {
        get => _highlight;
        set => Set(ref _highlight, value);
    }

    public event Action<LauncherIconModel>? Launched;

    public event Action? LauncherRequested;

    public void Launch(LauncherIconModel icon) => Launched?.Invoke(icon);

    public void OpenLauncher() => LauncherRequested?.Invoke();

    public int SlotAt(double x, double width)
    {
        var count = Icons.Count + 1;
        var start = (width - (count * SlotWidth)) / 2;
        var slot = (int)Math.Floor((x - start) / SlotWidth);
        return slot >= 0 && slot < count ? slot : -1;
    }
}
