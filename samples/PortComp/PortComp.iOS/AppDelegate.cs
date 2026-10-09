using Avalonia;
using Avalonia.iOS;
using Foundation;

namespace PortComp.iOS;

[Register("AppDelegate")]
public sealed class AppDelegate : AvaloniaAppDelegate<PortCompApp>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder)
            .With(new iOSPlatformOptions
            {
                RenderingMode = [iOSRenderingMode.Metal, iOSRenderingMode.OpenGl],
            });
}
