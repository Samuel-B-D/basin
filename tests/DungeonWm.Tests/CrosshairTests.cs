using System.Globalization;
using System.Numerics;
using Basin;
using Basin.Diagnostics;
using Xunit;
using static DungeonWm.Tests.ProcessTests;

namespace DungeonWm.Tests;

public sealed class CrosshairTests
{
    private const int Crop = 48;

    public static TheoryData<string> Rows => new() { "gl", "vulkan" };

    [Fact]
    public void Golden_every_reticle_shape_at_scale_one_and_two()
    {
        var color = new Vector4(1, 1, 1, 0.9f);
        var shapes = Reticle.Shapes.Length;
        var cell = 48;
        var width = cell * shapes;
        var height = cell * 4;
        var sheet = new byte[width * height * 4];
        for (var row = 0; row < 4; row++)
        {
            var scale = row < 2 ? 1.0 : 2.0;
            var dim = row % 2 == 1;
            for (var shape = 0; shape < shapes; shape++)
            {
                var image = Reticle.Paint(shape, dim, scale, 24, color);
                Assert.Equal(image.Width / 2, image.HotspotX);
                Assert.Equal(image.Height / 2, image.HotspotY);
                var left = (shape * cell) + ((cell - image.Width) / 2);
                var top = (row * cell) + ((cell - image.Height) / 2);
                for (var y = 0; y < image.Height; y++)
                {
                    for (var x = 0; x < image.Width; x++)
                    {
                        var from = ((y * image.Width) + x) * 4;
                        var to = (((top + y) * width) + left + x) * 4;
                        var alpha = image.Pixels[from + 3] / 255f;
                        var under = 64 * (1 - alpha);
                        sheet[to] = (byte)Math.Round(image.Pixels[from + 2] + under);
                        sheet[to + 1] = (byte)Math.Round(image.Pixels[from + 1] + under);
                        sheet[to + 2] = (byte)Math.Round(image.Pixels[from] + under);
                        sheet[to + 3] = 255;
                    }
                }
            }
        }

        for (var i = 0; i < sheet.Length; i += 4)
        {
            if (sheet[i + 3] == 0)
            {
                sheet[i] = sheet[i + 1] = sheet[i + 2] = 64;
                sheet[i + 3] = 255;
            }
        }

        Compare(sheet, width, height, "dungeon-wm-reticles", tolerance: 0);
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public async Task Golden_the_reticle_over_the_world_through_shotraw(string row)
    {
        var (compositor, client) = Prerequisites(row, "ssdwin");
        var directory = Directory.CreateTempSubdirectory("dungeon-wm-reticle-");
        try
        {
            var file = Path.Combine(directory.FullName, "dungeon-wm.toml");
            File.WriteAllText(file, Config(reach: 4));
            using var session = new Session(compositor, row, client, "--config", file);
            Assert.NotNull(await session.WaitForAsync(line => line.StartsWith("HUNG ssdwin", StringComparison.Ordinal)));
            var crops = new List<byte[]>();

            await session.SendAsync("face ssdwin");
            crops.Add(await Shot(session, directory, "use"));

            var (x, y) = await Located(session, "frame");
            await session.SendAsync(Aim(x, y));
            crops.Add(await Shot(session, directory, "grab"));

            await session.SendAsync("face ssdwin");
            (x, y) = await Located(session, "bottom-right");
            await session.SendAsync(Aim(x, y));
            crops.Add(await Shot(session, directory, "resize-se"));

            await session.SendAsync("face ssdwin");
            await session.SendAsync("look 0 -60");
            crops.Add(await Shot(session, directory, "idle"));

            File.WriteAllText(file, Config(reach: 0.3));
            await session.SendAsync("reload");
            await session.SendAsync("face ssdwin");
            crops.Add(await Shot(session, directory, "use-dim"));

            File.WriteAllText(file, Config(reach: 4));
            await session.SendAsync("reload");
            await session.SendAsync("face ssdwin");
            Assert.NotNull(await session.WaitForAsync(line => line == "RETICLE use", fresh: false));
            await session.SendAsync("button 272 true");
            crops.Add(await Shot(session, directory, "dragging"));
            await session.SendAsync("button 272 false");

            var width = Crop * crops.Count;
            var sheet = new byte[width * Crop * 4];
            for (var i = 0; i < crops.Count; i++)
            {
                for (var line = 0; line < Crop; line++)
                {
                    Array.Copy(crops[i], line * Crop * 4, sheet, ((line * width) + (i * Crop)) * 4, Crop * 4);
                }
            }

            Compare(sheet, width, Crop, $"dungeon-wm-reticle-world-{row}", tolerance: 2, allowed: crops.Count * Crop * Crop / 100);
            await session.Quit();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public async Task E_on_the_window_under_the_crosshair_frees_the_cursor_where_it_was(string row)
    {
        var (compositor, client) = Prerequisites(row, "ssdwin");
        using var session = new Session(compositor, row, client);
        Assert.NotNull(await session.WaitForAsync(line => line == "MODE crosshair"));
        Assert.NotNull(await session.WaitForAsync(line => line.StartsWith("HUNG ssdwin", StringComparison.Ordinal)));
        Assert.DoesNotContain(session.Lines, line => line == "FOCUS ssdwin");

        await session.SendAsync("face ssdwin");
        Assert.NotNull(await session.WaitForAsync(line => line == "RETICLE use", poke: "mode"));
        var before = await Steady(session);
        await session.SendAsync("key 18 true");
        await session.SendAsync("key 18 false");
        Assert.NotNull(await session.WaitForAsync(line => line == "USE ssdwin"));
        Assert.NotNull(await session.WaitForAsync(line => line == "MODE window"));
        var pointer = await session.WaitForAsync(line => line.StartsWith("POINTER ", StringComparison.Ordinal), poke: "where", fresh: true);
        Assert.Equal("POINTER 640.0 360.0 looking=no mode=window", pointer);
        var after = await Steady(session);
        Assert.Equal(before, after);

        await session.SendAsync("key 30 true");
        await session.SendAsync("key 30 false");
        Assert.NotNull(await session.WaitForAsync(line => line == "KEY 30 1"));
        Assert.DoesNotContain(session.Lines, line => line.StartsWith("KEY 18 ", StringComparison.Ordinal));
        await session.Quit();
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public async Task A_click_on_geometry_or_the_release_binding_locks_the_pointer_again(string row)
    {
        var (compositor, client) = Prerequisites(row, "ssdwin");
        using var session = new Session(compositor, row, client);
        Assert.NotNull(await session.WaitForAsync(line => line.StartsWith("HUNG ssdwin", StringComparison.Ordinal)));
        await session.SendAsync("face ssdwin");
        await Use(session);

        await session.SendAsync("move 40 40");
        Assert.NotNull(await session.WaitForAsync(line => line.StartsWith("PICK geometry", StringComparison.Ordinal), poke: "where", fresh: true));
        await session.SendAsync("button 272 true");
        await session.SendAsync("button 272 false");
        Assert.NotNull(await session.WaitForAsync(line => line == "MODE crosshair", fresh: false));
        var pointer = await session.WaitForAsync(line => line.StartsWith("POINTER ", StringComparison.Ordinal), poke: "where", fresh: true);
        Assert.Equal("POINTER 640.0 360.0 looking=no mode=crosshair", pointer);

        await session.SendAsync("move 700 360");
        var turned = await session.WaitForAsync(line => line.StartsWith("CAMERA ", StringComparison.Ordinal), poke: "where", fresh: true);
        Assert.Contains("yaw=-12.0", turned!, StringComparison.Ordinal);

        await session.SendAsync("face ssdwin");
        await Use(session);
        await session.SendAsync("key 125 true");
        await session.SendAsync("key 18 true");
        await session.SendAsync("key 18 false");
        await session.SendAsync("key 125 false");
        Assert.NotNull(await session.WaitForAsync(line => line == "ACTION release"));
        var mode = await session.WaitForAsync(line => line.StartsWith("MODE crosshair ", StringComparison.Ordinal), poke: "mode", fresh: true);
        Assert.NotNull(mode);
        await session.Quit();
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public async Task Past_reach_the_reticle_is_dim_and_neither_e_nor_a_click_does_anything(string row)
    {
        var (compositor, client) = Prerequisites(row, "ssdwin");
        var directory = Directory.CreateTempSubdirectory("dungeon-wm-reach-");
        try
        {
            var file = Path.Combine(directory.FullName, "dungeon-wm.toml");
            File.WriteAllText(file, "[world]\nrooms = 1\n\n[camera]\nreach = 0.3\n");
            using var session = new Session(compositor, row, client, "--config", file);
            Assert.NotNull(await session.WaitForAsync(line => line.StartsWith("HUNG ssdwin", StringComparison.Ordinal)));
            await session.SendAsync("face ssdwin");
            Assert.NotNull(await session.WaitForAsync(line => line == "RETICLE use-dim", poke: "mode"));
            await session.SendAsync("key 18 true");
            await session.SendAsync("key 18 false");
            Assert.NotNull(await session.WaitForAsync(line => line == "USE none"));
            await session.SendAsync("button 272 true");
            await session.SendAsync("button 272 false");
            var mode = await session.WaitForAsync(line => line.StartsWith("MODE ", StringComparison.Ordinal) && line.Contains(" reticle=", StringComparison.Ordinal), poke: "mode", fresh: true);
            Assert.StartsWith("MODE crosshair reticle=use-dim", mode, StringComparison.Ordinal);
            Assert.DoesNotContain(session.Lines, line => line.StartsWith("PULL ", StringComparison.Ordinal) || line == "MODE window");
            await session.Quit();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public async Task A_crosshair_press_on_the_frame_pulls_and_on_a_corner_resizes_by_looking(string row)
    {
        var (compositor, client) = Prerequisites(row, "ssdwin");
        using var session = new Session(compositor, row, client);
        Assert.NotNull(await session.WaitForAsync(line => line.StartsWith("HUNG ssdwin", StringComparison.Ordinal)));

        await session.SendAsync("face ssdwin");
        var (x, y) = await Located(session, "bottom-right");
        await session.SendAsync(Aim(x, y));
        Assert.NotNull(await session.WaitForAsync(line => line == "RETICLE resize-se", poke: "mode"));
        await session.SendAsync("button 272 true");
        Assert.NotNull(await session.WaitForAsync(line => line == "RESIZE begin ssdwin bottomright"));
        Assert.NotNull(await session.WaitForAsync(line => line == "RETICLE dragging"));
        for (var step = 1; step <= 6; step++)
        {
            await session.SendAsync("move 646 364.5");
            await Task.Delay(60, TestContext.Current.CancellationToken);
        }

        await Task.Delay(300, TestContext.Current.CancellationToken);
        await session.SendAsync("button 272 false");
        var end = await session.WaitForAsync(line => line.StartsWith("RESIZE end ssdwin ", StringComparison.Ordinal));
        Assert.NotNull(end);
        var size = end!["RESIZE end ssdwin ".Length..].Split('x');
        Assert.True(int.Parse(size[0], CultureInfo.InvariantCulture) > 256, end);
        Assert.True(int.Parse(size[1], CultureInfo.InvariantCulture) > 256, end);

        await session.SendAsync("face ssdwin");
        (x, y) = await Located(session, "frame");
        await session.SendAsync(Aim(x, y));
        Assert.NotNull(await session.WaitForAsync(line => line == "RETICLE grab", poke: "mode"));
        await session.SendAsync("button 272 true");
        Assert.NotNull(await session.WaitForAsync(line => line == "PULL begin ssdwin"));
        await session.SendAsync("walk 0 -0.6");
        Assert.NotNull(await session.WaitForAsync(line => line == "DETACHED ssdwin"));
        await session.SendAsync("button 272 false");
        Assert.NotNull(await session.WaitForAsync(line => line.StartsWith("THROW ssdwin", StringComparison.Ordinal)));
        Assert.DoesNotContain(session.Lines, line => line == "MODE window");
        await session.Quit();
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public async Task A_held_window_turns_with_the_view_and_r_turns_it_in_three_dimensions(string row)
    {
        var (compositor, client) = Prerequisites(row, "ssdwin");
        using var session = new Session(compositor, row, client);
        Assert.NotNull(await session.WaitForAsync(line => line.StartsWith("HUNG ssdwin", StringComparison.Ordinal)));
        await session.SendAsync("face ssdwin");
        Assert.NotNull(await session.WaitForAsync(line => line == "RETICLE use", poke: "mode"));
        await session.SendAsync("button 272 true");
        await session.SendAsync("walk 0 -0.6");
        Assert.NotNull(await session.WaitForAsync(line => line == "DETACHED ssdwin"));

        await session.SendAsync("look 90 0");
        var (_, facing, _) = await SettledBody(session);
        Assert.True(facing.X > 0.97, $"the held window faces {facing} after the view turned");

        var camera = await Steady(session);
        await session.SendAsync("key 19 true");
        Assert.NotNull(await session.WaitForAsync(line => line == "ROTATE begin"));
        await session.SendAsync("move 640 460");
        (_, facing, var up) = await SettledBody(session);
        Assert.True(facing.Y < -0.2 && up.X > 0.2, $"the window faces {facing} with up {up} after R and a drag down");
        await session.SendAsync("key 19 false");
        Assert.NotNull(await session.WaitForAsync(line => line == "ROTATE end"));
        Assert.Equal(camera, await Steady(session));

        await session.SendAsync("turn 0 0 90");
        (_, _, up) = await SettledBody(session);
        Assert.True(Math.Abs(up.Y) < 0.5, $"a roll of 90 left up at {up}");

        await session.SendAsync("key 42 true");
        await session.SendAsync("key 19 true");
        await session.SendAsync("key 19 false");
        await session.SendAsync("key 42 false");
        Assert.NotNull(await session.WaitForAsync(line => line == "SQUARE ssdwin yaw=90"));
        (_, facing, up) = await SettledBody(session);
        Assert.True(facing.X > 0.97 && up.Y > 0.97, $"a square window faces {facing} with up {up}");
        await session.SendAsync("button 272 false");
        Assert.NotNull(await session.WaitForAsync(line => line.StartsWith("THROW ssdwin", StringComparison.Ordinal)));
        await session.Quit();
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public async Task A_takeover_and_a_keyboard_layer_free_the_cursor_and_the_mode_comes_back(string row)
    {
        var (compositor, client) = Prerequisites(row, "ssdwin");
        var panel = Path.Combine(Repository(), "scripts", "wlclients", "bin", "panel");
        Assert.SkipUnless(File.Exists(panel), "panel has not been built (make -C scripts/wlclients)");
        using var session = new Session(compositor, row, $"{client} & sleep 10; {panel} top 40 keyboard & sleep 2; kill $!");
        Assert.NotNull(await session.WaitForAsync(line => line.StartsWith("HUNG ssdwin", StringComparison.Ordinal)));

        var mark = session.Lines.Count;
        await session.SendAsync("takeover ssdwin");
        Assert.NotNull(await session.WaitForAsync(line => line.StartsWith("TAKEOVER ssdwin on ", StringComparison.Ordinal)));
        await After(session, mark, "MODE window");
        mark = session.Lines.Count;
        await session.SendAsync("takeover off");
        Assert.NotNull(await session.WaitForAsync(line => line == "TAKEOVER off ssdwin"));
        await After(session, mark, "MODE crosshair");
        var focus = await session.WaitForAsync(line => line.StartsWith("FOCUS ", StringComparison.Ordinal), poke: "where", fresh: true);
        Assert.StartsWith("FOCUS none", focus, StringComparison.Ordinal);

        mark = session.Lines.Count;
        Assert.NotNull(await session.WaitForAsync(line => line == "KEYBOARD layer panel"));
        await After(session, mark, "MODE window");
        mark = session.Lines.Count;
        Assert.NotNull(await session.WaitForAsync(line => line == "KEYBOARD none"));
        await After(session, mark, "MODE crosshair");
        await session.Quit();
    }

    private static async Task After(Session session, int mark, string expected)
    {
        for (var attempt = 0; attempt < 150; attempt++)
        {
            if (session.Lines.Skip(mark).Contains(expected))
            {
                return;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        Assert.Fail($"no '{expected}' after line {mark}");
    }

    private static async Task<(double Yaw, Vector3 Facing, Vector3 Up)> SettledBody(Session session)
    {
        (double Yaw, Vector3 Facing, Vector3 Up) body = default;
        string? previous = null;
        for (var attempt = 0; attempt < 30; attempt++)
        {
            var line = await session.WaitForAsync(l => l.StartsWith("BODY ssdwin ", StringComparison.Ordinal), poke: "physics-state", fresh: true);
            Assert.NotNull(line);
            var fields = line!.Split(' ');
            var orientation = string.Join(' ', fields.Where(field => field.StartsWith("yaw=", StringComparison.Ordinal) || field.StartsWith("facing=", StringComparison.Ordinal) || field.StartsWith("up=", StringComparison.Ordinal)));
            body = (Number(fields, "yaw=")[0], Vector(fields, "facing="), Vector(fields, "up="));
            if (orientation == previous)
            {
                break;
            }

            previous = orientation;
            await Task.Delay(200, TestContext.Current.CancellationToken);
        }

        return body;
    }

    private static double[] Number(string[] fields, string key) =>
        [.. fields.First(field => field.StartsWith(key, StringComparison.Ordinal))[key.Length..].Split(',').Select(value => double.Parse(value, CultureInfo.InvariantCulture))];

    private static Vector3 Vector(string[] fields, string key)
    {
        var values = Number(fields, key);
        return new Vector3((float)values[0], (float)values[1], (float)values[2]);
    }

    private static async Task<string?> Steady(Session session)
    {
        string? camera = null;
        for (var attempt = 0; attempt < 30; attempt++)
        {
            var previous = camera;
            camera = await session.WaitForAsync(line => line.StartsWith("CAMERA ", StringComparison.Ordinal), poke: "where", fresh: true);
            if (camera == previous)
            {
                break;
            }

            await Task.Delay(150, TestContext.Current.CancellationToken);
        }

        return camera;
    }

    private static string Config(double reach) =>
        string.Create(CultureInfo.InvariantCulture, $"[world]\nrooms = 1\ntorch_color = \"#000000\"\n\n[camera]\nreach = {reach}\n");

    private static string Aim(double x, double y) =>
        string.Create(CultureInfo.InvariantCulture, $"aim {x:F1} {y:F1}");

    private static async Task<(double X, double Y)> Located(Session session, string part)
    {
        string? line = null;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var previous = line;
            line = await session.WaitForAsync(l => l.StartsWith($"LOCATE ssdwin {part} ", StringComparison.Ordinal), poke: "locate ssdwin", fresh: true);
            Assert.NotNull(line);
            if (line == previous)
            {
                break;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        var fields = line!.Split(' ');
        return (double.Parse(fields[3], CultureInfo.InvariantCulture), double.Parse(fields[4], CultureInfo.InvariantCulture));
    }

    private static async Task<byte[]> Shot(Session session, DirectoryInfo directory, string reticle)
    {
        Assert.NotNull(await session.WaitForAsync(line => line == $"RETICLE {reticle}", poke: "mode", fresh: false));
        await Task.Delay(600, TestContext.Current.CancellationToken);
        var path = Path.Combine(directory.FullName, $"{reticle}.png");
        Assert.NotNull(await session.WaitForAsync(line => line.StartsWith($"SHOTRAW {path} ", StringComparison.Ordinal), poke: $"shotraw {path}", fresh: true));
        var (rgba, width, height) = PngCodec.Decode(File.ReadAllBytes(path));
        var crop = new byte[Crop * Crop * 4];
        var left = (width / 2) - (Crop / 2);
        var top = (height / 2) - (Crop / 2);
        for (var line = 0; line < Crop; line++)
        {
            Array.Copy(rgba, (((top + line) * width) + left) * 4, crop, line * Crop * 4, Crop * 4);
        }

        return crop;
    }

    private static void Compare(byte[] rgba, int width, int height, string name, int tolerance, int allowed = 0)
    {
        var path = Path.Combine(Repository(), "tests", "DungeonWm.Tests", "Goldens", $"{name}.png");
        if (Environment.GetEnvironmentVariable("BASIN_UPDATE_GOLDENS") == "1")
        {
            File.WriteAllBytes(path, PngCodec.Encode(rgba, width, height));
            return;
        }

        Assert.True(File.Exists(path), $"Golden '{name}' missing. Run once with BASIN_UPDATE_GOLDENS=1 and commit {path}.");
        var (expected, expectedWidth, expectedHeight) = PngCodec.Decode(File.ReadAllBytes(path));
        Assert.Equal((expectedWidth, expectedHeight), (width, height));
        var differing = 0;
        for (var i = 0; i < expected.Length; i += 4)
        {
            var delta = Math.Max(Math.Abs(expected[i] - rgba[i]), Math.Max(Math.Abs(expected[i + 1] - rgba[i + 1]), Math.Abs(expected[i + 2] - rgba[i + 2])));
            if (delta > tolerance)
            {
                differing++;
            }
        }

        if (differing > allowed)
        {
            var actual = Path.Combine(Path.GetTempPath(), $"{name}-actual.png");
            File.WriteAllBytes(actual, PngCodec.Encode(rgba, width, height));
            Assert.Fail($"Golden '{name}' mismatch: {differing} pixels beyond {tolerance}, {allowed} allowed. Actual written to {actual}.");
        }
    }
}
