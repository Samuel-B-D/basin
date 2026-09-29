using System.Numerics;

namespace Basin.Scene3D;

public struct WorldLight
{
    public Vector3 Position;
    public Vector3 Color;
    public float Range;
    public float Intensity;
    public float Flicker;
    public float Phase;

    public WorldLight(Vector3 position, Vector3 color, float range, float intensity, float flicker = 0, float phase = 0)
    {
        Position = position;
        Color = color;
        Range = range;
        Intensity = intensity;
        Flicker = flicker;
        Phase = phase;
    }

    public readonly float IntensityAt(long nanos)
    {
        if (Flicker <= 0)
        {
            return Intensity;
        }

        var seconds = (nanos % 1_000_000_000_000L) / 1e9f;
        var wave = (0.6f * MathF.Sin((seconds * 7.3f) + Phase)) + (0.4f * MathF.Sin((seconds * 13.1f) + (Phase * 1.7f)));
        return Intensity * (1f + (Flicker * wave));
    }
}
