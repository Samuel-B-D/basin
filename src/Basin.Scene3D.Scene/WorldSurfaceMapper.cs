namespace Basin.Scene3D.Scene;

public static class WorldSurfaceMapper
{
    public static bool TryMap(
        World world, WorldCamera camera, OutputLayout layout, Surface surface, double x, double y, out double surfaceX, out double surfaceY)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(layout);
        surfaceX = 0;
        surfaceY = 0;
        var windows = world.Windows;
        for (var w = 0; w < windows.Count; w++)
        {
            var layers = windows[w].Layers;
            for (var i = 0; i < layers.Length; i++)
            {
                if (ReferenceEquals(layers[i].Surface, surface))
                {
                    return camera.TryRayAt(layout, x, y, out var ray)
                        && WorldPicker.TryOnPlane(windows[w], i, ray, out surfaceX, out surfaceY);
                }
            }
        }

        return false;
    }
}
