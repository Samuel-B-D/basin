using Basin.Effects;
using Xunit;

namespace Basin.Tests;

public sealed class GridPinchTests
{
    [Theory]
    [InlineData(CanvasStepSides.Left)]
    [InlineData(CanvasStepSides.Right)]
    [InlineData(CanvasStepSides.Top)]
    [InlineData(CanvasStepSides.Bottom)]
    public void The_bulge_round_trips_and_is_zero_at_both_wall_edges(CanvasStepSides side)
    {
        var map = CanvasStepSourceTests.Map(CanvasStepSides.All);
        Assert.True(map.SetBulge(side, 300, 0.6));
        Assert.False(map.SetBulge(side, 300, 0.6));
        Assert.True(map.SetBulge(side, 300, 5));
        Assert.Equal(CanvasStepMap.MaxBulge, map.BulgeDepth(side));
        Assert.Equal(0.0, map.BulgeAt(side, 0.0));
        Assert.Equal(0.0, map.BulgeAt(side, 1.0));
        Assert.Equal(CanvasStepMap.MaxBulge, map.BulgeAt(side, 0.5), 9);
        var vertical = side is CanvasStepSides.Left or CanvasStepSides.Right;
        foreach (var t in new[] { 0.0, 0.1, 0.5, 0.8, 1.0 })
        {
            var (start, length) = map.WallSpan(side, t);
            foreach (var along in new[] { 0.0, 0.05, 0.3, 0.5, 0.77, 1.0 })
            {
                var position = start + (along * length);
                var (x, y) = vertical ? (map.Outline.X - 10.0, position) : (position, map.Outline.Y - 10.0);
                var (bx, by) = map.Bend(side, x, y, t);
                var (ux, uy) = map.Unbend(side, bx, by, t);
                Assert.Equal(x, ux, 3);
                Assert.Equal(y, uy, 3);
                if (t is 0.0 or 1.0 || along is 0.0 or 1.0)
                {
                    Assert.Equal((x, y), (bx, by));
                }
            }
        }

        for (var along = 0.0; along < 1.0; along += 0.01)
        {
            var (start, length) = map.WallSpan(side, 0.5);
            var here = map.Bend(side, start + (along * length), start + (along * length), 0.5);
            var next = map.Bend(side, start + ((along + 0.01) * length), start + ((along + 0.01) * length), 0.5);
            Assert.True(vertical ? next.Y > here.Y : next.X > here.X, $"the bend stays monotone at {along:F2}");
        }
    }

