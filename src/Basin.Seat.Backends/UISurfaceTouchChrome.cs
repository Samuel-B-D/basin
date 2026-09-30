using Basin.Scene;

namespace Basin.Seat.Backends;

public sealed class UISurfaceTouchChrome : ITouchChrome
{
    private readonly UISurfaceRouter _router;

    public UISurfaceTouchChrome(UISurfaceRouter router)
    {
        ArgumentNullException.ThrowIfNull(router);
        _router = router;
    }

    public bool TryPress(int id, uint timeMs, double x, double y) => _router.TouchDown(timeMs, id, x, y);

    public void Motion(int id, uint timeMs, double x, double y) => _router.TouchMotion(timeMs, id, x, y);

    public void Release(int id, uint timeMs, double x, double y) => _router.TouchUp(timeMs, id);

    public void Cancel() => _router.TouchCancel();
}
