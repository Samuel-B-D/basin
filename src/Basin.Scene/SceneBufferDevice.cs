namespace Basin.Scene;

internal sealed class SceneBufferDevice
{
    public (int Width, int Height)? Pixels { get; set; }

    public (int X, int Y) Nudge { get; set; }

    public double Scale { get; set; } = 1.0;
}
