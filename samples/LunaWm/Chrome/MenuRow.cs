namespace LunaWm;

public abstract class MenuRow : ObservableModel
{
    private string _label = "";

    public string Label
    {
        get => _label;
        set => Set(ref _label, value);
    }

    public string Key { get; init; } = "";
}
