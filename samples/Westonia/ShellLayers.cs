using Basin.Scene;

namespace Westonia;

internal sealed class ShellLayers
{
    private readonly SceneTree _root;
    private bool _locked;

    public ShellLayers(SceneTree root)
    {
        _root = root;
        Minimized = new SceneTree(root) { Enabled = false };
        Background = new SceneTree(root);
        LayerBackground = new SceneTree(root);
        LayerBottom = new SceneTree(root);
        Workspaces = new SceneTree(root);
        Panel = new SceneTree(root);
        LayerTop = new SceneTree(root);
        Fullscreen = new SceneTree(root);
        InputPanel = new SceneTree(root);
        LayerOverlay = new SceneTree(root);
        Cursor = new SceneTree(root);
        Lock = new SceneTree(root) { Enabled = false };
    }

    public SceneTree Minimized { get; }

    public SceneTree Background { get; }

    public SceneTree LayerBackground { get; }

    public SceneTree LayerBottom { get; }

    public SceneTree Workspaces { get; }

    public SceneTree Panel { get; }

    public SceneTree LayerTop { get; }

    public SceneTree Fullscreen { get; }

    public SceneTree InputPanel { get; }

    public SceneTree LayerOverlay { get; }

    public SceneTree Cursor { get; }

    public SceneTree Lock { get; }

    public bool IsLocked => _locked;

    public void SetLocked(bool locked)
    {
        if (_locked == locked)
        {
            return;
        }

        _locked = locked;
        Background.Enabled = !locked;
        LayerBackground.Enabled = !locked;
        LayerBottom.Enabled = !locked;
        Workspaces.Enabled = !locked;
        Panel.Enabled = !locked;
        LayerTop.Enabled = !locked;
        Fullscreen.Enabled = !locked;
        InputPanel.Enabled = !locked;
        LayerOverlay.Enabled = !locked;
        Lock.Enabled = locked;
        Lock.RaiseToTop();
        Cursor.RaiseToTop();
    }

    public SceneTree Root => _root;
}
