using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Basin.Diagnostics;
using Basin.Effects;
using Basin;
using Xunit;
using static TinyComp.Tests.CanvasRows;

namespace TinyComp.Tests;

public sealed class CanvasParkTests
{
    [Fact]
    public void The_grid_draws_over_a_wallpaper_that_maps_after_it()
    {
        using var session = CanvasSession.Start(
            "[canvas]\nenable = true\ngrid = \"always\"\nwallpaper = \"output\"\nanimation_ms = 100\n");
        Assert.SkipWhen(session is null, "tinycomp or weston-simple-shm is not available beside the tests");
        using var wallpaper = new TestWallpaper(session!.SocketPath, 0xFFFF0000);
        Assert.True(wallpaper.Width > 0 && wallpaper.Height > 0, "the wallpaper was configured");
        wallpaper.Pump();

        var shot = session.Shot("wallpaper");
        for (var i = 0; i < 20 && Colored(shot, 0, ZoneWidth, red: true) < shot.Height * ZoneWidth / 2; i++)
        {
            shot = session.Shot($"wallpaper-{i}");
        }

        Assert.True(Colored(shot, 0, ZoneWidth, red: true) >= shot.Height * ZoneWidth / 2, "the wallpaper covers the left zone");
        Assert.True(Colored(shot, 0, ZoneWidth, red: false) > 0, "the grid draws over the wallpaper in the left zone");
    }

    [Fact]
    public void Park_left_draws_the_client_inside_the_zone()
    {
        using var session = CanvasSession.Start();
        Assert.SkipWhen(session is null, "tinycomp or weston-simple-shm is not available beside the tests");
        var before = session!.Shot("before");
        Assert.True(Saturated(before, ZoneWidth, before.Width - ZoneWidth) > 0, "the client draws in the center before the park");

        session.Send("park left");
        session.WaitForLine("PARK ");
        var parked = session.Shot("parked");
        Assert.True(Saturated(parked, 0, ZoneWidth) > 0, "the client draws inside the left zone after the park");
        Assert.Equal(0, Saturated(parked, ZoneWidth, parked.Width - ZoneWidth));
    }

    [Fact]
    public void A_drag_through_the_seam_parks_the_client_in_the_zone()
    {
        using var session = CanvasSession.Start();
        Assert.SkipWhen(session is null, "tinycomp or weston-simple-shm is not available beside the tests");
        var (x, y) = session!.WindowPosition();
        session.Send($"move {x + 100} {y + 100}");
        session.Send("key 56 1");
        session.Send("button 272 1");
        for (var cursor = x + 100; cursor > 20; cursor -= 40)
        {
            session.Send($"move {cursor} {y + 100}");
        }

        session.Send("move 10 " + (y + 100));
        session.Send("button 272 0");
        session.Send("key 56 0");
        var dragged = session.Shot("dragged");
        Assert.True(Saturated(dragged, 0, ZoneWidth) > 0, "the client draws inside the left zone after the drag");
        Assert.Equal(0, Saturated(dragged, ZoneWidth, dragged.Width - ZoneWidth));
        var (parkedX, _) = session.WindowPosition();
        Assert.True(parkedX < 0, $"the window's canvas X is {parkedX}");
    }

    [Fact]
    public void Turning_the_canvas_off_recalls_a_parked_window_to_the_centre()
    {
        using var session = CanvasSession.Start();
        Assert.SkipWhen(session is null, "tinycomp or weston-simple-shm is not available beside the tests");
        var (homeX, _) = session!.WindowPosition();
        session.Send("park left");
        session.WaitForLine("PARK ");
        _ = session.Shot("parked");
        session.Send("canvas off");
        session.WaitForLine("CANVAS off");
        var line = session.WaitForLine("CANVAS view=0 left=0 right=0", timeoutMillis: 5000);
        Assert.NotNull(line);
        var recalled = session.Shot("recalled");
        Assert.Equal(0, Saturated(recalled, 0, ZoneWidth));
        Assert.True(Saturated(recalled, ZoneWidth, recalled.Width - ZoneWidth) > 0, "the client draws in the center again");
        Assert.Equal(homeX, session.WindowPosition().X);
    }

    [Fact]
    public void Park_up_then_left_reaches_the_corner()
    {
        using var session = CanvasSession.Start(FourSides);
        Assert.SkipWhen(session is null, "tinycomp or weston-simple-shm is not available beside the tests");
        session!.Send("park up");
        Assert.Contains(" up to=", session.WaitForLine("PARK "));
        session.Send("park left");
        var corner = session.WaitForLine("PARK ");
        Assert.Contains(" left to=", corner);
        Assert.EndsWith(" corner", corner);
        var parked = session.Shot("corner");
        Assert.True(Saturated(parked, 0, 0, ZoneWidth, ZoneHeight) > 0, "the client draws inside the top-left corner");
        Assert.Equal(0, Saturated(parked, ZoneWidth, ZoneHeight, parked.Width - (2 * ZoneWidth), parked.Height - (2 * ZoneHeight)));
        var (canvasX, canvasY, _, _, home) = session.CanvasWindow();
        Assert.True(canvasX < 0 && canvasY < 0, $"the window's canvas position is {canvasX},{canvasY}");
        Assert.NotEqual("none", home);
    }

    [Fact]
    public void A_square_corner_parks_the_window_at_the_far_corner()
    {
        using var session = CanvasSession.Start(FourSides + "corner = \"square\"\n");
        Assert.SkipWhen(session is null, "tinycomp or weston-simple-shm is not available beside the tests");
        session!.Send("park up");
        session.WaitForLine("PARK ");
        session.Send("park left");
        var corner = session.WaitForLine("PARK ");
        Assert.Contains($" left to={ZoneWidth - 640} y={ZoneHeight - 360} corner", corner);
        var parked = session.Shot("square");
        Assert.True(Saturated(parked, 0, 0, 12, 12) > 0, "a square corner draws the window into the screen corner");
    }