    [Fact]
    public void A_bulged_wall_keeps_its_edges_on_both_planes()
    {
        var map = CanvasStepSourceTests.Map(CanvasStepSides.All);
        map.Bulges = true;
        map.SetBulge(CanvasStepSides.Left, map.CenterY - 200, 0.7);
        map.SetBulge(CanvasStepSides.Top, map.CenterX + 300, 0.7);
        var walls = new CanvasStepSurfaceSource(CanvasStepSurface.Walls) { Map = map };
        var bounds = new Box(0, 0, 1600, 900);
        var vertices = new MeshVertex[walls.VertexCount(bounds)];
        walls.WriteVertices(bounds, vertices);
        var outline = map.Outline;
        var inner = map.Inner;
        var left = vertices.Where(v => v.X <= outline.X + 0.01 && v.X >= inner.X - 0.01).ToArray();
        Assert.NotEmpty(left);
        foreach (var vertex in left.Where(v => Math.Abs(v.X - outline.X) < 0.01 || Math.Abs(v.X - inner.X) < 0.01))
        {
            var edge = Math.Abs(vertex.X - outline.X) < 0.01 ? outline : inner;
            Assert.InRange(vertex.Y, edge.Y - 0.01, edge.Bottom + 0.01);
        }

        Assert.Contains(left, v => v.X < outline.X - 1 && v.X > inner.X + 1 && v.Y < map.CenterY - 50);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_bulging_step_map_counts_what_it_writes_and_the_count_does_not_follow_the_depth(bool textured)
    {
        var map = CanvasStepSourceTests.Map(CanvasStepSides.All);
        map.Bulges = true;
        var bounds = new Box(0, 0, 1600, 900);
        var lines = new CanvasStepSource { Map = map, CellSize = 64, MinLineSpacing = 4 };
        var walls = new CanvasStepSurfaceSource(CanvasStepSurface.Walls)
        {
            Map = map,
            TextureWidth = textured ? 64 : 0,
            TextureHeight = textured ? 64 : 0,
            TextureScale = 0.5,
        };
        var lineCount = -1;
        var wallCount = -1;
        foreach (var depth in new[] { 0.0, 0.3, 0.9 })
        {
            map.SetBulge(CanvasStepSides.Left, 300, depth);
            map.SetBulge(CanvasStepSides.Top, 900, depth);
            foreach (Basin.Scene.IMeshSource source in new Basin.Scene.IMeshSource[] { lines, walls })
            {
                var count = source.VertexCount(bounds);
                var vertices = new MeshVertex[count + 1];
                var sentinel = new MeshVertex(-7, -7, -7, -7, default);
                vertices[count] = sentinel;
                source.WriteVertices(bounds, vertices.AsSpan(0, count));
                Assert.Equal(sentinel, vertices[count]);
                Assert.DoesNotContain(vertices.AsSpan(0, count).ToArray(), v => !float.IsFinite(v.X) || !float.IsFinite(v.Y));
            }

            var nowLines = lines.VertexCount(bounds);
            var nowWalls = walls.VertexCount(bounds);
            Assert.True(lineCount < 0 || lineCount == nowLines, "the line count follows the depth");
            Assert.True(wallCount < 0 || wallCount == nowWalls, "the wall count follows the depth");
            lineCount = nowLines;
            wallCount = nowWalls;
        }
    }

    [Fact]
    public void The_step_glow_lands_on_the_bent_cross_lines()
    {
        var map = CanvasStepSourceTests.Map(CanvasStepSides.Left);
        map.Bulges = true;
        map.SetBulge(CanvasStepSides.Left, map.Outline.Y + (map.Outline.Height * 0.7), 0.6);
        var lines = new CanvasStepSource
        {
            Map = map,
            CellSize = 64,
            Color = new RenderColor(0f, 0f, 1f, 1f),
            ShelfLines = false,
            DesktopLines = false,
            Effects = GridEffects.None with { Glow = 1f, GlowRadius = 2f },
        };
        var bounds = new Box(0, 0, 1600, 900);
        var values = new PixelShaderUniformValue[GridStepGlowShader.UniformCount];
        GridStepGlowShader.Pack(lines, bounds, values);
        var field = new GridStepGlowField(values);
        var outline = map.Outline;
        var inner = map.Inner;
        var t = 0.375;
        var x = outline.X - ((outline.X - inner.X) * t);
        var start = outline.Y;
        var screen = double.NaN;
        for (var canvas = -4096L; canvas <= 4096; canvas += 64)
        {
            var candidate = Math.Round(map.CenterY + ((canvas - map.CenterY) * map.Zoom));
            if (double.IsNaN(screen) || Math.Abs(((candidate - start) / outline.Height) - 0.35) < Math.Abs(((screen - start) / outline.Height) - 0.35))
            {
                screen = candidate;
            }
        }

        var along = (screen - start) / outline.Height;
        var straightY = ((1 - t) * screen) + (t * (inner.Y + (along * inner.Height)));
        var (_, bentY) = map.Bend(CanvasStepSides.Left, x, straightY, t);
        Assert.True(Math.Abs(bentY - straightY) > 5, $"the bulge moves the line: {straightY} to {bentY}");
        var onBent = field.At(x, bentY).B;
        var onStraight = field.At(x, straightY).B;
        Assert.True(onBent > 0.5, $"the glow on the bent line is {onBent}");
        Assert.True(onStraight < onBent, $"the glow stays on the bent line, {onStraight} off it");
    }

    [Fact]
    public void The_fastest_candidate_wins_and_the_dragged_window_beats_any_speed()
    {
        Assert.True(TinyComp.GridPinch.Score(900, 1.0) > TinyComp.GridPinch.Score(700, 1.0));
        Assert.True(TinyComp.GridPinch.Score(700, 1.0) > TinyComp.GridPinch.Score(900, 0.5));
        Assert.Equal(0.0, TinyComp.GridPinch.Score(5000, 0.0));
        Assert.True(TinyComp.GridPinch.Score(double.PositiveInfinity, 0.01) > TinyComp.GridPinch.Score(1e9, 1.0));
        Assert.Equal(0.0, TinyComp.GridPinch.Score(double.PositiveInfinity, 0.0));
    }
}
