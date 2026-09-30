using Basin.Scene;

namespace Basin.Scene3D.Scene;

public static class WorldSceneLayers
{
    public static void Collect(WorldWindow window, SceneTree tree, IRenderer renderer, int originX, int originY, ref int level)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(renderer);
        var children = tree.Children;
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (!child.Enabled)
            {
                continue;
            }

            if (child is SceneTree subtree)
            {
                Collect(window, subtree, renderer, originX, originY, ref level);
                continue;
            }

            if (child is not SceneBuffer buffer || buffer.Buffer is null)
            {
                continue;
            }

            var (x, y) = buffer.ScenePosition;
            var (width, height) = buffer.DisplaySize;
            window.AddLayer(new WorldSurfaceLayer
            {
                Surface = buffer.InputSurface,
                Texture = buffer.TextureFor(renderer),
                X = x - originX,
                Y = y - originY,
                Width = width,
                Height = height,
                Source = buffer.SourceBox,
                Level = level++,
                ContentVersion = buffer.ContentVersion,
                Input = buffer.InputEnabled && buffer.InputSurface is not null,
            });
        }
    }
}
