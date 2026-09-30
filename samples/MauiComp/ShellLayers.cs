using Basin.Scene;

namespace MauiComp;

internal sealed class ShellLayers
{
    public ShellLayers(SceneTree root)
    {
        Root = root;
        Background = new SceneTree(root);
        LayerBackground = new SceneTree(root);
        LayerBottom = new SceneTree(root);
        Windows = new SceneTree(root);
        Panel = new SceneTree(root);
        LayerTop = new SceneTree(root);
        Fullscreen = new SceneTree(root);
        LayerOverlay = new SceneTree(root);
        Overlay = new SceneTree(root);
        Lock = new SceneTree(root);
    }

    public SceneTree Root { get; }

    public SceneTree Background { get; }

    public SceneTree LayerBackground { get; }

    public SceneTree LayerBottom { get; }

    public SceneTree Windows { get; }

    public SceneTree Panel { get; }

    public SceneTree LayerTop { get; }

    public SceneTree Fullscreen { get; }

    public SceneTree LayerOverlay { get; }

    public SceneTree Overlay { get; }

    public SceneTree Lock { get; }

    public void SetLocked(bool locked)
    {
        Background.Enabled = !locked;
        LayerBackground.Enabled = !locked;
        LayerBottom.Enabled = !locked;
        Windows.Enabled = !locked;
        Panel.Enabled = !locked;
        LayerTop.Enabled = !locked;
        Fullscreen.Enabled = !locked;
        LayerOverlay.Enabled = !locked;
        Overlay.Enabled = !locked;
    }
}
