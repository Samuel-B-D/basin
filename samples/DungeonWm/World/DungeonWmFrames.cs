using System.Numerics;
using Basin.Scene3D;

namespace DungeonWm;

internal sealed class DungeonWmFrames
{
    public const float PlaquePixelsPerMeter = 2000;
    private const int CacheLimit = 48;

    private readonly Dictionary<(float Width, float Height, string Style), WorldMesh> _meshes = [];
    private readonly WorldImage? _atlas;
    private DungeonWmFrameStyle[] _styles = [];

    public DungeonWmFrames(WorldImage? atlas, FrameSettings settings, int seed)
    {
        _atlas = atlas;
        Configure(settings, seed);
    }

    public FrameSettings Settings { get; private set; } = new();

    public int Seed { get; private set; }

    public int CachedMeshes => _meshes.Count;

    public void Configure(FrameSettings settings, int seed)
    {
        Settings = settings;
        Seed = seed;
        _styles = [.. settings.StyleNames.Select(DungeonWmFrameStyle.Named).OfType<DungeonWmFrameStyle>()];
        if (_styles.Length == 0)
        {
            _styles = [DungeonWmFrameStyle.Named("oak")!];
        }

        _meshes.Clear();
    }

    public DungeonWmFrameStyle StyleOf(MapRegion room) => _styles[StableHash.Of(Seed, room.Id, 0x6672) % _styles.Length];

    public float RailWidth => Settings.Enabled ? Settings.Width : 0;

    public float ExtraWidth => 2 * RailWidth;

    public float ExtraHeight => !Settings.Enabled ? 0
        : (2 * Settings.Width) + (Settings.Plaque ? new WorldWindowFrame().PlaqueGap + Settings.PlaqueHeight : 0);

    public DungeonWmFrameStyle Style(string name) =>
        _styles.FirstOrDefault(style => style.Name == name) ?? DungeonWmFrameStyle.Named(name) ?? _styles[0];

    public void Apply(WorldWindow world, DungeonWmFrameStyle? style, bool shown)
    {
        if (!Settings.Enabled)
        {
            world.Frame = null;
            return;
        }

        var frame = world.Frame ??= new WorldWindowFrame();
        style ??= _styles[0];
        frame.Visible = shown;
        frame.Rail = Settings.Width;
        frame.Depth = Settings.Depth;
        frame.Recess = Math.Min(Settings.Recess, Settings.Depth * 0.9f);
        frame.Material = style.Material;
        frame.Moulding = Mesh(world.Width, world.Height, style);
        frame.PlaqueMaterial = DungeonWmFrameStyle.Brass;
        if (Settings.Plaque)
        {
            var outer = world.Width + (2 * Settings.Width);
            frame.PlaqueHeight = Settings.PlaqueHeight;
            frame.PlaqueWidth = MathF.Min(outer, Math.Clamp(outer * 0.6f, 0.32f, 0.62f));
            frame.PlaquePixelWidth = Math.Max(1, (int)MathF.Round(frame.PlaqueWidth * PlaquePixelsPerMeter));
            frame.PlaquePixelHeight = Math.Max(1, (int)MathF.Round(frame.PlaqueHeight * PlaquePixelsPerMeter));
        }
        else
        {
            frame.PlaqueWidth = frame.PlaqueHeight = 0;
            frame.PlaqueTexture = null;
        }
    }

    public void Trim(IEnumerable<DungeonWmWindow> windows)
    {
        if (_meshes.Count <= CacheLimit)
        {
            return;
        }

        var used = new HashSet<WorldMesh>();
        foreach (var window in windows)
        {
            if (window.World.Frame?.Moulding is { } mesh)
            {
                used.Add(mesh);
            }
        }

        foreach (var key in _meshes.Where(pair => !used.Contains(pair.Value)).Select(pair => pair.Key).ToList())
        {
            _meshes.Remove(key);
        }
    }

    private WorldMesh Mesh(float width, float height, DungeonWmFrameStyle style)
    {
        var key = (width, height, style.Name);
        if (!_meshes.TryGetValue(key, out var mesh))
        {
            mesh = WorldPrimitives.Moulding(
                width, height, Settings.Width, Settings.Depth, Math.Min(Settings.Recess, Settings.Depth * 0.9f), style.Swatch, _atlas);
            _meshes[key] = mesh;
        }

        return mesh;
    }
}
