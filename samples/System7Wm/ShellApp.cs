using Avalonia;
using Avalonia.Styling;
using System7.Avalonia;

namespace System7Wm;

internal sealed class ShellApp : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Light;
        Styles.Add(new System7Theme());
    }
}
