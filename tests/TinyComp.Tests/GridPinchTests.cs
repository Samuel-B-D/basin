using System.Globalization;
using Basin.Effects;
using Basin;
using Xunit;
using static TinyComp.Tests.CanvasRows;

namespace TinyComp.Tests;

public sealed class GridPinchTests
{
    private const int ZoneWidth = 154;

    private const string Pinch =
        "[canvas]\nenable = true\ngrid = \"always\"\nanimation_ms = 100\n[canvas.grid_effects]\npinch = 0.8\npinch_reach = 300\n";

    private static Dictionary<string, string> Fields(string line)
    {
        var fields = new Dictionary<string, string>();
        foreach (var part in line.Split(' '))
        {
            var equals = part.IndexOf('=');
            if (equals > 0)
            {
                fields[part[..equals]] = part[(equals + 1)..];
            }
        }

        return fields;
    }

    private static (Dictionary<string, (double Depth, double Center)> Zones, bool Shown) GridState(CanvasSession session, string output = "HEADLESS-1")
    {
        session.Send("grid-state");
        var line = session.WaitForLine($"GRID output={output} ");
        Assert.NotNull(line);
        var fields = Fields(line!);
        var zones = new Dictionary<string, (double, double)>();
        if (fields["pinch"] != "none")
        {
            foreach (var entry in fields["pinch"].Split(','))
            {
                var colon = entry.IndexOf(':');
                var at = entry.IndexOf('@');
                zones[entry[..colon]] = (
                    double.Parse(entry[(colon + 1)..at], CultureInfo.InvariantCulture),
                    double.Parse(entry[(at + 1)..], CultureInfo.InvariantCulture));
            }
        }

        return (zones, fields["shown"] == "yes");
    }

    private static (int X, int Y) Where(CanvasSession session)
    {
        session.Send("where");
        Assert.NotNull(session.WaitForLine("WIN "));
        var line = session.WaitForLine("CANVASWIN ");
        Assert.NotNull(line);
        var canvas = Fields(line!)["canvas"].Split(',');
        return (int.Parse(canvas[0], CultureInfo.InvariantCulture), int.Parse(canvas[1], CultureInfo.InvariantCulture));
    }

    private static void Grab(CanvasSession session, int x, int y)
    {
        session.Send($"move {x} {y}");
        session.Send("key 56 1");
        session.Send("button 272 1");
    }

    private static void Drop(CanvasSession session)
    {
        session.Send("button 272 0");
        session.Send("key 56 0");
    }

    private static void DragTo(CanvasSession session, int fromX, int toX, int y, int step = 30)
    {
        var commands = new List<string>();
        for (var x = fromX; x != toX;)
        {
            x = fromX > toX ? Math.Max(toX, x - step) : Math.Min(toX, x + step);
            commands.Add($"move {x} {y}");
        }

        session.SendPaced(commands, 20);
    }

    private static CanvasWarpTransform PinchedMap(double depth, double center)
    {
        var left = new CanvasWarp();
        left.Layout(ZoneWidth, -1, ZoneWidth, 640, 0.2, 0.25 + depth, center);
        var right = new CanvasWarp(1);
        right.Layout(1280 - ZoneWidth, 1, ZoneWidth, 640, 0.2, 0.25, 360);
        return new CanvasWarpTransform { Left = left, Right = right };
    }

    private static bool Settled(CanvasSession session, Func<Dictionary<string, (double Depth, double Center)>, bool> done, int millis = 6000)
    {
        var deadline = Environment.TickCount64 + millis;
        while (Environment.TickCount64 < deadline)
        {
            if (done(GridState(session).Zones))
            {
                return true;
            }

            Thread.Sleep(100);
        }

        return false;
    }

    [Fact]
    public void A_dragged_window_moves_the_left_zone_center_toward_its_row_and_it_springs_back()
    {
        using var session = CanvasSession.Start(Pinch);
        Assert.SkipWhen(session is null, "tinycomp or weston-simple-shm is not available beside the tests");
        var (x, y) = Where(session!);
        var before = GridState(session).Zones;
        Assert.Equal(0.0, before["left"].Depth);
        Assert.Equal(360.0, before["left"].Center);

        Grab(session, x + 100, y + 40);
        DragTo(session, x + 100, 120, y + 40);
        Thread.Sleep(1200);
        var (canvasX, canvasY) = Where(session);
        var held = GridState(session).Zones;
        var row = canvasY + 128.0;
        Assert.True(held["left"].Depth > 0.7, $"the left zone pinches while the window is held at canvas x {canvasX}: {held["left"]}");
        Assert.True(Math.Abs(held["left"].Center - row) < 3, $"the left center {held["left"].Center} follows the window's row {row}");
        Assert.Equal(0.0, held["right"].Depth);

        Drop(session);
        Assert.True(
            Settled(session, zones => zones["left"].Depth == 0.0 && zones["left"].Center == 360.0),
            "the pinch springs back to the layout after the drop");
    }

