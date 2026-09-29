using Basin;
using Basin.Effects;
using Basin.Host;
using Basin.Scene;

namespace TinyComp;

internal sealed partial class TinyComp
{
    private bool _gridPointerLight;

    private readonly List<IPixelShader> _glowShaders = [];

    private bool StepGridLights(in FrameTick tick)
    {
        var running = false;
        var pointerLight = false;
        for (var i = 0; i < Views.Count; i++)
        {
            var view = Views[i];
            if (view.Tag is not OutputPolicy)
            {
                continue;
            }

            var canvas = view.Canvas;
            var settings = canvas.Settings.GridEffectsValue;
            var overview = OverviewOf(view);
            var step = canvas.Step is not null && overview.StepMesh is { IsDestroyed: false } ? overview.StepSource : null;
            var grid = canvas.GridSource;
            var feed = canvas.Lights;
            if ((grid is null && step is null) || !settings.Highlights)
            {
                if (feed.Clear())
                {
                    ApplyGridLights(view, feed);
                }

                continue;
            }

            pointerLight |= settings.HighlightPointerValue > 0;
            feed.Begin();
            FeedGridLights(view, feed, settings);
            var pointer = settings.HighlightPointerValue > 0 && ReferenceEquals(ViewAt(_cursorX, _cursorY), view) &&
                PointerOverBackdrop(_cursorX, _cursorY);
            feed.Pointer(_cursorX, _cursorY, pointer ? (float)settings.HighlightPointerValue : 0f);
            if (feed.Step(
                tick, settings.HighlightSpringValue, settings.HighlightDampingValue, (float)settings.HighlightRadiusValue,
                settings.HighlightRenderColor, (float)settings.HighlightGlowValue))
            {
                ApplyGridLights(view, feed);
            }

            running |= feed.Moving;
        }

        _gridPointerLight = pointerLight;
        return running;
    }

    private void ApplyGridLights(OutputView view, GridLightFeed feed)
    {
        var canvas = view.Canvas;
        if (canvas is { GridSource: { } source, Grid: { } grid })
        {
            source.Lights = feed.Lights;
            canvas.NotifyGrid();
        }

        if (canvas.Step is not null && OverviewOf(view) is { StepMesh: { IsDestroyed: false } mesh } overview)
        {
            overview.StepSource.Lights = feed.Lights;
            overview.NotifyStepGrid();
        }

        view.Scheduler?.ScheduleRepaint();
    }

    private void FeedGridLights(OutputView view, GridLightFeed feed, GridEffectsSetting settings)
    {
        var strength = (float)settings.HighlightWindowsValue;
        var focusStrength = Math.Max((float)settings.HighlightFocusedValue, strength);
        IGrabTarget? focused = _focused is not null ? _focused : _focusedX;
        var dragged = LitDraggedWindow(view);
        if (dragged is not null)
        {
            var draggedStrength = Math.Max((float)settings.HighlightDraggedValue, ReferenceEquals(dragged, focused) ? focusStrength : strength);
            if (!feed.Want(dragged, ScreenBoxOf(dragged), draggedStrength))
            {
                return;
            }
        }

        if (focused is not null && !ReferenceEquals(focused, dragged) && LightsOn(focused, view) &&
            !feed.Want(focused, ScreenBoxOf(focused), focusStrength))
        {
            return;
        }

        if (strength <= 0f)
        {
            return;
        }

        if (!FeedTree(_layers.Top, view, feed, strength, dragged, focused) ||
            (view.Active is { } workspace && !FeedTree(workspace.Tree, view, feed, strength, dragged, focused)) ||
            !FeedTree(_layers.Windows, view, feed, strength, dragged, focused))
        {
            return;
        }

        if (OverviewOf(view).ShelfTree is { } shelf)
        {
            _ = FeedTree(shelf, view, feed, strength, dragged, focused);
        }
    }

    private bool FeedTree(SceneTree tree, OutputView view, GridLightFeed feed, float strength, IGrabTarget? dragged, IGrabTarget? focused)
    {
        var children = tree.Children;
        for (var i = children.Count - 1; i >= 0; i--)
        {
            if (WindowOfTree(children[i]) is not { } window || ReferenceEquals(window, dragged) || ReferenceEquals(window, focused) ||
                !LightsOn(window, view))
            {
                continue;
            }

            if (!feed.Want(window, ScreenBoxOf(window), strength))
            {
                return false;
            }
        }

        return true;
    }

