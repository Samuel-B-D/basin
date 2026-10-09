using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace LunaWm;

public sealed partial class SystemMenuView : UserControl
{
    public SystemMenuView() => InitializeComponent();

    private SystemMenuModel? Model => DataContext as SystemMenuModel;

    private void OnBackground(object? sender, PointerPressedEventArgs e) => Model?.Dismiss();

    private void OnPanel(object? sender, PointerPressedEventArgs e) => e.Handled = true;

    private void OnToggle(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Control { Tag: ToggleRow row })
        {
            row.Toggle();
            Model?.Choose(row);
        }
    }

    private void OnAction(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Control { Tag: ActionRow row, IsEffectivelyEnabled: true })
        {
            row.Invoked?.Invoke();
            Model?.Choose(row);
        }
    }
}