    [Fact]
    public void A_thrown_window_holds_the_drag_grid_and_the_pinch_while_it_is_fast()
    {
        using var session = CanvasSession.Start(
            "[canvas]\nenable = true\ngrid = \"drag\"\ngrid_linger_ms = 150\nanimation_ms = 100\n"
            + "[canvas.grid_effects]\npinch = 0.8\npinch_reach = 600\npinch_speed = 150\n"
            + "[physics]\nenable = true\n");
        Assert.SkipWhen(session is null, "tinycomp or weston-simple-shm is not available beside the tests");
        var (x, y) = Where(session!);
        Assert.False(GridState(session).Shown);
        Grab(session, x + 100, y + 40);
        DragTo(session, x + 100, x + 60, y + 40, 10);
        Drop(session);
        session.Send("throw -900 0");
        session.WaitForLine("THROW ");
        Thread.Sleep(400);
        var during = GridState(session);
        Assert.True(during.Shown, "the drag grid stays shown while the thrown window is fast");
        Assert.True(during.Zones["left"].Depth > 0.05, $"the left zone pinches toward the thrown window: {during.Zones["left"]}");

        var deadline = Environment.TickCount64 + 10000;
        var after = during;
        while (Environment.TickCount64 < deadline && (after.Shown || after.Zones.Values.Any(zone => zone.Depth > 0)))
        {
            Thread.Sleep(200);
            after = GridState(session);
        }

        Assert.False(after.Shown, "the drag grid hides once the window slows");
        Assert.All(after.Zones.Values, zone => Assert.Equal(0.0, zone.Depth));
    }

    [Fact]
    public void A_click_in_a_pinched_zone_lands_on_the_window_drawn_there()
    {
        const string config = "[canvas]\nenable = true\ngrid = \"always\"\nanimation_ms = 100\n"
            + "[canvas.grid_effects]\npinch = 1.5\npinch_reach = 600\n";
        using var session = CanvasSession.Start(config, SsdWin());
        Assert.SkipWhen(session is null, "tinycomp or ssdwin is not available beside the tests");
        var (x, y) = Where(session!);
        Grab(session, x + 100, y + 40);
        DragTo(session, x + 100, 140, y + 40);
        Thread.Sleep(1000);
        session.Rewrite(config + "pinch_spring = 1\npinch_damping = 4\n");
        session.Send("reload");
        Assert.NotNull(session.WaitForLine("RELOAD"));
        Drop(session);
        Thread.Sleep(200);
        var (canvasX, canvasY) = Where(session);
        var first = GridState(session).Zones["left"];
        Assert.True(first.Depth > 0.3, $"the pinch is still well under way: {first}");
        var local = (X: 20.0, Y: 220.0);
        var pinched = PinchedMap(first.Depth, first.Center);
        var (screenX, screenY) = pinched.ToScreenPoint(canvasX + local.X, canvasY + local.Y);
        var (plainX, plainY) = PinchedMap(0, 360).ToScreenPoint(canvasX + local.X, canvasY + local.Y);
        Assert.True(Math.Abs(screenY - plainY) > 3, $"the pinch moves the point: {screenY} against {plainY}");

        session.Send(string.Create(CultureInfo.InvariantCulture, $"move {screenX:F2} {screenY:F2}"));
        session.Send("button 272 1");
        session.Send("button 272 0");
        var line = session.WaitForClientLine("BUTTON ");
        var second = GridState(session).Zones["left"];
        Assert.NotNull(line);
        var parts = line!.Split(' ');
        var actualX = double.Parse(parts[2], CultureInfo.InvariantCulture);
        var actualY = double.Parse(parts[3], CultureInfo.InvariantCulture);
        var early = pinched.ToCanvasPoint(screenX, screenY);
        var late = PinchedMap(second.Depth, second.Center).ToCanvasPoint(screenX, screenY);
        var lowY = Math.Min(early.Y, late.Y) - canvasY - 1.0;
        var highY = Math.Max(early.Y, late.Y) - canvasY + 1.0;
        var lowX = Math.Min(early.X, late.X) - canvasX - 1.0;
        var highX = Math.Max(early.X, late.X) - canvasX + 1.0;
        Assert.True(
            actualX >= lowX && actualX <= highX && actualY >= lowY && actualY <= highY,
            $"the click reached ({actualX},{actualY}); the pinched map says x {lowX:F1}..{highX:F1}, y {lowY:F1}..{highY:F1}");
    }

