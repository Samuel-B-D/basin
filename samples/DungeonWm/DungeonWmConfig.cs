using System.Numerics;
using Basin.Config;
using Basin.Diagnostics;
using Tomlyn.Model;

namespace DungeonWm;

internal static class DungeonWmConfig
{
    public static List<string> RestartChanges(DungeonWmSettings running, DungeonWmSettings loaded)
    {
        var changed = new List<string>();
        void Check(string key, bool differs)
        {
            if (differs)
            {
                changed.Add(key);
            }
        }

        Check("render.renderer", running.Renderer != loaded.Renderer);
        Check("render.anisotropy", running.Render.Anisotropy != loaded.Render.Anisotropy);
        Check("world.seed", running.Map.Seed != loaded.Map.Seed);
        Check("world.unit", running.Bake.Unit != loaded.Bake.Unit);
        Check("world.room_min", (running.Map.RoomMinWidth, running.Map.RoomMinDepth) != (loaded.Map.RoomMinWidth, loaded.Map.RoomMinDepth));
        Check("world.room_max", (running.Map.RoomMaxWidth, running.Map.RoomMaxDepth) != (loaded.Map.RoomMaxWidth, loaded.Map.RoomMaxDepth));
        Check("world.decor_walls", running.Map.DecorWalls != loaded.Map.DecorWalls);
        Check("world.roof", running.Bake.Roof != loaded.Bake.Roof);
        Check("world.roof_beams", running.Bake.RoofBeams != loaded.Bake.RoofBeams);
        Check("world.torch_spacing", running.Map.TorchSpacing != loaded.Map.TorchSpacing);
        Check("world.torch_height", running.Bake.TorchHeight != loaded.Bake.TorchHeight);
        Check("world.torch_color", running.Bake.Torch != loaded.Bake.Torch);
        Check("world.props", running.Bake.Props != loaded.Bake.Props);
        Check("world.dynamic_props", running.Bake.DynamicProps != loaded.Bake.DynamicProps);
        Check("world.rooms", running.Rooms != loaded.Rooms);
        Check("physics.prop_density", running.PropDensity != loaded.PropDensity);
        return changed;
    }

    public static string DefaultPath() => TomlConfig.DefaultPath("dungeon-wm");

    public static DungeonWmSettings Load(string? path, bool none, BasinLogger log, out string? used)
    {
        used = null;
        var settings = new DungeonWmSettings();
        if (none)
        {
            return settings;
        }

        var named = path is { Length: > 0 };
        var file = named ? path! : DefaultPath();
        if (!named && !File.Exists(file))
        {
            Seed(file, log);
        }

        var table = TomlConfig.Read(file, out var failure);
        if (table is null)
        {
            if (named)
            {
                throw new InvalidOperationException($"{file}: {failure}");
            }

            if (File.Exists(file))
            {
                log.Warn($"{file} did not parse, keeping defaults: {failure}");
            }

            return settings;
        }

        used = file;
        Apply(settings, new TomlReader(table, log));
        return settings;
    }

    public static DungeonWmSettings Parse(string text, BasinLogger log)
    {
        var settings = new DungeonWmSettings();
        Apply(settings, new TomlReader(Tomlyn.Toml.ToModel(text), log));
        return settings;
    }

