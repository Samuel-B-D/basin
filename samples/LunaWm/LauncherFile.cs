namespace LunaWm;

internal sealed class LauncherFile
{
    public int Version { get; set; } = 1;

    public List<LauncherFileTab> Tabs { get; set; } = [];

    public List<string> Hidden { get; set; } = [];

    public List<string>? QuickLaunch { get; set; }
}
