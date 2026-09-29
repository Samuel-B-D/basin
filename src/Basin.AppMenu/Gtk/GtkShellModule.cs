namespace Basin.AppMenu;

public sealed class GtkShellModule : IProtocolModule
{
    public string WireInterface => "gtk_shell1";

    public int Version => GtkShellManager.Version;

    public GtkShellManager? Manager { get; private set; }

    public void SeedDefaults(BasinServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.UseDefault(new AppMenuAddresses());
    }

    public IDisposable Install(BasinServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        Manager = new GtkShellManager(services.Display, services.Require<CompositorGlobal>(), services.Require<AppMenuAddresses>());
        services.Use(Manager);
        return Manager;
    }
}
