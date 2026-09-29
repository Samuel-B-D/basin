using System.Numerics;

namespace Basin.Scene3D;

public sealed class WorldPicker
{
    public const float LayerLift = 0.0005f;

    private readonly World _world;

    public WorldPicker(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        _world = world;
    }

    public WorldHit Pick(in WorldRay ray, WorldVisibleSet? visible = null)
    {
        var best = WorldHit.Miss;
        var windows = visible?.Windows ?? _world.Windows;
        for (var i = 0; i < windows.Count; i++)
        {
            var window = windows[i];
            if (window.Visible && window.Pickable && TryWindow(window, ray, out var hit) && hit.Distance < best.Distance)
            {
                best = hit;
            }
        }

        var rooms = visible?.Rooms ?? _world.Rooms;
        for (var r = 0; r < rooms.Count; r++)
        {
            var blockers = rooms[r].Blockers;
            for (var b = 0; b < blockers.Count; b++)
            {
                if (ray.TryBox(blockers[b], out var distance) && distance < best.Distance)
                {
                    best = new WorldHit(
                        WorldHitKind.Geometry, null, -1, null, 0, 0, ray.At(distance), BoxNormal(blockers[b], ray.At(distance)), distance);
                }
            }
        }

        return best;
    }

    public static bool TryWindow(WorldWindow window, in WorldRay ray, out WorldHit hit)
    {
        hit = WorldHit.Miss;
        var model = window.Model;
        if (!Matrix4x4.Invert(model, out var inverse))
        {
            return false;
        }

        var local = ray.Transformed(inverse);
        var halfWidth = window.Width * 0.5f;
        var halfHeight = window.Height * 0.5f;
        var layers = window.Layers;
        var found = false;
        for (var l = layers.Length - 1; l >= 0; l--)
        {
            var layer = layers[l];
            var lift = window.LayerDepth(layer);
            if (local.Direction.Z >= 0 || !local.TryPlane(new Vector3(0, 0, lift), Vector3.UnitZ, out var distance))
            {
                continue;
            }

            var point = local.At(distance);
            ToSurface(window, point, out var sx, out var sy);
            if (sx < layer.X || sy < layer.Y || sx >= layer.X + layer.Width || sy >= layer.Y + layer.Height ||
                (layer.Level == 0 && (MathF.Abs(point.X) > halfWidth || MathF.Abs(point.Y) > halfHeight)))
            {
                continue;
            }

            if (found && distance >= hit.Distance)
            {
                continue;
            }

            hit = new WorldHit(
                layer.Input && layer.Surface is not null ? WorldHitKind.Surface : WorldHitKind.Window,
                window, l, layer.Input ? layer.Surface : null, sx - layer.X, sy - layer.Y,
                Vector3.Transform(point, model), window.Normal, distance);
            found = true;
        }

        if (window.ShownFrame is { } frame)
        {
            return TryFrame(window, frame, local, model, found, ref hit);
        }

        var slab = new WorldBounds(
            new Vector3(-halfWidth, -halfHeight, -window.Depth), new Vector3(halfWidth, halfHeight, 0));
        if (local.TryBox(slab, out var slabDistance) && (!found || slabDistance < hit.Distance - 1e-5f))
        {
            var point = local.At(slabDistance);
            hit = new WorldHit(
                WorldHitKind.Window, window, -1, null, 0, 0, Vector3.Transform(point, model),
                Vector3.TransformNormal(BoxNormal(slab, point), model), slabDistance);
            found = true;
        }

        return found;
    }

    public static bool TryOnPlane(WorldWindow window, int layer, in WorldRay ray, out double surfaceX, out double surfaceY)
    {
        surfaceX = surfaceY = 0;
        if ((uint)layer >= (uint)window.LayerCount || !Matrix4x4.Invert(window.Model, out var inverse))
        {
            return false;
        }

        var info = window.Layers[layer];
        var local = ray.Transformed(inverse);
        var facing = local.Direction.Z;
        if (MathF.Abs(facing) < 1e-7f)
        {
            return false;
        }

        var distance = (window.LayerDepth(info) - local.Origin.Z) / facing;
        ToSurface(window, local.At(distance), out var sx, out var sy);
        surfaceX = sx - info.X;
        surfaceY = sy - info.Y;
        return true;
    }

    private static bool TryFrame(
        WorldWindow window, WorldWindowFrame frame, in WorldRay local, in Matrix4x4 model, bool found, ref WorldHit hit)
    {
        var halfWidth = window.Width * 0.5f;
        var halfHeight = window.Height * 0.5f;
        var outer = frame.OuterBox(window.Width, window.Height);
        if (local.TryBox(outer, out var distance) && (!found || distance < hit.Distance - 1e-5f))
        {
            var point = local.At(distance);
            var opening = point.Z > -1e-4f && MathF.Abs(point.X) <= halfWidth && MathF.Abs(point.Y) <= halfHeight;
            if (!opening)
            {
                ToSurface(window, point, out var sx, out var sy);
                hit = new WorldHit(
                    WorldHitKind.Frame, window, -1, null, sx, sy, Vector3.Transform(point, model),
                    Vector3.TransformNormal(BoxNormal(outer, point), model), distance,
                    frame.PartAt(point, window.Width, window.Height));
                found = true;
            }
            else if (!found && local.Direction.Z < 0 &&
                     local.TryPlane(new Vector3(0, 0, -frame.Recess), Vector3.UnitZ, out var canvas))
            {
                var back = local.At(canvas);
                ToSurface(window, back, out var sx, out var sy);
                hit = new WorldHit(
                    WorldHitKind.Window, window, -1, null, sx, sy, Vector3.Transform(back, model), window.Normal, canvas);
                found = true;
            }
        }

        if (frame.HasPlaque)
        {
            var plaque = frame.PlaqueBox(window.Height);
            if (local.TryBox(plaque, out var plaqueDistance) && (!found || plaqueDistance < hit.Distance - 1e-5f))
            {
                var point = local.At(plaqueDistance);
                var px = Math.Clamp((point.X - plaque.Min.X) / frame.PlaqueWidth, 0, 1) * frame.PlaquePixelWidth;
                var py = Math.Clamp((plaque.Max.Y - point.Y) / frame.PlaqueHeight, 0, 1) * frame.PlaquePixelHeight;
                hit = new WorldHit(
                    WorldHitKind.Plaque, window, -1, null, px, py, Vector3.Transform(point, model),
                    Vector3.TransformNormal(BoxNormal(plaque, point), model), plaqueDistance);
                found = true;
            }
        }

        return found;
    }

    private static void ToSurface(WorldWindow window, Vector3 local, out double sx, out double sy)
    {
        sx = (local.X + (window.Width * 0.5f)) / window.Width * window.SurfaceWidth;
        sy = ((window.Height * 0.5f) - local.Y) / window.Height * window.SurfaceHeight;
    }

    private static Vector3 BoxNormal(in WorldBounds box, Vector3 point)
    {
        var center = box.Center;
        var half = box.Size * 0.5f;
        var d = point - center;
        var x = MathF.Abs(d.X / MathF.Max(half.X, 1e-6f));
        var y = MathF.Abs(d.Y / MathF.Max(half.Y, 1e-6f));
        var z = MathF.Abs(d.Z / MathF.Max(half.Z, 1e-6f));
        return x >= y && x >= z ? new Vector3(MathF.Sign(d.X), 0, 0)
            : y >= z ? new Vector3(0, MathF.Sign(d.Y), 0)
            : new Vector3(0, 0, MathF.Sign(d.Z));
    }
}
