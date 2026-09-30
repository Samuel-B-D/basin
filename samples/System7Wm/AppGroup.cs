using Basin.AppMenu;
using System7.Avalonia.Rendering;

namespace System7Wm;

internal sealed class AppGroup
{
    public AppGroup(string key, string appId, uint pid, string name)
    {
        Key = key;
        AppId = appId;
        Pid = pid;
        Name = name;
    }

    public string Key { get; }

    public string AppId { get; }

    public uint Pid { get; }

    public string Name { get; set; }

    public bool IsFinder { get; init; }

    public System7IconFamily? Icon { get; set; }

    public List<ShellWindow> Windows { get; } = [];

    public bool Hidden { get; set; }

    public LocalAppMenu Synthesized { get; } = new();

    public IAppMenuSource MenusFor(ShellWindow? window) =>
        window?.Imported is { IsReady: true } imported && ReferenceEquals(window.App, this) ? imported
        : Windows.LastOrDefault(w => w.Imported is { IsReady: true })?.Imported ?? Synthesized;

    public string MenuKindFor(ShellWindow? window) => IsFinder ? "finder" : MenusFor(window) switch
    {
        LocalAppMenu => "synth",
        { Kind: AppMenuAddressKind.GMenu } => "gmenu",
        _ => "dbusmenu",
    };

    public int Layer { get; set; }
}
