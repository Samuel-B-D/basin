namespace Basin.AppMenu;

public readonly record struct AppMenuShortcut(AppMenuModifiers Modifiers, string Keysym)
{
    public static AppMenuShortcut? FromDbusMenu(IReadOnlyList<string> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        var modifiers = AppMenuModifiers.None;
        string? key = null;
        foreach (var part in parts)
        {
            var modifier = ModifierNamed(part);
            if (modifier != AppMenuModifiers.None)
            {
                modifiers |= modifier;
            }
            else
            {
                key = part;
            }
        }

        return key is { Length: > 0 } ? new AppMenuShortcut(modifiers, Normalize(key)) : null;
    }

    public static AppMenuShortcut? FromGtkAccelerator(string accelerator)
    {
        ArgumentNullException.ThrowIfNull(accelerator);
        var modifiers = AppMenuModifiers.None;
        var rest = accelerator.AsSpan().Trim();
        while (rest.Length > 0 && rest[0] == '<')
        {
            var end = rest.IndexOf('>');
            if (end < 0)
            {
                return null;
            }

            modifiers |= ModifierNamed(rest[1..end].ToString());
            rest = rest[(end + 1)..];
        }

        return rest.Length > 0 ? new AppMenuShortcut(modifiers, Normalize(rest.ToString())) : null;
    }

    private static string Normalize(string key) => key.Length == 1 ? key.ToLowerInvariant() : key;

    private static AppMenuModifiers ModifierNamed(string name) => name.ToLowerInvariant() switch
    {
        "control" or "ctrl" or "primary" => AppMenuModifiers.Control,
        "shift" => AppMenuModifiers.Shift,
        "alt" or "mod1" => AppMenuModifiers.Alt,
        "super" or "meta" or "mod4" or "logo" => AppMenuModifiers.Super,
        _ => AppMenuModifiers.None,
    };
}
