using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace LunaWm;

public sealed partial class AlertView : UserControl
{
    public AlertView() => InitializeComponent();

    private void OnAction(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: string key } && DataContext is AlertModel model)
        {
            model.Choose(key);
        }
    }
}
