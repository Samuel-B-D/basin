namespace Basin.Seat;

public interface IPinchHandler
{
    bool Accepts(double x, double y);

    bool Begin(double centroidX, double centroidY, double spread, uint timeMs);

    void Update(double scale, double centroidX, double centroidY, uint timeMs);

    void End(double scalePerSecond, uint timeMs);

    void Cancel();
}
