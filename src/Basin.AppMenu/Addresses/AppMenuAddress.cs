namespace Basin.AppMenu;

public sealed record AppMenuAddress(
    AppMenuAddressKind Kind,
    string BusName,
    string ObjectPath = "",
    string MenubarPath = "",
    string AppMenuPath = "",
    string ApplicationPath = "",
    string WindowPath = "")
{
    public static AppMenuAddress DbusMenu(string busName, string objectPath) => new(AppMenuAddressKind.DbusMenu, busName, objectPath);
}
