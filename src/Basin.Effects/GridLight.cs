namespace Basin.Effects;

public readonly record struct GridLight(float X, float Y, float Width, float Height, float Strength)
{
    public float Right => X + Width;

    public float Bottom => Y + Height;
}
