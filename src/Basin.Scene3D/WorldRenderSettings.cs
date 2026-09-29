namespace Basin.Scene3D;

public sealed class WorldRenderSettings
{
    public int PortalDepth { get; set; } = 3;

    public int RoomDepth { get; set; } = 4;

    public int MaxLights { get; set; } = 8;

    public float Anisotropy { get; set; } = 8;

    public float DirectSampleMin { get; set; } = 0.85f;

    public float DirectSampleMax { get; set; } = 1.2f;
}
