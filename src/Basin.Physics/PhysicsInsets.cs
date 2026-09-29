namespace Basin.Physics;

public readonly record struct PhysicsInsets(int Left, int Top, int Right, int Bottom)
{
    public int Horizontal => Left + Right;

    public int Vertical => Top + Bottom;
}