    private IGrabTarget? WindowOfTree(SceneNode node)
    {
        foreach (var window in _windows)
        {
            if (ReferenceEquals(window.Tree, node))
            {
                return window;
            }
        }

        foreach (var xwindow in _xwindows)
        {
            if (ReferenceEquals(xwindow.Tree, node))
            {
                return xwindow;
            }
        }

        return null;
    }

    private bool LightsOn(IGrabTarget window, OutputView view)
    {
        var (minimized, tree) = window switch
        {
            Window w => (w.Minimized, w.Tree),
            XWindow x => (x.Minimized, (SceneTree?)x.Tree),
            _ => (true, null),
        };
        return !minimized && tree is { IsDestroyed: false, Enabled: true } && ReferenceEquals(ViewOfWindow(window), view);
    }

    private IGrabTarget? LitDraggedWindow(OutputView view)
    {
        if (_mode == DragMode.Move && _grabWindow is { } held && LightsOn(held, view))
        {
            return held;
        }

        return FastestAwakeBody(view, out _);
    }

    private bool PointerOverBackdrop(double x, double y)
    {
        if (_scene.NodeAt(x, y) is not { Node: { } node })
        {
            return true;
        }

        for (SceneNode? walk = node; walk is not null; walk = walk.Parent)
        {
            if (ReferenceEquals(walk, _wallpapers))
            {
                return true;
            }
        }

        return false;
    }

    private static readonly CanvasSide[] PinchZones = [CanvasSide.Left, CanvasSide.Right, CanvasSide.Top, CanvasSide.Bottom];

    private static double PinchedCenter(CanvasView canvas, CanvasSide side, double rest)
    {
        var zone = SideIndex(side);
        canvas.Pinch.Rest(zone, rest);
        return canvas.Pinch.CenterOf(zone);
    }

    private static double PinchedSlope(CanvasView canvas, CanvasSide side, double slope) =>
        slope + canvas.Pinch.DepthOf(SideIndex(side));

    private static double StepPinch(GridEffectsSetting settings) => Math.Clamp(settings.PinchValue, 0.0, CanvasStepMap.MaxBulge);

    private bool PinchStepWalls(OutputView view)
    {
        var map = OverviewOf(view).Step;
        var pinch = view.Canvas.Pinch;
        var bulges = StepPinch(view.Canvas.Settings.GridEffectsValue) > 0;
        var changed = map.Bulges != bulges;
        map.Bulges = bulges;
        foreach (var side in PinchZones)
        {
            var zone = SideIndex(side);
            var rest = (side & CanvasSide.Horizontal) != 0 ? map.CenterY : map.CenterX;
            pinch.Rest(zone, rest);
            changed |= map.SetBulge((CanvasStepSides)(int)side, pinch.CenterOf(zone), pinch.DepthOf(zone));
        }

        return changed;
    }

    private bool StepGridPinches(in FrameTick tick)
    {
        var running = false;
        for (var i = 0; i < Views.Count; i++)
        {
            var view = Views[i];
            if (view.Tag is not OutputPolicy)
            {
                continue;
            }

            var canvas = view.Canvas;
            var settings = canvas.Settings.GridEffectsValue;
            var pinch = canvas.Pinch;
            var overview = OverviewOf(view);
            var step = canvas.Step is not null;
            var depth = step ? StepPinch(settings) : settings.PinchValue;
            var shown = GridShown(view);
            var fast = FastestAwakeBody(view, out var fastest) is not null && fastest > settings.PinchSpeedValue;
            canvas.Reveal.Held = fast;
            overview.StepReveal.Held = fast;
            if (depth == 0.0 && !pinch.Moving && !AnyEngaged(pinch))
            {
                continue;
            }

            foreach (var side in PinchZones)
            {
                var zone = SideIndex(side);
                if (depth == 0.0 || !shown || !PinchZoneActive(view, side) || !AimPinch(view, side, zone, depth, settings, step))
                {
                    pinch.Release(zone);
                }
            }

            if (pinch.Step(tick, settings.PinchSpringValue, settings.PinchDampingValue))
            {
                LayoutCanvas(view, animating: true);
                ReanchorDrag();
                view.Scheduler?.ScheduleRepaint();
            }

            running |= pinch.Moving;
        }

        return running;
    }

    private static bool AnyEngaged(GridPinch pinch)
    {
        for (var zone = 0; zone < GridPinch.Zones; zone++)
        {
            if (pinch.Engaged(zone))
            {
                return true;
            }
        }

        return false;
    }

