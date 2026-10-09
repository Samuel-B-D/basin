namespace Basin.SoftKeyboard;

public readonly record struct KeyRect(float X, float Y, float Width, float Height)
{
    public float Right => X + Width;

    public float Bottom => Y + Height;

    public float CenterX => X + Width / 2;

    public float CenterY => Y + Height / 2;

    public bool Contains(float x, float y) => x >= X && x < Right && y >= Y && y < Bottom;
}
