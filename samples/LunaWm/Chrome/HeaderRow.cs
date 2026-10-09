namespace LunaWm;

public sealed class HeaderRow : MenuRow
{
    private string _detail = "";

    public string Detail
    {
        get => _detail;
        set => Set(ref _detail, value);
    }
}
