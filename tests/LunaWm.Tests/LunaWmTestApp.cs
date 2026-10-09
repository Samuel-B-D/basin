using Avalonia;
using Avalonia.Headless;
using AvaEnyo;

namespace LunaWm.Tests;

public static class LunaWmTestApp
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<LunaApp>()
            .UseSkia()
            .UseHarfBuzz()
            .WithAvaEnyoFonts()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
