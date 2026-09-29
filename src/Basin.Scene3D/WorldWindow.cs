using System.Numerics;

namespace Basin.Scene3D;

public sealed class WorldWindow
{
    private WorldSurfaceLayer[] _layers = new WorldSurfaceLayer[4];

    public Vector3 Position { get; set; }

    public Quaternion Orientation { get; set; } = Quaternion.Identity;

    public float Width { get; set; } = 1;

    public float Height { get; set; } = 1;

    public float Depth { get; set; } = 0.02f;

    public int SurfaceWidth { get; set; } = 1;

    public int SurfaceHeight { get; set; } = 1;

    public float Opacity { get; set; } = 1;

    public float CanvasOpacity { get; set; } = 1;

    public bool Pickable { get; set; } = true;

    public bool Focused { get; set; }

    public bool Visible { get; set; } = true;

    public Vector4 EdgeColor { get; set; } = new(0.25f, 0.22f, 0.2f, 1);

    public Vector4 FocusColor { get; set; } = new(0.95f, 0.75f, 0.35f, 1);

    public float Flash { get; set; }

    public WorldRoom? Room { get; set; }

    public WorldWindowFrame? Frame { get; set; }

    public WorldWindowFrame? ShownFrame => Frame is { Visible: true } frame ? frame : null;

    public float Recess => ShownFrame?.Recess ?? 0;

    public object? Tag { get; set; }

    public int LayerCount { get; private set; }

    public ReadOnlySpan<WorldSurfaceLayer> Layers => _layers.AsSpan(0, LayerCount);

    public Span<WorldSurfaceLayer> EditableLayers => _layers.AsSpan(0, LayerCount);

    public Surface? Surface => LayerCount > 0 ? _layers[0].Surface : null;

    public Matrix4x4 Model => Matrix4x4.CreateFromQuaternion(Orientation) * Matrix4x4.CreateTranslation(Position);

    public bool IsTranslucent => Opacity < 0.999f || CanvasOpacity < 0.999f;

    public Vector3 Normal => Vector3.Transform(Vector3.UnitZ, Orientation);

    public float MetersPerPixel => SurfaceWidth > 0 ? Width / SurfaceWidth : 0;

    public WorldBounds LocalBounds => ShownFrame is { } frame
        ? frame.HasPlaque ? frame.OuterBox(Width, Height).Union(frame.PlaqueBox(Height)) : frame.OuterBox(Width, Height)
        : new WorldBounds(new Vector3(-Width * 0.5f, -Height * 0.5f, -Depth), new Vector3(Width * 0.5f, Height * 0.5f, 0));

    public WorldBounds Bounds => LocalBounds.Transformed(Model);

    public float LayerDepth(in WorldSurfaceLayer layer)
    {
        var lift = layer.Level * WorldPicker.LayerLift;
        if (ShownFrame is not { } frame)
        {
            return lift;
        }

        var inside = layer.X >= -0.5f && layer.Y >= -0.5f &&
            layer.X + layer.Width <= SurfaceWidth + 0.5f && layer.Y + layer.Height <= SurfaceHeight + 0.5f;
        return inside ? lift - frame.Recess : lift;
    }

    public void ClearLayers() => LayerCount = 0;

    public void AddLayer(in WorldSurfaceLayer layer)
    {
        if (LayerCount == _layers.Length)
        {
            Array.Resize(ref _layers, _layers.Length * 2);
        }

        _layers[LayerCount++] = layer;
    }

    public Vector3 SurfacePoint(float surfaceX, float surfaceY, float lift = 0)
    {
        var local = new Vector3(
            (surfaceX * MetersPerPixel) - (Width * 0.5f),
            (Height * 0.5f) - (surfaceY * (SurfaceHeight > 0 ? Height / SurfaceHeight : 0)),
            lift - Recess);
        return Vector3.Transform(local, Model);
    }
}
