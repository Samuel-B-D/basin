namespace Basin.AppMenu;

public interface IAppMenuSource : IDisposable
{
    AppMenuAddressKind Kind { get; }

    AppMenu Menu { get; }

    bool IsReady { get; }

    event Action? Failed;

    void AboutToShow(AppMenuItem item);

    void Activate(AppMenuItem item, uint timestamp);
}
