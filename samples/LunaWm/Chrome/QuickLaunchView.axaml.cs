using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace LunaWm;

public sealed partial class QuickLaunchView : UserControl
{
    public QuickLaunchView() => InitializeComponent();

    private QuickLaunchModel? Model => DataContext as QuickLaunchModel;

    private void OnIcon(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Control { Tag: LauncherIconModel icon })
        {
            Model?.Launch(icon);
        }
    }

    private void OnLauncher(object? sender, PointerReleasedEventArgs e) => Model?.OpenLauncher();
}
