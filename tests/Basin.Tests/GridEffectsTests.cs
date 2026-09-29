using Basin.Effects;
using Xunit;

namespace Basin.Tests;

public sealed class GridEffectsTests
{
    private static readonly MeshVertex Sentinel = new(-7, -7, -7, -7, default);

    private static GridLights Lit()
    {
        var lights = new GridLights { Radius = 60f };
        lights.Set(0, new Box(150, 120, 90, 70), 1f);
        lights.Set(3, new Box(20, 300, 40, 40), 0.4f);
        lights.SetPointer(400, 60, 0.7f);
        return lights;
    }

    public static TheoryData<int> Combinations() => new() { 0, 1, 2, 3, 4, 5, 6, 7 };

    private static void Apply(CanvasGridSource source, int combination)
    {
        source.Lights = (combination & 1) != 0 ? Lit() : new GridLights();
        source.Effects = GridEffects.None with
        {
            Density = (combination & 2) != 0,
            Glow = (combination & 4) != 0 ? 0.6f : 0f,
            GlowPath = GridGlowPath.Halo,
        };
    }

    private static void Apply(CanvasStepSource source, int combination)
    {
        source.Lights = (combination & 1) != 0 ? Lit() : new GridLights();
        source.Effects = GridEffects.None with
        {
            Density = (combination & 2) != 0,
            Glow = (combination & 4) != 0 ? 0.6f : 0f,
        };
    }

    private static MeshVertex[] WriteCounted(Basin.Scene.IMeshSource source, in Box bounds)
    {
        var count = source.VertexCount(bounds);
        var vertices = new MeshVertex[count + 6];
        Array.Fill(vertices, Sentinel);
        source.WriteVertices(bounds, vertices.AsSpan(0, count));
        for (var i = 0; i < count; i++)
        {
            Assert.NotEqual(Sentinel, vertices[i]);
            Assert.True(float.IsFinite(vertices[i].X) && float.IsFinite(vertices[i].Y), $"vertex {i} is not finite");
        }

        Assert.Equal(Sentinel, vertices[count]);
        return vertices[..count];
    }

    [Theory]
    [MemberData(nameof(Combinations))]
    public void Every_grid_counts_what_it_writes_with_every_effect_combination(int combination)
    {
        foreach (var kind in new[] { 0, 1, 2, 3, 4, 5 })
        {
            foreach (var reveal in new[] { GridReveal.Full, new GridReveal(GridRevealStyle.Ripple, 0.5f, 200, 150, 24), new GridReveal(GridRevealStyle.Fade, 0.5f, 0, 0, 0) })
            {
                var (source, bounds) = CanvasGridSourceTests.RevealSource(kind);
                Apply(source, combination);
                source.Reveal = reveal;
                var lines = WriteCounted(source, bounds);
                var halo = WriteCounted(source.Glow, bounds);
                Assert.Equal((combination & 4) != 0 ? 2 * lines.Length : 0, halo.Length);
            }
        }

        foreach (var mask in new[] { 0, 3, 5, 12, 15 })
        {
            var source = CanvasGridSourceTests.FourSides(mask);
            Apply(source, combination);
            source.CornerRadius = 0.5;
            source.MinLineSpacing = 6;
            _ = WriteCounted(source, new Box(0, 0, 600, 400));
            _ = WriteCounted(source.Glow, new Box(0, 0, 600, 400));
        }
    }

    [Theory]
    [MemberData(nameof(Combinations))]
    public void The_step_grid_counts_what_it_writes_with_every_effect_combination(int combination)
    {
        var bounds = new Box(0, 0, 1600, 900);
        foreach (var sides in new[] { CanvasStepSides.Left | CanvasStepSides.Right, CanvasStepSides.All })
        {
            foreach (var reveal in new[] { GridReveal.Full, new GridReveal(GridRevealStyle.Sweep, 0.4f, 0, 0, 24) })
            {
                var source = new CanvasStepSource { Map = CanvasStepSourceTests.Map(sides), CellSize = 64, MinLineSpacing = 4, Reveal = reveal };
                Apply(source, combination);
                _ = WriteCounted(source, bounds);
                var halo = WriteCounted(source.Glow, bounds);
                Assert.Equal((combination & 4) != 0, halo.Length > 0);
            }
        }
    }

