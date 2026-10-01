using System.Numerics;
using Basin.Diagnostics;

namespace DungeonWm;

internal sealed partial class DungeonWm
{
    private const float HangDrop = 0.15f;
    private const float HangDropShare = 0.35f;
    private const float HangSwingRadians = 3.5f * MathF.PI / 180;
    private const float HangFrameOpacity = 0.6f;

    private bool StartHang(DungeonWmWindow window)
    {
        if (_settings.OpenMilliseconds <= 0 || window.State != DungeonWmWindowState.Hung || window.Slot is null)
        {
            EndHang(window);
            return false;
        }

        window.Hanging = 0;
        window.Swing = (window.ToplevelId & 1) == 0 ? 1 : -1;
        PoseHang(window);
        BasinReport.Line($"HANGING {window.AppId} room={(window.Room?.Workspace ?? -1) + 1} slot={SlotName(window.Slot)}");
        ScheduleAll();
        return true;
    }

    private bool StepHangs(float seconds)
    {
        var any = false;
        foreach (var window in _windows)
        {
            if (window.Hanging < 0)
            {
                continue;
            }

            if (window.State != DungeonWmWindowState.Hung || window.Slot is null)
            {
                EndHang(window);
                continue;
            }

            window.Hanging += seconds;
            if (window.Hanging * 1000 >= _settings.OpenMilliseconds)
            {
                EndHang(window);
                PlaceOnSlot(window);
                BasinReport.Line($"HUNG {window.AppId} room={(window.Room?.Workspace ?? -1) + 1} slot={SlotName(window.Slot)}");
                SoundHung(window);
                RaiseEvent("dungeonwm/window-hung");
                continue;
            }

            PoseHang(window);
            any = true;
        }

        return any;
    }

    private bool Hangs()
    {
        foreach (var window in _windows)
        {
            if (window.Hanging >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void EndHang(DungeonWmWindow window)
    {
        window.Hanging = -1;
        window.Fade = 1;
        window.CanvasFade = 1;
    }

    private void PoseHang(DungeonWmWindow window)
    {
        PlaceOnSlot(window);
        var progress = Math.Clamp(window.Hanging * 1000 / Math.Max(1, _settings.OpenMilliseconds), 0, 1);
        var world = window.World;
        float lift;
        float angle;
        if (progress < HangDropShare)
        {
            var drop = progress / HangDropShare;
            lift = HangDrop * (1 - (drop * drop));
            angle = 0;
        }
        else
        {
            var settle = (progress - HangDropShare) / (1 - HangDropShare);
            var damping = (1 - settle) * (1 - settle);
            lift = 0;
            angle = window.Swing * HangSwingRadians * damping * MathF.Sin(2.5f * MathF.PI * settle);
        }

        var pivot = new Vector3(0, (world.Height * 0.5f) + (world.ShownFrame?.Rail ?? 0), 0);
        var swing = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, angle);
        var orientation = world.Orientation;
        world.Position += Vector3.Transform(pivot - Vector3.Transform(pivot, swing), orientation) + new Vector3(0, lift, 0);
        world.Orientation = orientation * swing;

        var canvas = Math.Clamp((progress - 0.25f) / 0.75f, 0, 1);
        window.Fade = HangFrameOpacity + ((1 - HangFrameOpacity) * progress);
        window.CanvasFade = canvas * canvas * (3 - (2 * canvas));
    }
}
