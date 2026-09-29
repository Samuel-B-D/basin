namespace Basin.AppMenu;

public static class AppMenuPack
{
    public static ProtocolPack Default => new(
    [
        new KdeAppMenuModule(),
        new GtkShellModule(),
    ]);
}
