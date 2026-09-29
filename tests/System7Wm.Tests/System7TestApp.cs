using Avalonia;
using Avalonia.Headless;

namespace System7Wm.Tests;

public static class System7TestApp
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<ShellApp>()
        .UseSkia()
        .UseHarfBuzz()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