    [Fact]
    public void A_tapered_corner_parks_the_window_at_the_far_corner()
    {
        using var session = CanvasSession.Start(FourSides + "corner = \"taper\"\n");
        Assert.SkipWhen(session is null, "tinycomp or weston-simple-shm is not available beside the tests");
        session!.Send("park up");
        session.WaitForLine("PARK ");
        session.Send("park left");
        var corner = session.WaitForLine("PARK ");
        Assert.Contains($" left to={ZoneWidth - 640} y={ZoneHeight - 360} corner", corner);
        var parked = session.Shot("taper");
        Assert.True(Saturated(parked, 0, 0, 12, 12) > 0, "a tapered corner draws the window into the screen corner");
    }

    [Fact]
    public void Recall_returns_both_axes_home()
    {
        using var session = CanvasSession.Start(FourSides);
        Assert.SkipWhen(session is null, "tinycomp or weston-simple-shm is not available beside the tests");
        var home = session!.WindowPosition();
        session.Send("park up");
        session.WaitForLine("PARK ");
        session.Send("park left");
        session.WaitForLine("PARK ");
        _ = session.Shot("parked");
        Assert.NotEqual(home, session.WindowPosition());
        session.Send("recall");
        Assert.Contains($"to={home.X} y={home.Y}", session.WaitForLine("RECALL "));
        _ = session.Shot("recalled");
        Assert.Equal(home, session.WindowPosition());
    }

    [Fact]
    public void Park_up_is_refused_without_a_top_zone()
    {
        using var session = CanvasSession.Start();
        Assert.SkipWhen(session is null, "tinycomp or weston-simple-shm is not available beside the tests");
        var home = session!.WindowPosition();
        session.Send("park up");
        Assert.Contains("refused: no top zone", session.WaitForLine("PARK "));
        Assert.Equal(home, session.WindowPosition());
    }

    [Fact]
    public void A_bottom_right_rule_parks_on_both_axes()
    {
        using var session = CanvasSession.Start(
            FourSides + "[[rule]]\napp_id = \"org.freedesktop.weston.simple-shm\"\ncanvas = \"bottom-right\"\n");
        Assert.SkipWhen(session is null, "tinycomp or weston-simple-shm is not available beside the tests");
        _ = session!.Shot("ruled");
        var (x, y) = session.WindowPosition();
        Assert.True(x + 250 > 1280 - ZoneWidth && y + 250 > 720 - ZoneHeight, $"the window's canvas position is {x},{y}");
        Assert.True(x > 1280 - ZoneWidth - 250 + 100 && y > 720 - ZoneHeight - 250 + 100, $"the window's canvas position is {x},{y}");
    }

    [Fact]
    public void A_click_in_the_corner_reaches_the_client_at_true_surface_coordinates()
    {
        using var session = CanvasSession.Start(FourSides, SsdWin());
        Assert.SkipWhen(session is null, "tinycomp or ssdwin is not available beside the tests");
        session!.Send("park up");
        session.WaitForLine("PARK ");
        session.Send("park left");
        session.WaitForLine("PARK ");
        _ = session.Shot("corner");
        var (canvasX, canvasY, screen, _, _) = session.CanvasWindow();

        var map = LayoutMap();
        var clicks = new[] { (0.5, 0.5), (0.25, 0.75), (0.8, 0.3) };
        foreach (var (fx, fy) in clicks)
        {
            var x = Math.Round(screen.X + (screen.Width * fx), 2);
            var y = Math.Round(screen.Y + (screen.Height * fy), 2);
            var (expectedX, expectedY) = map.ToCanvasPoint(x, y);
            session.Send(string.Create(CultureInfo.InvariantCulture, $"move {x} {y}"));
            session.Send("button 272 1");
            session.Send("button 272 0");
            var line = session.WaitForClientLine("BUTTON ");
            Assert.NotNull(line);
            var parts = line!.Split(' ');
            var localX = double.Parse(parts[2], CultureInfo.InvariantCulture);
            var localY = double.Parse(parts[3], CultureInfo.InvariantCulture);
            Assert.True(
                Math.Abs(localX - (expectedX - canvasX)) < 1.0 && Math.Abs(localY - (expectedY - canvasY)) < 1.0,
                $"a click at ({x},{y}) reached ({localX},{localY}), the canvas says ({expectedX - canvasX:F2},{expectedY - canvasY:F2})");
        }
    }

    [Fact]
    public void Scale_mode_parks_a_flat_window_at_the_floor_inside_the_output()
    {
        using var session = CanvasSession.Start(ScaleMode);
        Assert.SkipWhen(session is null, "tinycomp or weston-simple-shm is not available beside the tests");
        session!.Send("park right");
        var park = session.WaitForLine("PARK ");
        Assert.Contains(" scale=0.350", park);
        var parked = session.Shot("scaled");
        Assert.True(Saturated(parked, parked.Width - ZoneWidth, ZoneWidth) > 0, "the client draws inside the right zone");
        Assert.Equal(0, Saturated(parked, 0, parked.Width - ZoneWidth));
        var (_, _, screen, deformed, home) = session.CanvasWindow();
        Assert.True(deformed);
        Assert.NotEqual("none", home);
        Assert.True(screen.Right <= 1280 && screen.Right >= 1278, $"the drawn box is {screen}");
        Assert.Equal(0.350, session.CanvasScale(), 2);

        session.Send("recall");
        session.WaitForLine("RECALL ");
        Thread.Sleep(400);
        Assert.Equal(1.0, session.CanvasScale(), 3);
    }
}
