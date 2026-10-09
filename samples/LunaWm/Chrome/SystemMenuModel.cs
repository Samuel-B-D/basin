using System.Collections.ObjectModel;

namespace LunaWm;

public sealed class SystemMenuModel : ObservableModel
{
    private string _kind = "";
    private double _panelLeft;
    private double _panelWidth = 300;

    public ObservableCollection<MenuRow> Rows { get; } = [];

    public string Kind
    {
        get => _kind;
        set => Set(ref _kind, value);
    }

    public double PanelLeft
    {
        get => _panelLeft;
        set
        {
            if (Set(ref _panelLeft, value))
            {
                Raise(nameof(PanelMargin));
            }
        }
    }

    public double PanelWidth
    {
        get => _panelWidth;
        set => Set(ref _panelWidth, value);
    }

    public Avalonia.Thickness PanelMargin => new(_panelLeft, 0, 0, 0);

    public event Action? Dismissed;

    public event Action<MenuRow>? Chosen;

    public void Dismiss() => Dismissed?.Invoke();

    public void Choose(MenuRow row) => Chosen?.Invoke(row);
}
