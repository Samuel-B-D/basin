using System.Numerics;

namespace Basin.Scene3D;

public sealed class WorldWindowFrame
{
    public bool Visible { get; set; } = true;

    public float Rail { get; set; } = 0.06f;

    public float Depth { get; set; } = 0.04f;

    public float Recess { get; set; } = 0.015f;

    public float EdgeShare { get; set; } = 0.4f;

    public WorldMesh? Moulding { get; set; }

    public WorldMaterial Material { get; set; } = WorldMaterial.Default;

    public float PlaqueWidth { get; set; }

    public float PlaqueHeight { get; set; }

    public float PlaqueGap { get; set; } = 0.012f;

    public float PlaqueThickness { get; set; } = 0.008f;

    public WorldMaterial PlaqueMaterial { get; set; } = WorldMaterial.Default;

    public ITexture? PlaqueTexture { get; set; }

    public FBox PlaqueSource { get; set; }

    public int PlaquePixelWidth { get; set; }

    public int PlaquePixelHeight { get; set; }

    public int PlaqueVersion { get; set; }

    public bool HasPlaque => PlaqueWidth > 0 && PlaqueHeight > 0;

    public Vector2 OuterSize(float innerWidth, float innerHeight) =>
        new(innerWidth + (2 * Rail), innerHeight + (2 * Rail));

    public WorldBounds OuterBox(float innerWidth, float innerHeight)
    {
        var half = OuterSize(innerWidth, innerHeight) * 0.5f;
        return new WorldBounds(new Vector3(-half.X, -half.Y, -Depth), new Vector3(half.X, half.Y, 0));
    }

    public Vector3 PlaqueCenter(float innerHeight) =>
        new(0, -((innerHeight * 0.5f) + Rail + PlaqueGap + (PlaqueHeight * 0.5f)), PlaqueThickness - Depth);

    public WorldBounds PlaqueBox(float innerHeight)
    {
        var center = PlaqueCenter(innerHeight);
        var half = new Vector3(PlaqueWidth * 0.5f, PlaqueHeight * 0.5f, 0);
        return new WorldBounds(
            new Vector3(center.X - half.X, center.Y - half.Y, -Depth),
            new Vector3(center.X + half.X, center.Y + half.Y, center.Z));
    }

    public float PaintingHeight(float innerHeight) =>
        innerHeight + (2 * Rail) + (HasPlaque ? PlaqueGap + PlaqueHeight : 0);

    public float PaintingCenterOffset => HasPlaque ? -(PlaqueGap + PlaqueHeight) * 0.5f : 0;

    public WorldFramePart PartAt(Vector3 local, float innerWidth, float innerHeight)
    {
        var outX = MathF.Abs(local.X) - (innerWidth * 0.5f);
        var outY = MathF.Abs(local.Y) - (innerHeight * 0.5f);
        var right = local.X >= 0;
        var top = local.Y >= 0;
        if (outX > 0 && outY > 0)
        {
            return (right, top) switch
            {
                (true, true) => WorldFramePart.TopRight,
                (false, true) => WorldFramePart.TopLeft,
                (true, false) => WorldFramePart.BottomRight,
                _ => WorldFramePart.BottomLeft,
            };
        }

        var edge = Rail * EdgeShare;
        if (outX >= outY)
        {
            return outX < edge ? (right ? WorldFramePart.Right : WorldFramePart.Left) : WorldFramePart.Face;
        }

        return outY < edge ? (top ? WorldFramePart.Top : WorldFramePart.Bottom) : WorldFramePart.Face;
    }
}