    [Fact]
    public void A_grid_drag_into_a_pinched_zone_keeps_the_window_under_the_pointer()
    {
        using var session = CanvasSession.Start(Pinch.Replace("animation_ms = 100\n", "animation_ms = 100\ndrag = \"grid\"\n", StringComparison.Ordinal));
        Assert.SkipWhen(session is null, "tinycomp or weston-simple-shm is not available beside the tests");
        var (x, y) = Where(session!);
        var grabX = x + 100;
        var grabY = y + 40;
        Grab(session, grabX, grabY);
        DragTo(session, grabX, 60, grabY, 12);
        Thread.Sleep(1500);
        var (canvasX, canvasY) = Where(session);
        var left = GridState(session).Zones["left"];
        Assert.True(left.Depth > 0.5, $"the zone is pinched: {left}");
        var map = PinchedMap(left.Depth, left.Center);
        var (screenX, screenY) = map.ToScreenPoint(canvasX + 100, canvasY + 40);
        var (stretchX, stretchY) = map.AxisStretch(canvasX + 100, canvasY + 40);
        Drop(session);
        var toleranceX = Math.Max(1.0, stretchX + 0.25);
        var toleranceY = Math.Max(1.0, stretchY + 0.25);
        var under = map.ToCanvasPoint(60, grabY);
        Assert.True(
            Math.Abs(screenX - 60) <= toleranceX && Math.Abs(screenY - grabY) <= toleranceY,
            $"the grabbed point draws at ({screenX:F2},{screenY:F2}), the pointer is at (60,{grabY}), within ({toleranceX:F2},{toleranceY:F2}); canvas under the pointer {under.X:F2},{under.Y:F2} against grab {canvasX + 100},{canvasY + 40}; left {left}");
    }

    [Fact]
    public void A_window_across_two_outputs_pinches_a_zone_on_each()
    {
        using var session = CanvasSession.Start(
            "[canvas]\nenable = true\ngrid = \"always\"\nanimation_ms = 100\n[canvas.grid_effects]\npinch = 0.8\npinch_reach = 2400\n",
            compositorArguments: ["--outputs", "2"]);
        Assert.SkipWhen(session is null, "tinycomp or weston-simple-shm is not available beside the tests");
        var (x, y) = Where(session!);
        Grab(session, x + 100, y + 40);
        DragTo(session, x + 100, 1280 + 20, y + 40, 60);
        Thread.Sleep(1200);
        var first = GridState(session, "HEADLESS-1").Zones;
        var second = GridState(session, "HEADLESS-2").Zones;
        Drop(session);
        Assert.True(first.TryGetValue("left", out var left) && left.Depth > 0.05, $"the left output pinches its left zone: {string.Join(',', first)}");
        Assert.True(second.TryGetValue("right", out var right) && right.Depth > 0.05, $"the right output pinches its right zone: {string.Join(',', second)}");
        Assert.False(first.ContainsKey("right"), "the shared seam has no zone");
        Assert.False(second.ContainsKey("left"), "the shared seam has no zone");
    }

    [Fact]
    public void A_window_dragged_into_a_corner_pinches_both_zones()
    {
        using var session = CanvasSession.Start(
            "[canvas]\nenable = true\ngrid = \"always\"\nanimation_ms = 100\nsides = [\"left\", \"right\", \"top\", \"bottom\"]\n"
            + "[canvas.grid_effects]\npinch = 0.8\npinch_reach = 300\n");
        Assert.SkipWhen(session is null, "tinycomp or weston-simple-shm is not available beside the tests");
        var (x, y) = Where(session!);
        Grab(session, x + 100, y + 40);
        var commands = new List<string>();
        for (var i = 1; i <= 12; i++)
        {
            commands.Add($"move {x + 100 - (i * ((x + 100 - 60) / 12))} {y + 40 - (i * ((y + 40 - 30) / 12))}");
        }

        session.SendPaced(commands, 20);
        Thread.Sleep(1200);
        var zones = GridState(session).Zones;
        Drop(session);
        Assert.True(zones["left"].Depth > 0.3, $"the left zone pinches: {zones["left"]}");
        Assert.True(zones["top"].Depth > 0.3, $"the top zone pinches: {zones["top"]}");
        Assert.Equal(0.0, zones["right"].Depth);
        Assert.Equal(0.0, zones["bottom"].Depth);
    }

    [Fact]
    public void The_curved_preset_applies_at_run_time_and_a_reload_clears_it()
    {
        using var session = CanvasSession.Start("[canvas]\nenable = true\ngrid = \"always\"\nanimation_ms = 100\n");
        Assert.SkipWhen(session is null, "tinycomp or weston-simple-shm is not available beside the tests");
        session!.Send("grid-state");
        var before = Fields(session.WaitForLine("GRID output=HEADLESS-1 ")!);
        Assert.Equal("none", before["preset"]);
        Assert.Equal("off", before["density"]);

        session.Send("grid-effects curved");
        Assert.Equal("GRID-EFFECTS preset=curved", session.WaitForLine("GRID-EFFECTS "));
        session.Send("grid-state");
        var curved = Fields(session.WaitForLine("GRID output=HEADLESS-1 ")!);
        Assert.Equal("curved", curved["preset"]);
        Assert.Equal("on", curved["density"]);
        Assert.NotEqual("none", curved["glow"]);

        session.Send("reload");
        var reload = session.WaitForLine("RELOAD ");
        Assert.Contains("canvas-grid-glow=0.00", reload, StringComparison.Ordinal);
        Assert.Contains("overview-grid-pinch=0.00", reload, StringComparison.Ordinal);
        session.Send("grid-state");
        var after = Fields(session.WaitForLine("GRID output=HEADLESS-1 ")!);
        Assert.Equal("none", after["preset"]);
        Assert.Equal("off", after["density"]);
        Assert.Equal("none", after["glow"]);
    }
}
