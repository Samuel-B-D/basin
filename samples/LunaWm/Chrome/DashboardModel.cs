using System.Collections.ObjectModel;

namespace LunaWm;

public sealed class DashboardModel : ObservableModel
{
    public const double Width = 320;
    public const double RowHeight = 52;
    public const double BadgeWidth = 50;

    public ObservableCollection<DashboardGroupModel> Groups { get; } = [];

    public bool Empty => Groups.Count == 0;

    public event Action<DashboardGroupModel>? Tapped;

    public event Action<DashboardGroupModel>? Swiped;

    public event Action<DashboardItemModel>? ItemTapped;

    public event Action<DashboardItemModel>? ItemSwiped;

    public event Action? Dismissed;

    public void Tap(DashboardGroupModel group) => Tapped?.Invoke(group);

    public void Swipe(DashboardGroupModel group) => Swiped?.Invoke(group);

    public void TapItem(DashboardItemModel item) => ItemTapped?.Invoke(item);

    public void SwipeItem(DashboardItemModel item) => ItemSwiped?.Invoke(item);

    public void Dismiss() => Dismissed?.Invoke();

    public void Refresh() => Raise(nameof(Empty));
}