    [Fact]
    public void Dark_lights_and_no_effects_write_the_plain_grid()
    {
        var (plain, bounds) = CanvasGridSourceTests.RevealSource(1);
        var expected = WriteCounted(plain, bounds);
        var (dark, _) = CanvasGridSourceTests.RevealSource(1);
        var lights = new GridLights { Radius = 80f };
        lights.Set(0, new Box(100, 100, 50, 50), 0f);
        lights.SetPointer(20, 20, 0f);
        dark.Lights = lights;
        dark.Effects = GridEffects.None with { Glow = 0f, GlowPath = GridGlowPath.Halo };
        Assert.Equal(expected, WriteCounted(dark, bounds));

        var step = new CanvasStepSource { Map = CanvasStepSourceTests.Map(CanvasStepSides.All), CellSize = 64 };
        var stepBounds = new Box(0, 0, 1600, 900);
        var stepExpected = WriteCounted(step, stepBounds);
        step.Lights = lights;
        Assert.Equal(stepExpected, WriteCounted(step, stepBounds));
    }

    [Fact]
    public void The_weight_is_the_strength_inside_a_box_and_zero_at_the_radius()
    {
        var lights = new GridLights { Radius = 100f };
        lights.Set(0, new Box(100, 100, 50, 40), 0.8f);
        Assert.Equal(0.8f, lights.At(120, 120), 6);
        Assert.Equal(0.8f, lights.At(100, 140), 6);
        Assert.Equal(0f, lights.At(250, 120), 6);
        Assert.Equal(0f, lights.At(150 + 71, 140 + 71), 6);
        Assert.Equal(0.8 * GridLights.Falloff(0.5), lights.At(200, 120), 6);
    }

    [Fact]
    public void Two_boxes_saturate_and_never_pass_one()
    {
        var lights = new GridLights { Radius = 100f };
        lights.Set(0, new Box(0, 0, 100, 100), 1f);
        lights.Set(1, new Box(100, 0, 100, 100), 1f);
        Assert.Equal(1f, lights.At(100, 50), 6);
        var half = new GridLights { Radius = 100f };
        half.Set(0, new Box(0, 0, 100, 100), 0.5f);
        half.Set(1, new Box(0, 0, 100, 100), 0.5f);
        Assert.Equal(0.75f, half.At(50, 50), 6);
        half.SetPointer(50, 50, 1f);
        Assert.Equal(1f, half.At(50, 50), 6);
        for (var x = -50; x < 300; x += 7)
        {
            Assert.InRange(lights.At(x, 50), 0f, 1f);
        }
    }

    [Fact]
    public void A_lit_line_moves_toward_the_highlight_color_and_full_alpha()
    {
        var source = new CanvasGridSource { CellSize = 64, Alpha = 0.3f, Color = new RenderColor(0.2f, 0.2f, 0.6f, 1f) };
        var lights = new GridLights { Radius = 40f, Color = new RenderColor(1f, 1f, 1f, 1f) };
        lights.Set(0, new Box(0, 0, 70, 70), 1f);
        source.Lights = lights;
        var vertices = WriteCounted(source, new Box(0, 0, 640, 320));
        var near = vertices.First(v => v.X <= 2 && v.Y <= 2);
        var far = vertices.First(v => v.X >= 600 && v.Y >= 250);
        Assert.Equal(1f, near.Color.A, 3);
        Assert.Equal(1f, near.Color.R, 3);
        Assert.Equal(0.3f, far.Color.A, 3);
        Assert.Equal(0.2f * 0.3f, far.Color.R, 3);
    }

    [Fact]
    public void Straight_lines_split_into_whole_line_segments_that_do_not_move_with_the_light()
    {
        var source = new CanvasGridSource { CellSize = 64 };
        var bounds = new Box(0, 0, 640, 320);
        var plain = source.VertexCount(bounds);
        var lights = new GridLights { Radius = 40f };
        lights.Set(0, new Box(10, 10, 20, 20), 1f);
        source.Lights = lights;
        var lit = source.VertexCount(bounds);
        Assert.Equal(((10 * (320 / 8)) + (5 * (640 / 8))) * 6, lit);
        Assert.Equal(15 * 6, plain);
        lights.Set(0, new Box(400, 200, 20, 20), 1f);
        source.Lights = lights;
        Assert.Equal(lit, source.VertexCount(bounds));
    }

