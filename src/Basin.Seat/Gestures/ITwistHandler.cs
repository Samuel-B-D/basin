namespace Basin.Seat;

public interface ITwistHandler
{
    bool Accepts(double x, double y);

    bool Begin(double anchorX, double anchorY, uint timeMs);

    void Update(double radians, uint timeMs);

    void End(bool cancelled, double radiansPerSecond, uint timeMs);
}
