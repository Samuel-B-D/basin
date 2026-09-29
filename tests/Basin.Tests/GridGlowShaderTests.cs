using System.Runtime.CompilerServices;
using Basin.Effects;
using Xunit;

namespace Basin.Tests;

public sealed class GridGlowShaderTests
{
    private static string ShaderPath([CallerFilePath] string path = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", "..", "src", "Basin.Effects", "shaders", "grid_glow.frag"));

    [Fact]
    public void The_uniform_block_fits_the_vulkan_capacity()
    {
        var bytes = 16;
        foreach (var uniform in GridGlowShader.Uniforms)
        {
            Assert.Equal(PixelShaderUniformType.Float4, uniform.Type);
            bytes += 16;
        }

        Assert.Equal(GridGlowShader.UniformCount, GridGlowShader.Uniforms.Length);
        Assert.True(bytes <= 512, $"the block is {bytes} bytes");
        Assert.Equal(496, bytes);
        Assert.Equal(GridStepGlowShader.UniformCount, GridStepGlowShader.Uniforms.Length);
        Assert.Equal(16 + (16 * GridStepGlowShader.UniformCount), 480);
        Assert.All(GridStepGlowShader.Uniforms, uniform => Assert.Equal(PixelShaderUniformType.Float4, uniform.Type));
    }

    [Theory]
    [InlineData("grid_glow.frag", false)]
    [InlineData("grid_step_glow.frag", true)]
    public void The_checked_in_fragment_carries_the_shared_body_and_declares_the_uniforms_in_order(string file, bool step)
    {
        var frag = File.ReadAllText(Path.Combine(Path.GetDirectoryName(ShaderPath())!, file));
        var body = step ? GridStepGlowShader.Source.Glsl! : GridGlowShader.Source.Glsl!;
        static string Dense(string text) => string.Join('\n', text.Split('\n').Where(line => line.Trim().Length > 0));
        var shared = Dense(body[..body.IndexOf("vec4 basin_pixel", StringComparison.Ordinal)]);
        Assert.Contains(shared, Dense(frag), StringComparison.Ordinal);
        var at = frag.IndexOf("float u_alpha;", StringComparison.Ordinal);
        foreach (var uniform in step ? GridStepGlowShader.Uniforms : GridGlowShader.Uniforms)
        {
            var next = frag.IndexOf($"vec4 {uniform.Name};", at, StringComparison.Ordinal);
            Assert.True(next > at, $"{uniform.Name} is declared in order");
            at = next;
        }
    }

    internal static CanvasGridSource Grid(int mask, double radius, bool taper, bool terrace = false, bool separable = false)
    {
        CanvasWarp? Warp(int bit, int seam, int direction, int zone, int extension, double center)
        {
            if ((mask & bit) == 0)
            {
                return null;
            }

            var warp = new CanvasWarp(direction);
            if (terrace)
            {
                warp.LayoutTerrace(seam, direction, zone, zone / 2, 0.35, CanvasWarp.TerraceExponent, 0.3, center);
            }
            else
            {
                warp.Layout(seam, direction, zone, extension, 0.15, 0.3, center);
            }

            return warp;
        }

        return new CanvasGridSource
        {
            CellSize = 24,
            Left = Warp(1, 120, -1, 120, 500, 400),
            Right = Warp(2, 880, 1, 120, 500, 400),
            Top = Warp(4, 96, -1, 96, 400, 500),
            Bottom = Warp(8, 704, 1, 96, 400, 500),
            CornerRadius = radius,
            CornerTaper = taper,
            Separable = separable,
        };
    }

    internal static GridGlowField Field(CanvasGridSource grid, in Box bounds)
    {
        var values = new PixelShaderUniformValue[GridGlowShader.UniformCount];
        GridGlowShader.Pack(grid, bounds, values);
        return new GridGlowField(values);
    }

    private static CanvasWarpTransform MapOf(CanvasGridSource grid) => new()
    {
        Left = grid.Left,
        Right = grid.Right,
        Top = grid.Top,
        Bottom = grid.Bottom,
        CornerRadius = grid.CornerRadius,
        CornerTaper = grid.CornerTaper,
        Separable = grid.Separable,
    };

    public static TheoryData<int, double, bool, bool, bool> Maps() => new()
    {
        { 3, 1.0, false, false, false },
        { 15, 1.0, false, false, false },
        { 15, 0.0, false, false, false },
        { 15, 0.5, false, false, false },
        { 15, 1.0, true, false, false },
        { 15, 0.6, true, false, false },
        { 5, 1.0, false, false, false },
        { 10, 0.3, false, false, false },
        { 15, 1.0, false, true, false },
        { 15, 1.0, false, true, true },
        { 15, 1.0, false, false, true },
    };

    [Theory]
    [MemberData(nameof(Maps))]
    public void The_field_inverts_the_warp_like_the_transform(int mask, double radius, bool taper, bool terrace, bool separable)
    {
        var grid = Grid(mask, radius, taper, terrace, separable);
        var field = Field(grid, new Box(0, 0, 1000, 800));
        var map = MapOf(grid);
        var worstFlat = 0.0;
        var worstCorner = 0.0;
        for (var y = 1.5; y < 800; y += 13)
        {
            for (var x = 1.5; x < 1000; x += 11)
            {
                var expected = map.ToCanvasPoint(x, y);
                var actual = field.Unwarp(x, y);
                var error = Math.Max(Math.Abs(expected.X - actual.X), Math.Abs(expected.Y - actual.Y));
                var inSide = (grid.Left?.ContainsScreen(x) ?? false) || (grid.Right?.ContainsScreen(x) ?? false);
                var inEnd = (grid.Top?.ContainsScreen(y) ?? false) || (grid.Bottom?.ContainsScreen(y) ?? false);
                if (inSide && inEnd)
                {
                    worstCorner = Math.Max(worstCorner, error);
                }
                else
                {
                    worstFlat = Math.Max(worstFlat, error);
                }
            }
        }

        Assert.True(worstFlat <= 0.05, $"flat and zones: {worstFlat:F4} canvas px");
        Assert.True(worstCorner <= (taper ? 0.5 : 0.05), $"corners: {worstCorner:F4} canvas px");
    }

    [Fact]
    public void The_field_is_dark_off_the_lines_and_brightest_on_them()
    {
        var grid = new CanvasGridSource
        {
            CellSize = 32,
            Color = new RenderColor(0.2f, 0.3f, 0.9f, 1f),
            Effects = GridEffects.None with { Glow = 1f, GlowRadius = 3f },
        };
        var field = Field(grid, new Box(0, 0, 320, 240));
        var on = field.At(64.5, 100.5);
        var near = field.At(66.5, 100.5);
        var far = field.At(80.5, 80.5);
        Assert.Equal(0.9, on.B, 3);
        Assert.True(near.B < on.B && near.B > 0.1, $"near {near.B}");
        Assert.True(far.B < 1e-6, $"far {far.B}");
    }

    public static TheoryData<string> ShaderRows() => new() { "gl", "vulkan", "skia", "skia-gl", "skia-vulkan", "skia-graphite" };

    internal static CanvasGridSource Scene(bool lit, GridReveal reveal)
    {
        var left = new CanvasWarp();
        left.Layout(24, -1, 24, 80, 0.12, -0.3, 60);
        var right = new CanvasWarp(1);
        right.Layout(136, 1, 24, 80, 0.12, 0.4, 60);
        var top = new CanvasWarp();
        top.Layout(18, -1, 18, 60, 0.15, 0.25, 80);
        var lights = new GridLights { Radius = 30f };
        if (lit)
        {
            lights.Set(0, new Box(60, 44, 34, 26), 1f);
            lights.Set(4, new Box(110, 80, 30, 24), 0.35f);
            lights.SetPointer(20, 100, 0.8f);
        }

        return new CanvasGridSource
        {
            Left = left,
            Right = right,
            Top = top,
            CellSize = 16,
            Alpha = 0.7f,
            Color = new RenderColor(0.16f, 0.21f, 0.75f, 1f),
            Lights = lights,
            Reveal = reveal,
            CornerRadius = 0.8,
            Effects = GridEffects.None with { Density = true, DensitySpacing = 10f, Glow = 0.6f, GlowRadius = 3f, GlowPath = GridGlowPath.Shader },
        };
    }

    [Theory]
    [MemberData(nameof(ShaderRows))]
    public void The_rendered_glow_matches_the_field(string renderer)
    {
        CompositorTestHost.SkipUnlessRunnable(renderer);
        using var host = new CompositorTestHost(renderer: renderer);
        using var shader = host.Renderer.CompilePixelShader(GridGlowShader.Source, GridGlowShader.Uniforms);
        Assert.NotNull(shader);
        var bounds = new Box(0, 0, host.Target.Width, host.Target.Height);
        foreach (var reveal in new[] { GridReveal.Full, new GridReveal(GridRevealStyle.Ripple, 0.5f, 80, 60, 20) })
        {
            var grid = Scene(lit: true, reveal);
            var values = new PixelShaderUniformValue[GridGlowShader.UniformCount];
            GridGlowShader.Pack(grid, bounds, values);
            shader!.SetUniforms(values);
            var node = new Basin.Scene.SceneShader(host.Scene.Root) { Shader = shader, Bounds = bounds };
            host.RenderFrame();
            var rgba = Basin.Diagnostics.BufferCapture.ReadRgba(host.Target);
            var field = new GridGlowField(values);
            var worst = 0.0;
            var at = (0, 0);
            var brightest = 0;
            for (var y = 0; y < bounds.Height; y++)
            {
                for (var x = 0; x < bounds.Width; x++)
                {
                    var (r, g, b) = field.At(x + 0.5, y + 0.5);
                    var i = ((y * bounds.Width) + x) * 4;
                    brightest = Math.Max(brightest, Math.Max(rgba[i], Math.Max(rgba[i + 1], rgba[i + 2])));
                    var error = Math.Max(
                        Math.Abs(rgba[i] - (Math.Clamp(r, 0, 1) * 255)),
                        Math.Max(Math.Abs(rgba[i + 1] - (Math.Clamp(g, 0, 1) * 255)), Math.Abs(rgba[i + 2] - (Math.Clamp(b, 0, 1) * 255))));
                    if (error > worst)
                    {
                        worst = error;
                        at = (x, y);
                    }
                }
            }

            node.Destroy();
            Assert.True(brightest > 120, $"{renderer} {reveal.Style}: the glow is too faint to judge ({brightest})");
            Assert.True(worst <= 2.5, $"{renderer} {reveal.Style}: {worst:F2} LSB at {at}");
        }
    }

    internal static CanvasStepSource StepScene(CanvasStepSides sides, bool lit)
    {
        var output = new Box(0, 0, 160, 120);
        var full = new TinyComp.OverviewSide[4];
        full[0] = (sides & CanvasStepSides.Left) != 0 ? TinyComp.OverviewLayout.StepFull(0, 0, 80, -1, 0.6, 0.06, 160) : default;
        full[1] = (sides & CanvasStepSides.Right) != 0 ? TinyComp.OverviewLayout.StepFull(160, 160, 80, 1, 0.6, 0.06, 160) : default;
        full[2] = (sides & CanvasStepSides.Top) != 0 ? TinyComp.OverviewLayout.StepFull(0, 0, 60, -1, 0.6, 0.06, 120) : default;
        full[3] = (sides & CanvasStepSides.Bottom) != 0 ? TinyComp.OverviewLayout.StepFull(120, 120, 60, 1, 0.6, 0.06, 120) : default;
        var map = new CanvasStepMap();
        _ = TinyComp.OverviewLayout.LayoutStep(map, output, output, full, 1.0, 0.6, 0.3);
        var lights = new GridLights { Radius = 26f };
        if (lit)
        {
            lights.Set(0, new Box(50, 40, 30, 22), 1f);
            lights.SetPointer(140, 20, 0.7f);
        }

        return new CanvasStepSource
        {
            Map = map,
            CellSize = 16,
            MinLineSpacing = 4,
            Color = new RenderColor(0.16f, 0.21f, 0.75f, 1f),
            Lights = lights,
            Effects = GridEffects.None with { Density = true, DensitySpacing = 10f, Glow = 0.6f, GlowRadius = 3f, GlowPath = GridGlowPath.Shader },
        };
    }

    [Theory]
    [InlineData(CanvasStepSides.All)]
    [InlineData(CanvasStepSides.Horizontal)]
    [InlineData(CanvasStepSides.Left | CanvasStepSides.Top)]
    public void The_step_field_inverts_the_step_map(CanvasStepSides sides)
    {
        var grid = StepScene(sides, lit: false);
        var values = new PixelShaderUniformValue[GridStepGlowShader.UniformCount];
        GridStepGlowShader.Pack(grid, new Box(0, 0, 160, 120), values);
        var field = new GridStepGlowField(values);
        var map = grid.Map!;
        var planes = 0;
        for (var y = 0.5; y < 120; y += 1.0)
        {
            for (var x = 0.5; x < 160; x += 1.0)
            {
                var expected = map.TryToCanvas(x, y, out var plane, out var ex, out var ey);
                var actual = field.TryToCanvas(x, y, out var shelf, out var ax, out var ay);
                Assert.Equal(expected, actual);
                if (!expected)
                {
                    continue;
                }

                planes++;
                Assert.Equal(plane == CanvasStepPlane.Shelf, shelf);
                Assert.True(Math.Abs(ex - ax) <= 0.05 && Math.Abs(ey - ay) <= 0.05, $"({x},{y}): ({ex},{ey}) against ({ax},{ay})");
            }
        }

        Assert.True(planes > 1000);
    }

    [Theory]
    [MemberData(nameof(ShaderRows))]
    public void The_rendered_step_glow_matches_the_field(string renderer)
    {
        CompositorTestHost.SkipUnlessRunnable(renderer);
        using var host = new CompositorTestHost(renderer: renderer);
        using var shader = host.Renderer.CompilePixelShader(GridStepGlowShader.Source, GridStepGlowShader.Uniforms);
        Assert.NotNull(shader);
        var bounds = new Box(0, 0, host.Target.Width, host.Target.Height);
        var grid = StepScene(CanvasStepSides.All, lit: true);
        var values = new PixelShaderUniformValue[GridStepGlowShader.UniformCount];
        GridStepGlowShader.Pack(grid, bounds, values);
        shader!.SetUniforms(values);
        var node = new Basin.Scene.SceneShader(host.Scene.Root) { Shader = shader, Bounds = bounds };
        host.RenderFrame();
        var rgba = Basin.Diagnostics.BufferCapture.ReadRgba(host.Target);
        var field = new GridStepGlowField(values);
        var worst = 0.0;
        var at = (0, 0);
        var brightest = 0;
        (double X, double Y)[] nudges = [(0, 0), (0.25, 0), (-0.25, 0), (0, 0.25), (0, -0.25)];
        for (var y = 0; y < bounds.Height; y++)
        {
            for (var x = 0; x < bounds.Width; x++)
            {
                var i = ((y * bounds.Width) + x) * 4;
                brightest = Math.Max(brightest, Math.Max(rgba[i], Math.Max(rgba[i + 1], rgba[i + 2])));
                var best = double.PositiveInfinity;
                foreach (var (nx, ny) in nudges)
                {
                    var (r, g, b) = field.At(x + 0.5 + nx, y + 0.5 + ny);
                    best = Math.Min(best, Math.Max(
                        Math.Abs(rgba[i] - (Math.Clamp(r, 0, 1) * 255)),
                        Math.Max(Math.Abs(rgba[i + 1] - (Math.Clamp(g, 0, 1) * 255)), Math.Abs(rgba[i + 2] - (Math.Clamp(b, 0, 1) * 255)))));
                }

                if (best > worst)
                {
                    worst = best;
                    at = (x, y);
                }
            }
        }

        node.Destroy();
        Assert.True(brightest > 120, $"{renderer}: the glow is too faint to judge ({brightest})");
        Assert.True(worst <= 2.5, $"{renderer}: {worst:F2} LSB at {at}");
    }
}
