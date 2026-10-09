using Avalonia.Controls;

namespace LunaWm;

public sealed partial class KeyboardView : UserControl
{
    public KeyboardView()
    {
        InitializeComponent();
    }

    public KeyboardModel? Model
    {
        get => Canvas.Model;
        set => Canvas.Model = value;
    }

    internal KeyboardCanvas Keys => Canvas;
}
