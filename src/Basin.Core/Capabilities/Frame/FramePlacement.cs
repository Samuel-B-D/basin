namespace Basin.Capabilities;

public readonly record struct FramePlacement(int X, int Y, int OutputWidth, int OutputHeight)
{
    public bool IsEmpty => OutputWidth <= 0 || OutputHeight <= 0;
}
