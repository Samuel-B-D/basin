using Basin.Capabilities;
using Basin.Capabilities.Defaults;

namespace Basin.Desktop;

public sealed class PointerConstraintsModule : DesktopModule<PointerConstraintsManager>
{
    public override string WireInterface => "zwp_pointer_constraints_v1";

    public override int Version => PointerConstraintsManager.Version;

    protected override PointerConstraintsManager Create(BasinServices services)
    {
        var manager = new PointerConstraintsManager(services.Display, services.Require<CompositorGlobal>(), services.Find<Seat.Seat>());
        if (manager.Driver is { } driver)
        {
            services.Use(driver);
        }

        return manager;
    }
}
