using Android.App;
using Android.Content.PM;
using Avalonia.Android;

namespace PortComp.Android;

[Activity(
    Label = "PortComp",
    Theme = "@style/Theme.AppCompat.NoActionBar",
    MainLauncher = true,
    Exported = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode | ConfigChanges.KeyboardHidden)]
public sealed class MainActivity : AvaloniaMainActivity
{
}