    private bool PinchZoneActive(OutputView view, CanvasSide side) =>
        view.Canvas.Step is { } map ? map.WallWidth((CanvasStepSides)(int)side) > 0 : !WarpOf(view.Canvas, side).IsIdentity;

    private bool AimPinch(OutputView view, CanvasSide side, int zone, double depth, GridEffectsSetting settings, bool step)
    {
        IGrabTarget? best = null;
        var bestScore = 0.0;
        var bestInfluence = 0.0;
        if (_mode == DragMode.Move && _grabWindow is { } held && OverlapsView(held, view))
        {
            var influence = PinchInfluence(view, held, side, settings.PinchReachValue);
            if (influence > 0)
            {
                best = held;
                bestScore = GridPinch.Score(double.PositiveInfinity, influence);
                bestInfluence = influence;
            }
        }

        foreach (var (window, binding) in _physicsBindings)
        {
            if (!binding.Body.IsAwake || ReferenceEquals(window, best))
            {
                continue;
            }

            var (vx, vy) = binding.Body.Velocity;
            var speed = Math.Sqrt((vx * vx) + (vy * vy));
            if (speed <= settings.PinchSpeedValue || !OverlapsView(window, view))
            {
                continue;
            }

            var influence = PinchInfluence(view, window, side, settings.PinchReachValue);
            var score = GridPinch.Score(speed, influence);
            if (score > bestScore)
            {
                best = window;
                bestScore = score;
                bestInfluence = influence;
            }
        }

        if (best is null)
        {
            return false;
        }

        double center;
        if (step)
        {
            var screen = ScreenBoxOf(best);
            center = (side & CanvasSide.Horizontal) != 0 ? screen.Y + (screen.Height / 2.0) : screen.X + (screen.Width / 2.0);
        }
        else
        {
            var box = CanvasBoxOf(best);
            center = (side & CanvasSide.Horizontal) != 0 ? box.Y + (box.Height / 2.0) : box.X + (box.Width / 2.0);
        }

        view.Canvas.Pinch.Aim(zone, center, depth * bestInfluence, best);
        return true;
    }

    private bool OverlapsView(IGrabTarget window, OutputView view)
    {
        var screen = ScreenBoxOf(window);
        var output = _layout.BoxOf(view.Output);
        return screen.X < output.Right && screen.Right > output.X && screen.Y < output.Bottom && screen.Bottom > output.Y;
    }

    private bool GridShown(OutputView view) => view.Canvas.Step is not null
        ? OverviewOf(view).StepMesh is { IsDestroyed: false } && StepGridAlphaOf(view) > 0f
        : view.Canvas.GridSource is not null && GridAlphaOf(view) > 0f;

    private double PinchInfluence(OutputView view, IGrabTarget window, CanvasSide side, double reach)
    {
        var box = CanvasBoxOf(window);
        double seam;
        if (view.Canvas.Step is { } map)
        {
            var outline = map.Outline;
            seam = side switch
            {
                CanvasSide.Left => map.CenterX + ((outline.X - map.CenterX) / map.Zoom),
                CanvasSide.Right => map.CenterX + ((outline.Right - map.CenterX) / map.Zoom),
                CanvasSide.Top => map.CenterY + ((outline.Y - map.CenterY) / map.Zoom),
                _ => map.CenterY + ((outline.Bottom - map.CenterY) / map.Zoom),
            };
        }
        else
        {
            seam = WarpOf(view.Canvas, side).Seam;
        }

        var past = side switch
        {
            CanvasSide.Left => seam - box.X,
            CanvasSide.Right => box.Right - seam,
            CanvasSide.Top => seam - box.Y,
            _ => box.Bottom - seam,
        };
        if (past >= 0)
        {
            return 1.0;
        }

        return reach > 0 ? GridLights.Falloff(-past / reach) : 0.0;
    }

    private void ReanchorDrag()
    {
        if (_mode != DragMode.Move || _grabWindow is not { } window)
        {
            return;
        }

        if (_canvasScaleDrag && _canvasStates.TryGetValue(window, out var state) && state.Dragging && state.StepView is null)
        {
            (state.DragFieldX, state.DragFieldY) = ToCanvasPointAt(_cursorX, _cursorY);
            state.DragCursorX = _cursorX;
            state.DragCursorY = _cursorY;
        }

        _ = DragTo(_cursorX, _cursorY);
    }
}
