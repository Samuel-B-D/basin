using Basin.Scene;
using Basin.Shell.Xdg;

namespace Basin.Desktop;

public sealed class NestedPopupPlacer
{
    private readonly OutputLayout? _layout;
    private readonly Dictionary<XdgSurfaceState, SceneSurface> _scenes = [];

    public NestedPopupPlacer(OutputLayout? layout = null)
    {
        _layout = layout;
    }

    public Func<XdgToplevelWindow, Box?>? ConstrainBox { get; set; }

    public event Action? Changed;

    public void Add(XdgSurfaceState xdg, SceneSurface scene)
    {
        ArgumentNullException.ThrowIfNull(xdg);
        ArgumentNullException.ThrowIfNull(scene);
        _scenes[xdg] = scene;
    }

    public void Remove(XdgSurfaceState xdg)
    {
        ArgumentNullException.ThrowIfNull(xdg);
        _scenes.Remove(xdg);
    }

    public bool TryGet(XdgSurfaceState xdg, out SceneSurface scene)
    {
        ArgumentNullException.ThrowIfNull(xdg);
        return _scenes.TryGetValue(xdg, out scene!);
    }

    public static XdgToplevelWindow? RootOf(XdgPopupWindow popup)
    {
        ArgumentNullException.ThrowIfNull(popup);
        var xdg = popup.Parent;
        while (xdg?.Role is XdgPopupWindow parent)
        {
            xdg = parent.Parent;
        }

        return xdg?.Role as XdgToplevelWindow;
    }

    public SceneSurface? Attach(XdgPopupWindow popup)
    {
        ArgumentNullException.ThrowIfNull(popup);
        if (popup.Parent is not { } parent || !_scenes.TryGetValue(parent, out var parentScene))
        {
            return null;
        }

        var scene = new SceneSurface(parentScene.Tree, popup.Surface);
        _scenes[popup.Xdg] = scene;

        void Place()
        {
            var origin = parent.EffectiveGeometry;
            var offset = popup.SurfacePosition;
            scene.Tree.SetPosition(origin.X + offset.X, origin.Y + offset.Y);
            Changed?.Invoke();
        }

        void Constrain()
        {
            if (!parentScene.Tree.TryMapSceneToLocal(0, 0, out var localX, out var localY))
            {
                return;
            }

            var origin = parent.EffectiveGeometry;
            var originX = (int)-localX + origin.X;
            var originY = (int)-localY + origin.Y;
            if (BoxFor(popup, originX, originY) is not { } box)
            {
                return;
            }

            popup.Unconstrain(new Box(box.X - originX, box.Y - originY, box.Width, box.Height));
        }

        Constrain();
        Place();
        popup.Xdg.Committed += Place;
        popup.GeometryChanged += Place;
        popup.Repositioned += Constrain;
        popup.Destroyed += () =>
        {
            popup.Xdg.Committed -= Place;
            popup.GeometryChanged -= Place;
            popup.Repositioned -= Constrain;
            _scenes.Remove(popup.Xdg);
            Changed?.Invoke();
        };
        return scene;
    }

    private Box? BoxFor(XdgPopupWindow popup, int originX, int originY)
    {
        if (ConstrainBox is { } choose)
        {
            return RootOf(popup) is { } root ? choose(root) : null;
        }

        if (_layout is not { } layout)
        {
            return null;
        }

        return layout.OutputAt(originX, originY) is { } output ? layout.BoxOf(output) : layout.Bounds;
    }
}
