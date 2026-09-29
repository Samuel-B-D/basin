namespace Basin.AppMenu;

public sealed class AppMenu
{
    private readonly List<AppMenuItem> _items = [];

    public IReadOnlyList<AppMenuItem> Items => _items;

    public event Action? Changed;

    public AppMenuItem? Find(int id) => Find(_items, id);

    public void Replace(IEnumerable<AppMenuItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items.Clear();
        _items.AddRange(items);
        Changed?.Invoke();
    }

    public void NotifyChanged() => Changed?.Invoke();

    private static AppMenuItem? Find(List<AppMenuItem> items, int id)
    {
        foreach (var item in items)
        {
            if (item.Id == id)
            {
                return item;
            }

            if (Find(item.Children, id) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
