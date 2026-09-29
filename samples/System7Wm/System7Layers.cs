using Basin.Scene;

namespace System7Wm;

internal sealed class System7Layers
{
    public System7Layers(SceneTree root)
    {
        Root = root;
        Hidden = new SceneTree(root) { Enabled = false };
        Pattern = new SceneTree(root);
        Icons = new SceneTree(root);
        Windows = new SceneTree(root);
        Outlines = new SceneTree(root);
        Alerts = new SceneTree(root);
        MenuBar = new SceneTree(root);
        Popups = new SceneTree(root);
        Cursor = new SceneTree(root);
    }

    public SceneTree Root { get; }

    public SceneTree Hidden { get; }

    public SceneTree Pattern { get; }

    public SceneTree Icons { get; }

    public SceneTree Windows { get; }

    public SceneTree Outlines { get; }

    public SceneTree Alerts { get; }

    public SceneTree MenuBar { get; }

    public SceneTree Popups { get; }

    public SceneTree Cursor { get; }
}
