namespace LunaWm;

public sealed class ActionRow : MenuRow
{
    private string _detail = "";
    private bool _checked;
    private bool _enabled = true;

    public string Detail
    {
        get => _detail;
        set => Set(ref _detail, value);
    }

    public bool Checked
    {
        get => _checked;
        set => Set(ref _checked, value);
    }

    public bool Enabled
    {
        get => _enabled;
        set => Set(ref _enabled, value);
    }

    public int Depth { get; init; }

    public Avalonia.Thickness Indent => new(Depth * 24, 0, 0, 0);

    public Action? Invoked { get; init; }
}
