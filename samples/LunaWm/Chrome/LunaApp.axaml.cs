using Avalonia;
using Avalonia.Markup.Xaml;

namespace LunaWm;

public sealed class LunaApp : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
}
