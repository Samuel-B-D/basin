using System.Collections.ObjectModel;
using Avalonia.Media;

namespace LunaWm;

public sealed class DashboardGroupModel : ObservableModel
{
    private bool _expanded;
    private string _summary = "";
    private string _body = "";
    private int _count;

    public required string Key { get; init; }

    public required string AppName { get; init; }

    public IImage? Icon { get; init; }

    public bool HasIcon => Icon is not null;

    public string Letter => AppName.Length > 0 ? char.ToUpperInvariant(AppName[0]).ToString() : "?";

    public ObservableCollection<DashboardItemModel> Items { get; } = [];

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

    public int Count
    {
        get => _count;
        set
        {
            if (Set(ref _count, value))
            {
                Raise(nameof(Badge));
                Raise(nameof(HasBadge));
            }
        }
    }

    public string Badge => _count.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public bool HasBadge => _count > 1;

    public bool Expanded
    {
        get => _expanded;
        set => Set(ref _expanded, value);
    }
}
