using System.Numerics;
using Basin.Config;
using Basin.Diagnostics;
using Basin.Ipc;
using Basin.Scene3D;

namespace DungeonWm;

internal sealed partial class DungeonWm
{
    private static PlaqueButton PlaqueAt(in WorldHit pick) =>
        pick.Kind == WorldHitKind.Plaque && pick.Window?.ShownFrame is { } frame
            ? PlaqueLayout.At(pick.SurfaceX, pick.SurfaceY, frame.PlaquePixelWidth, frame.PlaquePixelHeight)
            : PlaqueButton.None;

    private void HoverPlaque(in WorldHit pick)
    {
        var window = pick.Kind == WorldHitKind.Plaque ? pick.Window?.Tag as DungeonWmWindow : null;
        var button = window is null ? PlaqueButton.None : PlaqueAt(pick);
        if (ReferenceEquals(window, _plaqueHover) && (window is null || window.PlaqueHover == button))
        {
            return;
        }

        if (_plaqueHover is { } previous)
        {
            previous.PlaqueHover = PlaqueButton.None;
        }

        _plaqueHover = window;
        if (window is not null)
        {
            window.PlaqueHover = button;
        }

        ScheduleAll();
    }

    private void PressPlaque(DungeonWmWindow window, PlaqueButton button)
    {
        if (button == PlaqueButton.None)
        {
            return;
        }

        _plaquePress = window;
        window.PlaquePressed = button;
        ScheduleAll();
    }

    private void ReleasePlaque(DungeonWmWindow window)
    {
        _plaquePress = null;
        var pressed = window.PlaquePressed;
        window.PlaquePressed = PlaqueButton.None;
        ScheduleAll();
        var pick = PickAt(_pointer.X, _pointer.Y);
        if (ReferenceEquals(pick.Window?.Tag, window) && PlaqueAt(pick) == pressed)
        {
            var shift = (DungeonWmBindings.Held(_seat.Keyboard) & Modifiers.Shift) != 0;
            RunPlaque(window, pressed, shift);
        }
    }

    private bool RunPlaque(DungeonWmWindow window, PlaqueButton button, bool nextRoom)
    {
        BasinReport.Line($"PLAQUE {window.AppId} {PlaqueLayout.Name(button)}{(nextRoom ? " next-room" : string.Empty)}");
        SoundPlaque(window);
        switch (button)
        {
            case PlaqueButton.Close:
                window.Close();
                return true;
            case PlaqueButton.Takeover:
                EnterTakeover(window);
                return true;
            case PlaqueButton.Rehang:
                return RehangNext(window, nextRoom);
            case PlaqueButton.Throw:
                return Toss(window);
            default:
                return false;
        }
    }

    private bool RehangNext(DungeonWmWindow window, bool nextRoom)
    {
        if (window.State == DungeonWmWindowState.Takeover)
        {
            LeaveTakeover();
        }

        var rooms = _world.Map.Rooms.Where(r => !r.Removed).ToList();
        var room = window.Room ?? _workspaces.Active ?? rooms.FirstOrDefault();
        if (room is null)
        {
            return false;
        }

        if (nextRoom)
        {
            var index = rooms.IndexOf(room);
            var next = rooms[(index + 1) % rooms.Count];
            HangNow(window, next, null);
            return true;
        }

        var slots = _world.SlotsOf(room).ToList();
        var start = window.Slot is { } current ? slots.FindIndex(slot => slot.Side == current.Side && slot.Along == current.Along) : -1;
        for (var step = 1; step <= slots.Count; step++)
        {
            var candidate = slots[((start + step) % slots.Count + slots.Count) % slots.Count];
            if (_world.IsFree(candidate) && (!window.Wide || (_world.Neighbour(candidate) is { } partner && _world.IsFree(partner))))
            {
                HangNow(window, room, candidate);
                return true;
            }
        }

        BasinReport.Line($"REHANG none {window.AppId}");
        return false;
    }

