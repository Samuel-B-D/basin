namespace Basin.Seat;

public interface IHoldDragHandler
{
    bool Accepts(double x, double y);

    bool Begin(double x, double y, uint timeMs);

    void Update(double x, double y, double radians, uint timeMs);

    void End(bool cancelled, double radiansPerSecond, uint timeMs);
}
