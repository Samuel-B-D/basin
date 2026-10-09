using System.Collections.ObjectModel;
using Avalonia.Media;

namespace LunaWm;

public sealed class AlertModel : ObservableModel
{
    private string _summary = "";
    private string _body = "";
    private IImage? _icon;
    private double _sheetWidth = 600;

    public uint Id { get; set; }

    public string Summary
    {
        get => _summary;
        set => Set(ref _summary, value);
    }

    public string Body
    {
        get => _body;
        set => Set(ref _body, value);
    }

    public IImage? Icon
    {
        get => _icon;
        set
        {
            if (Set(ref _icon, value))
            {
                Raise(nameof(HasIcon));
            }
        }
    }

    public bool HasIcon => _icon is not null;

    public double SheetWidth
    {
        get => _sheetWidth;
        set => Set(ref _sheetWidth, value);
    }

    public ObservableCollection<AlertActionModel> Actions { get; } = [];

    public event Action<string>? Chosen;

    public void Choose(string key) => Chosen?.Invoke(key);
}