    private bool Toss(DungeonWmWindow window)
    {
        if (_physics is not { } physics || window.State == DungeonWmWindowState.Takeover)
        {
            return false;
        }

        Detach(window);
        if (!physics.Bodies.TryGetValue(window, out var body))
        {
            return false;
        }

        var velocity = (window.World.Normal * 1.6f) + new Vector3(0, 1.2f, 0);
        physics.World.SetVelocity(body, velocity, new Vector3(0.4f, 0.2f, 0));
        BasinReport.Line(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"THROW {window.AppId} speed={velocity.Length():F2}"));
        OnThrow(window);
        ScheduleAll();
        return true;
    }

    private void Locate(DungeonWmWindow window)
    {
        var world = window.World;
        var view = OutputUnderPointer() ?? _outputs.Views[0];
        var projection = _camera.ViewOf(_layout.Bounds, _layout.BoxOf(view.Output));
        void Line(string name, Vector3 local)
        {
            if (projection.TryProject(Vector3.Transform(local, world.Model), out var x, out var y, out _))
            {
                _report.Line(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"LOCATE {window.AppId} {name} {x:F1} {y:F1}"));
            }
        }

        var halfWidth = world.Width * 0.5f;
        var halfHeight = world.Height * 0.5f;
        Line("canvas", new Vector3(0, 0, -world.Recess));
        if (world.ShownFrame is not { } frame)
        {
            return;
        }

        var rail = frame.Rail;
        var face = rail * (1 + frame.EdgeShare) * 0.5f;
        var lip = rail * frame.EdgeShare * 0.5f;
        Line("frame", new Vector3(-halfWidth - face, 0, 0));
        Line("left", new Vector3(-halfWidth - lip, 0, 0));
        Line("right", new Vector3(halfWidth + lip, 0, 0));
        Line("top", new Vector3(0, halfHeight + lip, 0));
        Line("bottom", new Vector3(0, -halfHeight - lip, 0));
        Line("top-left", new Vector3(-halfWidth - (rail * 0.5f), halfHeight + (rail * 0.5f), 0));
        Line("top-right", new Vector3(halfWidth + (rail * 0.5f), halfHeight + (rail * 0.5f), 0));
        Line("bottom-left", new Vector3(-halfWidth - (rail * 0.5f), -halfHeight - (rail * 0.5f), 0));
        Line("bottom-right", new Vector3(halfWidth + (rail * 0.5f), -halfHeight - (rail * 0.5f), 0));
        if (!frame.HasPlaque)
        {
            return;
        }

        var box = frame.PlaqueBox(world.Height);
        foreach (var button in PlaqueLayout.Buttons)
        {
            var (x, y, size) = PlaqueLayout.Rect(button, frame.PlaquePixelWidth, frame.PlaquePixelHeight);
            var u = (x + (size * 0.5f)) / frame.PlaquePixelWidth;
            var v = (y + (size * 0.5f)) / frame.PlaquePixelHeight;
            Line($"plaque-{PlaqueLayout.Name(button)}", new Vector3(box.Min.X + (u * frame.PlaqueWidth), box.Max.Y - (v * frame.PlaqueHeight), box.Max.Z));
        }
    }

    private void RegisterPlaqueCommands(IpcMethodRegistry methods)
    {
        _report.Register(methods, "dungeonwm/locate", "locate {id}",
            Describe("Print where a window's canvas, frame rails, corners and plaque buttons fall in layout pixels.", IpcMethodTraits.ReadOnly),
            (ref IpcParams p, IpcReply _) =>
            {
                var id = p.GetString("id");
                if (p.Failed)
                {
                    return;
                }

                if (FindWindow(id) is not { } window || _outputs.Views.Count == 0)
                {
                    p.Invalid($"no window {id}");
                    return;
                }

                Locate(window);
            });

        _report.Register(methods, "dungeonwm/plaque", "plaque {id} {button}",
            Describe("Press a button on a window's plaque: close, takeover, rehang, rehang-room or throw.", IpcMethodTraits.None),
            (ref IpcParams p, IpcReply _) =>
            {
                var id = p.GetString("id");
                var name = p.GetString("button");
                if (p.Failed)
                {
                    return;
                }

                if (FindWindow(id) is not { } window)
                {
                    p.Invalid($"no window {id}");
                    return;
                }

                var nextRoom = name == "rehang-room";
                var button = PlaqueLayout.Parse(nextRoom ? "rehang" : name);
                if (button == PlaqueButton.None)
                {
                    p.Invalid($"no plaque button {name}: close, takeover, rehang, rehang-room or throw");
                    return;
                }

                RunPlaque(window, button, nextRoom);
                ScheduleAll();
            });
    }
}
