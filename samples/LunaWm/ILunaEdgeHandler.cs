namespace LunaWm;

internal interface ILunaEdgeHandler
{
    bool TryBegin(double x, double y, out LunaEdge edge, out double scale);

    bool Claim(LunaEdge edge, EdgeAxis axis, double x, double y, uint timeMs);

    void Move(double x, double y, uint timeMs);

    void End(double velocityX, double velocityY, uint timeMs, bool cancelled);
}
