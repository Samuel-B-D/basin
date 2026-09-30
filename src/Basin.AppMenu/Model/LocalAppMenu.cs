namespace Basin.AppMenu;

public sealed class LocalAppMenu(AppMenuModifiers commandModifiers = AppMenuModifiers.Control) : IAppMenuSource
{
    private readonly Dictionary<int, Action> _actions = [];
    private readonly Dictionary<int, Action> _opening = [];
    private int _next = 1;

    public AppMenuAddressKind Kind => AppMenuAddressKind.DbusMenu;

    public AppMenu Menu { get; } = new();

    public bool IsReady => true;

    public event Action? Failed
    {
        add { }
        remove { }
    }

    public void Set(params AppMenuItem[] titles)
    {
        Menu.Replace(titles);
    }

    public void Clear()
    {
        _actions.Clear();
        _opening.Clear();
        _next = 1;
    }

    public AppMenuItem Item(string label, Action? action, char? command = null, bool enabled = true, bool? check = null, string? iconName = null)
    {
        var item = new AppMenuItem
        {
            Id = _next++,
            Label = label,
            Enabled = enabled && action is not null,
            Kind = check is null ? AppMenuItemKind.Normal : AppMenuItemKind.Check,
            ToggleState = check ?? false,
            Shortcut = command is { } letter ? new AppMenuShortcut(commandModifiers, char.ToLowerInvariant(letter).ToString()) : null,
            IconName = iconName,
        };
        if (action is not null)
        {
            _actions[item.Id] = action;
        }

        return item;
    }

    public AppMenuItem Separator() => new() { Id = _next++, Kind = AppMenuItemKind.Separator };

    public AppMenuItem Submenu(string label, IEnumerable<AppMenuItem> children, Action? opening = null, bool enabled = true, string? iconName = null)
    {
        var item = new AppMenuItem { Id = _next++, Label = label, IsSubmenu = true, Enabled = enabled, IconName = iconName };
        item.Children.AddRange(children);
        if (opening is not null)
        {
            _opening[item.Id] = opening;
        }

        return item;
    }

    public void AboutToShow(AppMenuItem item)
    {
        if (_opening.TryGetValue(item.Id, out var opening))
        {
            opening();
        }
    }

    public void Activate(AppMenuItem item, uint timestamp)
    {
        if (_actions.TryGetValue(item.Id, out var action))
        {
            action();
        }
    }

    public void Dispose()
    {
    }
}
