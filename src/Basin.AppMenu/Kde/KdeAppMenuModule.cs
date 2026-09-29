namespace Basin.AppMenu;

public sealed class KdeAppMenuModule : IProtocolModule
{
    public string WireInterface => "org_kde_kwin_appmenu_manager";

    public int Version => KdeAppMenuManager.Version;

    public KdeAppMenuManager? Manager { get; private set; }

    public void SeedDefaults(BasinServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.UseDefault(new AppMenuAddresses());
    }

    public IDisposable Install(BasinServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        Manager = new KdeAppMenuManager(services.Display, services.Require<CompositorGlobal>(), services.Require<AppMenuAddresses>());
        services.Use(Manager);
        return Manager;
    }
}
