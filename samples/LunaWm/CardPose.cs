namespace LunaWm;

internal readonly record struct CardPose(double X, double Y, double Scale, double Rotation, double Brightness)
{
    public static CardPose Lerp(in CardPose a, in CardPose b, double t) => new(
        a.X + ((b.X - a.X) * t),
        a.Y + ((b.Y - a.Y) * t),
        a.Scale + ((b.Scale - a.Scale) * t),
        a.Rotation + ((b.Rotation - a.Rotation) * t),
        a.Brightness + ((b.Brightness - a.Brightness) * t));
}