    [Fact]
    public void Density_dims_packed_columns_to_the_floor_and_the_corner_takes_the_smaller_spacing()
    {
        var left = new CanvasWarp();
        left.Layout(seam: 120, direction: -1, zoneWidth: 120, extension: 480, edgeScale: 0.05, slope: 0.3, center: 200);
        var top = new CanvasWarp();
        top.Layout(seam: 48, direction: -1, zoneWidth: 48, extension: 200, edgeScale: 0.15, slope: 0.3, center: 300);
        var source = new CanvasGridSource
        {
            CellSize = 16,
            Left = left,
            Top = top,
            Color = new RenderColor(1f, 1f, 1f, 1f),
            Effects = GridEffects.None with { Density = true, DensitySpacing = 16f, DensityFloor = 0.15f },
        };
        var vertices = WriteCounted(source, new Box(0, 0, 600, 400));
        var flat = vertices.Where(v => v.X > 300 && v.X < 500 && v.Y > 150 && v.Y < 350).ToArray();
        Assert.NotEmpty(flat);
        Assert.All(flat, v => Assert.Equal(1f, v.Color.A, 3));
        var packed = vertices.Where(v => v.X < 6 && v.Y > 150 && v.Y < 250).ToArray();
        Assert.NotEmpty(packed);
        Assert.Contains(packed, v => Math.Abs(v.Color.A - 0.15f) < 1e-3);
        var corner = vertices.Where(v => v.X < 40 && v.Y < 12).ToArray();
        Assert.NotEmpty(corner);
        Assert.All(corner, v => Assert.True(v.Color.A < 0.9f, $"a corner vertex at ({v.X},{v.Y}) keeps alpha {v.Color.A}"));
    }

    [Fact]
    public void Density_on_a_step_wall_follows_the_wall_fan()
    {
        var map = CanvasStepSourceTests.Map(CanvasStepSides.All);
        var dense = new CanvasStepSource
        {
            Map = map,
            CellSize = 16,
            Color = new RenderColor(1f, 1f, 1f, 1f),
            ShelfLines = false,
            DesktopLines = false,
            Effects = GridEffects.None with { Density = true, DensitySpacing = 40f, DensityFloor = 0.1f },
        };
        var vertices = WriteCounted(dense, new Box(0, 0, 1600, 900));
        CanvasStepSurfaceSource.Corners(map, CanvasStepSides.Left, out var inner0, out _, out var outer0, out _);
        var innerSpacing = 16 * map.Zoom;
        var fan = (map.Inner.Height) / map.Outline.Height;
        var innerAlpha = (float)Math.Clamp(innerSpacing / 40.0, 0.1, 1.0);
        var outerAlpha = (float)Math.Clamp(innerSpacing * fan / 40.0, 0.1, 1.0);
        Assert.Contains(vertices, v => Math.Abs(v.X - inner0.X) < 0.5 && Math.Abs(v.Color.A - innerAlpha) < 1e-3);
        Assert.Contains(vertices, v => Math.Abs(v.X - outer0.X) < 0.5 && Math.Abs(v.Color.A - outerAlpha) < 1e-3);
        Assert.True(outerAlpha > innerAlpha, $"the wall widens toward the shelf: {innerAlpha} to {outerAlpha}");
    }

    [Fact]
    public void The_halo_sums_within_a_tenth_of_the_gaussian_across_a_line()
    {
        var radius = 6f;
        var source = new CanvasGridSource
        {
            CellSize = 64,
            Color = new RenderColor(1f, 1f, 1f, 1f),
            Effects = GridEffects.None with { Glow = 1f, GlowRadius = radius, GlowPath = GridGlowPath.Halo },
        };
        var bounds = new Box(0, 0, 640, 320);
        Assert.Equal(15 * 6, WriteCounted(source, bounds).Length);
        var vertices = WriteCounted(source.Glow, bounds);
        Assert.Equal(15 * 12, vertices.Length);
        var column = vertices.AsSpan(0, 12).ToArray();
        Assert.All(column, v => Assert.True(v.Color.A >= Math.Max(v.Color.R, Math.Max(v.Color.G, v.Color.B)), "the halo is valid premultiplied"));
        var inner = column.Where(v => v.Color.R > 0).Select(v => v.X).ToArray();
        var outer = column.Where(v => v.Color.R == 0).Select(v => v.X).ToArray();
        var width = outer.Max() - (inner.Max());
        var ramp = 2 * (width / 2f);
        var gaussian = 0.0;
        for (var d = -200.0; d <= 200.0; d += 0.01)
        {
            gaussian += Math.Exp(-(d / radius) * (d / radius)) * 0.01;
        }

        Assert.InRange(ramp / gaussian, 0.9, 1.1);
    }
}
