namespace Basin.AppMenu;

public sealed class AppMenuItem
{
    public int Id { get; init; }

    public string Label { get; set; } = "";

    public bool Enabled { get; set; } = true;

    public bool Visible { get; set; } = true;

    public AppMenuItemKind Kind { get; set; }

    public bool ToggleState { get; set; }

    public AppMenuShortcut? Shortcut { get; set; }

    public string? IconName { get; set; }

    public byte[]? IconData { get; set; }

    public bool IsSubmenu { get; set; }

    public List<AppMenuItem> Children { get; } = [];

    internal object? Tag { get; set; }
}