    public static string Template()
    {
        using var stream = typeof(DungeonWmConfig).Assembly.GetManifestResourceStream("dungeon-wm.toml")
            ?? throw new InvalidOperationException("dungeon-wm.toml is not embedded");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static void Apply(DungeonWmSettings settings, TomlReader reader)
    {
        if (reader.Section("render") is { } render)
        {
            settings.Renderer = render.Choice("renderer", settings.Renderer, "vulkan", "gl");
            settings.Render.RoomDepth = Math.Clamp(render.Number("room_depth", settings.Render.RoomDepth), 0, 16);
            settings.Render.MaxLights = Math.Clamp(render.Number("max_lights", settings.Render.MaxLights), 0, 8);
            settings.Render.Anisotropy = (float)Math.Clamp(render.Number("anisotropy", (double)settings.Render.Anisotropy), 1, 16);
            if (render.Numbers("direct_sample_range") is { Length: 2 } range)
            {
                settings.Render.DirectSampleMin = (float)range[0];
                settings.Render.DirectSampleMax = (float)range[1];
            }

            render.ReportUnknown();
        }

        if (reader.Section("camera") is { } camera)
        {
            settings.FieldOfView = (float)Math.Clamp(camera.Number("fov", (double)settings.FieldOfView), 30, 120);
            settings.Near = (float)Math.Clamp(camera.Number("near", (double)settings.Near), 0.005, 1);
            settings.Far = (float)Math.Clamp(camera.Number("far", (double)settings.Far), 5, 5000);
            settings.LookSensitivity = (float)camera.Number("look_sensitivity", (double)settings.LookSensitivity);
            settings.WalkSpeed = (float)camera.Number("walk_speed", (double)settings.WalkSpeed);
            settings.RunSpeed = (float)camera.Number("run_speed", (double)settings.RunSpeed);
            settings.EyeHeight = (float)Math.Clamp(camera.Number("eye_height", (double)settings.EyeHeight), 0.5, 2.5);
            settings.CrosshairOutput = camera.Text("crosshair_output") is { Length: > 0 } output ? output : null;
            settings.Reach = (float)Math.Clamp(camera.Number("reach", (double)settings.Reach), 0.1, 100);
            camera.ReportUnknown();
        }

        if (reader.Section("reticle") is { } reticle)
        {
            settings.ReticleColor = Color4(reticle.Raw("color"), settings.ReticleColor);
            settings.ReticleSize = Math.Clamp(reticle.Number("size", settings.ReticleSize), 8, 96);
            reticle.ReportUnknown();
        }

        if (reader.Section("world") is { } world)
        {
            var roomMin = world.Numbers("room_min") is { Length: 2 } min ? ((int)min[0], (int)min[1]) : (settings.Map.RoomMinWidth, settings.Map.RoomMinDepth);
            var roomMax = world.Numbers("room_max") is { Length: 2 } max ? ((int)max[0], (int)max[1]) : (settings.Map.RoomMaxWidth, settings.Map.RoomMaxDepth);
            var seed = world.Number("seed", settings.Map.Seed);
            settings.Map = new MapSettings(
                seed,
                Math.Max(2, roomMin.Item1),
                Math.Max(2, roomMin.Item2),
                Math.Max(Math.Max(2, roomMin.Item1), roomMax.Item1),
                Math.Max(Math.Max(2, roomMin.Item2), roomMax.Item2),
                Math.Clamp(world.Number("decor_walls", settings.Map.DecorWalls), 0, 0.8),
                Math.Max(1, world.Number("torch_spacing", settings.Map.TorchSpacing)));
            var roof = world.Choice("roof", "stone", "stone", "wood", "mixed", "none");
            settings.Bake = new BakeSettings(
                (float)Math.Clamp(world.Number("unit", (double)settings.Bake.Unit), 0.2, 3),
                roof switch
                {
                    "wood" => RoofKind.Wood,
                    "mixed" => RoofKind.Mixed,
                    "none" => RoofKind.None,
                    _ => RoofKind.Stone,
                },
                world.Flag("roof_beams", settings.Bake.RoofBeams),
                (float)world.Number("torch_height", (double)settings.Bake.TorchHeight),
                Color3(world.Raw("torch_color"), settings.Bake.Torch),
                Math.Clamp(world.Number("props", settings.Bake.Props), 0, 1),
                world.Flag("dynamic_props", settings.Bake.DynamicProps),
                seed);
            settings.PixelsPerMeter = (float)Math.Clamp(world.Number("pixels_per_meter", (double)settings.PixelsPerMeter), 100, 10000);
            settings.SkyColor = Color3(world.Raw("sky_color"), settings.SkyColor);
            settings.Rooms = Math.Clamp(world.Number("rooms", settings.Rooms), 1, 32);
            world.ReportUnknown();
        }

        if (reader.Section("windows") is { } windows)
        {
            settings.WindowDepth = (float)Math.Clamp(windows.Number("depth", (double)settings.WindowDepth), 0.001, 0.5);
            settings.HangHeight = (float)windows.Number("hang_height", (double)settings.HangHeight);
            settings.EdgeColor = Color4(windows.Raw("edge_color"), settings.EdgeColor);
            settings.FocusColor = Color4(windows.Raw("focus_color"), settings.FocusColor);
            settings.UnfocusedOpacity = (float)Math.Clamp(windows.Number("unfocused_opacity", (double)settings.UnfocusedOpacity), 0.1, 1);
            settings.OpenMilliseconds = Math.Clamp(windows.Number("open_ms", settings.OpenMilliseconds), 0, 10000);
            settings.CloseMilliseconds = Math.Clamp(windows.Number("close_ms", settings.CloseMilliseconds), 0, 10000);
            windows.ReportUnknown();
        }

        if (reader.Section("frames") is { } frames)
        {
            var current = settings.Frames;
            var depth = (float)Math.Clamp(frames.Number("depth", (double)current.Depth), 0.005, 0.3);
            string[]? styles = null;
            if (frames.Words("styles") is { } names)
            {
                styles = [.. names.Where(name => DungeonWmFrameStyle.Named(name) is not null)];
                foreach (var unknown in names.Where(name => DungeonWmFrameStyle.Named(name) is null))
                {
                    frames.Log.Warn($"{frames.Where("styles")}: unknown style {unknown}, expected one of {string.Join(", ", FrameSettings.AllStyles)}");
                }
            }

            settings.Frames = new FrameSettings(
                frames.Flag("enabled", current.Enabled),
                (float)Math.Clamp(frames.Number("width", (double)current.Width), 0.005, 0.5),
                depth,
                (float)Math.Clamp(frames.Number("recess", (double)current.Recess), 0, depth * 0.9),
                styles ?? current.Styles,
                frames.Flag("plaque", current.Plaque),
                (float)Math.Clamp(frames.Number("plaque_height", (double)current.PlaqueHeight), 0.02, 0.3));
            frames.ReportUnknown();
        }

        if (reader.Section("physics") is { } physics)
        {
            settings.Gravity = (float)physics.Number("gravity", (double)settings.Gravity);
            settings.PullDistance = (float)physics.Number("pull_distance", (double)settings.PullDistance);
            settings.RehangDistance = (float)physics.Number("rehang_distance", (double)settings.RehangDistance);
            settings.ThrowScale = (float)physics.Number("throw_scale", (double)settings.ThrowScale);
            settings.ThrowMinSpeed = (float)physics.Number("throw_min_speed", (double)settings.ThrowMinSpeed);
            settings.PropDensity = (float)Math.Clamp(physics.Number("prop_density", (double)settings.PropDensity), 1, 20000);
            physics.ReportUnknown();
        }

        if (reader.Section("avatar") is { } avatar)
        {
            settings.AvatarModel = avatar.Text("model");
            settings.AvatarScale = (float)Math.Clamp(avatar.Number("scale", (double)settings.AvatarScale), 0.05, 10);
            settings.AvatarShow = avatar.Words("show") ?? settings.AvatarShow;
            settings.AvatarSampleHz = Math.Clamp(avatar.Number("sample_hz", settings.AvatarSampleHz), 5, 240);
            if (avatar.Section("clips") is { } clips)
            {
                foreach (var state in settings.AvatarClips.Keys.ToArray())
                {
                    if (clips.Text(state) is { } clip)
                    {
                        settings.AvatarClips[state] = clip;
                    }
                }

                clips.ReportUnknown();
            }

            avatar.ReportUnknown();
        }

        if (reader.Free("keybindings") is TomlTable bindings)
        {
            settings.Bindings.Clear();
            foreach (var (chord, action) in bindings)
            {
                if (action is string name && DungeonWmBindings.IsAction(name))
                {
                    settings.Bindings[chord] = name;
                }
                else
                {
                    reader.Log.Warn($"[keybindings] {chord}: unknown action {action}");
                }
            }
        }

        reader.ReportUnknown();
    }

    private static Vector3 Color3(object? value, Vector3 fallback)
    {
        if (TomlColor.Rgba(value) is not { } rgba)
        {
            return fallback;
        }

        return new Vector3(((rgba >> 24) & 0xFF) / 255f, ((rgba >> 16) & 0xFF) / 255f, ((rgba >> 8) & 0xFF) / 255f);
    }

    private static Vector4 Color4(object? value, Vector4 fallback)
    {
        if (TomlColor.Rgba(value) is not { } rgba)
        {
            return fallback;
        }

        return new Vector4(((rgba >> 24) & 0xFF) / 255f, ((rgba >> 16) & 0xFF) / 255f, ((rgba >> 8) & 0xFF) / 255f, (rgba & 0xFF) / 255f);
    }

    private static void Seed(string file, BasinLogger log)
    {
        try
        {
            if (Path.GetDirectoryName(file) is { Length: > 0 } directory)
            {
                Directory.CreateDirectory(directory);
            }

            using (var stream = new FileStream(file, FileMode.CreateNew, FileAccess.Write))
            {
                using var writer = new StreamWriter(stream);
                writer.Write(Template());
            }

            log.Info($"wrote the default configuration to {file}");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            log.Warn($"cannot write {file}, keeping the built-in defaults: {error.Message}");
        }
    }
}
