using Basin;
using Basin.Diagnostics;
using Basin.Scene;
using Basin.XWayland;

namespace System7Wm;

internal sealed partial class System7Shell
{
    private readonly Dictionary<XWaylandWindow, SceneTree> _x11Containers = [];
    private XWaylandSceneDriver? _x11;

    private void AttachXWayland(XWaylandWm wm)
    {
        _x11?.Dispose();
        _x11 = new XWaylandSceneDriver
        {
            ManagedParent = window =>
            {
                var container = new SceneTree(_layers.Windows);
                _x11Containers[window] = container;
                return container;
            },
            OverrideRedirectParent = _ => _layers.Alerts,
        };
        _x11.Adopted += OnX11Adopted;
        _x11.Removed += (window, _) =>
        {
            _registrar?.Forget(window.WindowId);
            if (WindowOf(window.Surface) is { X11: not null } shell)
            {
                Forget(shell, destroyed: true);
                if (window.Surface is { } surface)
                {
                    _bySurface.Remove(surface);
                }
            }

            if (_x11Containers.Remove(window, out var container) && !container.IsDestroyed)
            {
                container.Destroy();
            }
        };
        _x11.ActivationRequested += window =>
        {
            if (WindowOf(window.Surface) is { } shell)
            {
                Activate(shell);
            }
        };
        _x11.Attach(wm);
    }

    private void OnX11Adopted(XWaylandWindow x11, SceneSurface scene, bool managed)
    {
        RefreshSurfaceLuts();
        if (!managed || x11.Surface is not { } surface || !_x11Containers.TryGetValue(x11, out var container))
        {
            _outputs.ScheduleAll();
            return;
        }

        var app = AppFor(x11.Class.Length > 0 ? x11.Class : x11.Instance, PidOf(surface));
        var window = new ShellWindow(container, app) { X11 = x11, Owner = WindowOf, Scene = scene, ContentNode = scene.Tree, Mapped = true };
        _bySurface[surface] = window;
        app.Windows.Add(window);
        window.Kind = KindOf(window);
        if (x11.TransientFor is null)
        {
            Place(window);
            x11.Configure(window.X, window.Y, x11.Width, x11.Height);
        }
        else
        {
            window.X = x11.X;
            window.Y = x11.Y;
        }

        scene.Tree.SetPosition(window.X, window.Y);
        x11.GeometryChanged += () =>
        {
            if (window.Mapped && _drag?.Window != window)
            {
                window.X = x11.X;
                window.Y = x11.Y;
                UpdateFrame(window);
            }
        };
        x11.TitleChanged += () => UpdateFrame(window);
        x11.DecorationsChanged += () => Reframe(window);
        x11.SizeHintsChanged += () => UpdateFrame(window);
        x11.AppMenuChanged += () => ApplyAddress(window);
        x11.MinimizeRequested += minimize =>
        {
            if (minimize)
            {
                HideApp(window.App);
            }
        };
        x11.MaximizeRequested += maximize =>
        {
            if (maximize != window.Zoomed)
            {
                Zoom(window);
            }
        };
        _stack.Add(window);
        Reframe(window);
        ApplyAddress(window);
        if (x11.WantsFocus)
        {
            Activate(window);
        }

        BasinReport.Line($"WINDOW + {window.Id} {window.AppId} \"{window.Title}\" x11");
    }
}
