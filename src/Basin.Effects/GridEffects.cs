namespace Basin.Effects;

public readonly record struct GridEffects
{
    public GridEffects()
    {
    }

    public static GridEffects None { get; } = new();

    public float Glow { get; init; }

    public float GlowRadius { get; init; } = 6f;

    public bool Density { get; init; }

    public float DensitySpacing { get; init; } = 12f;

    public float DensityFloor { get; init; } = 0.15f;

    public GridGlowPath GlowPath { get; init; }

    public bool Glows => Glow > 0f && GlowRadius > 0f && GlowPath != GridGlowPath.None;

    public bool DrawsHalo => Glows && GlowPath is GridGlowPath.Auto or GridGlowPath.Halo;

    public float DensityOf(double spacing)
    {
        if (!Density)
        {
            return 1f;
        }

        var floor = Math.Clamp(DensityFloor, 0f, 1f);
        return (float)Math.Clamp(spacing / Math.Max(1e-3, DensitySpacing), floor, 1.0);
    }
}
