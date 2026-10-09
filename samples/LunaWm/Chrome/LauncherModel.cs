using System.Collections.ObjectModel;

namespace LunaWm;

public sealed class LauncherModel : ObservableModel
{
    public const int Columns = 7;

    private LauncherTabModel? _current;
    private bool _reordering;
    private int _dropSlot = -1;

    public ObservableCollection<LauncherTabModel> Tabs { get; } = [];

    public ObservableCollection<LauncherIconModel> Dock { get; } = [];

    public LauncherTabModel? Current
    {
        get => _current;
        set
        {
            if (ReferenceEquals(_current, value))
            {
                return;
            }

            if (_current is not null)
            {
                _current.Selected = false;
            }

            _current = value;
            if (value is not null)
            {
                value.Selected = true;
            }

            Raise(nameof(Current));
            Raise(nameof(CurrentIcons));
        }
    }

    public ObservableCollection<LauncherIconModel>? CurrentIcons => _current?.Icons;

    public bool Reordering
    {
        get => _reordering;
        set
        {
            if (Set(ref _reordering, value))
            {
                foreach (var tab in Tabs)
                {
                    foreach (var icon in tab.Icons)
                    {
                        icon.Reordering = value;
                    }
                }

                ReorderingChanged?.Invoke(value);
            }
        }
    }

    public int DropSlot
    {
        get => _dropSlot;
        set => Set(ref _dropSlot, value);
    }

    public event Action<LauncherIconModel>? Launched;

    public event Action<LauncherIconModel, string, int>? Moved;

    public event Action<LauncherIconModel>? Hidden;

    public event Action<int, LauncherIconModel>? Docked;

    public event Action<bool>? ReorderingChanged;

    public event Action? SearchRequested;

    public event Action? Dismissed;

    public void Select(string name)
    {
        foreach (var tab in Tabs)
        {
            if (string.Equals(tab.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                Current = tab;
                return;
            }
        }
    }

    public LauncherIconModel? Find(string nameOrId)
    {
        foreach (var tab in Tabs)
        {
            foreach (var icon in tab.Icons)
            {
                if (icon.Id == nameOrId || string.Equals(icon.Name, nameOrId, StringComparison.OrdinalIgnoreCase))
                {
                    return icon;
                }
            }
        }

        return null;
    }

    public void Launch(LauncherIconModel icon)
    {
        if (!Reordering)
        {
            Launched?.Invoke(icon);
        }
    }

    public void MoveWithin(LauncherIconModel icon, int index)
    {
        if (Current is not { } tab)
        {
            return;
        }

        var from = tab.Icons.IndexOf(icon);
        index = Math.Clamp(index, 0, Math.Max(0, tab.Icons.Count - 1));
        if (from < 0 || from == index)
        {
            return;
        }

        tab.Icons.Move(from, index);
        Moved?.Invoke(icon, tab.Name, index);
    }

    public void MoveToTab(LauncherIconModel icon, LauncherTabModel target)
    {
        if (ReferenceEquals(target, Current) || Current is not { } from)
        {
            return;
        }

        from.Icons.Remove(icon);
        target.Icons.Add(icon);
        Moved?.Invoke(icon, target.Name, target.Icons.Count - 1);
    }

    public void Hide(LauncherIconModel icon)
    {
        foreach (var tab in Tabs)
        {
            tab.Icons.Remove(icon);
        }

        Hidden?.Invoke(icon);
    }

    public void DockAt(int slot, LauncherIconModel icon) => Docked?.Invoke(slot, icon);

    public void Search() => SearchRequested?.Invoke();

    public void Dismiss() => Dismissed?.Invoke();
}
