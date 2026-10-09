namespace LunaWm;

public sealed class DashboardItemModel : ObservableModel
{
    public required uint Id { get; init; }

    public required string Summary { get; init; }

    public string Body { get; init; } = "";

    public bool HasBody => Body.Length > 0;
}
