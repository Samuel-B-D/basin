using System.Globalization;
using System.Text.Json;
using Xunit;
using TinyCompSession = Basin.Tests.TinyCompCanvasProcessTests.CanvasSession;

namespace Basin.Tests;

public sealed class TinyCompPhysicsProcessTests
{
    private const string Physics = "[physics]\nenable = true\n";

    public static TheoryData<string> Engines => new() { "bepu", "box2d" };

    private static string PhysicsOn(string engine) => $"{Physics}engine = \"{engine}\"\n";

    private const string Canvas = "[canvas]\nenable = true\ngrid = \"never\"\nanimation_ms = 100\n";

    private static Dictionary<string, string> Body(TinyCompSession session)
    {
        session.Send("where");
        var line = session.WaitForLine("PHYS ");
        Assert.NotNull(line);
        return Fields(line!);
    }

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

    private static Dictionary<string, string> Rest(TinyCompSession session, int timeoutMillis = 10000)
    {
        var deadline = Environment.TickCount64 + timeoutMillis;
        while (true)
        {
            var body = Body(session);
            if (body.GetValueOrDefault("awake") == "no" || Environment.TickCount64 > deadline)
            {
                return body;
            }

            Thread.Sleep(150);
        }
    }

    private static double Number(Dictionary<string, string> fields, string key) =>
        double.Parse(fields[key], CultureInfo.InvariantCulture);

    private static ulong[] WindowIds(TinyCompSession session)
    {
        session.Send("ipc windows/list {}");
        var line = session.WaitForLine("OK windows/list ");
        Assert.NotNull(line);
        var json = line!["OK windows/list windows=".Length..];
        using var document = JsonDocument.Parse(json);
        return [.. document.RootElement.EnumerateArray().Select(window => window.GetProperty("id").GetUInt64())];
    }

    [Theory]
    [MemberData(nameof(Engines))]
    public void A_throw_comes_to_rest_integral_and_upright(string engine)
    {
        using var session = TinyCompSession.Start(PhysicsOn(engine));
        Assert.SkipWhen(session is null, "tinycomp or weston-simple-shm is not available beside the tests");
        var (startX, startY) = session!.WindowPosition();

        session.Send("throw 2500 600");
        Assert.StartsWith("THROW ", session.WaitForLine("THROW "));
        var rest = Rest(session);

        Assert.Equal("no", rest["awake"]);
        Assert.Equal(0, Number(rest, "angle"));
        var (x, y) = session.WindowPosition();
        Assert.True(x > startX + 200, $"the window moved from {startX} to {x}");
        Assert.True(y > startY, $"the window moved from {startY} to {y}");
        Assert.InRange(x + Number(rest, "w"), 0, 1280);
    }

    [Theory]
    [MemberData(nameof(Engines))]
    public void A_pointer_drag_throws_with_the_release_velocity(string engine)
    {
        using var session = TinyCompSession.Start(PhysicsOn(engine));
        Assert.SkipWhen(session is null, "tinycomp or weston-simple-shm is not available beside the tests");
        var (startX, startY) = session!.WindowPosition();
        var grabX = startX + 80;
        var grabY = startY + 80;
        var drag = new List<string> { $"move {grabX} {grabY}", "key 56 1", "button 272 1" };
        for (var i = 1; i <= 12; i++)
        {
            drag.Add($"move {grabX + (i * 20)} {grabY + (i * 4)}");
        }

        drag.Add("button 272 0");
        drag.Add("key 56 0");
        session.SendPaced(drag, 8);
        _ = Rest(session);

        var (x, _) = session.WindowPosition();
        Assert.True(x > startX + 240 + 100, $"released at {startX + 240}, came to rest at {x}");
    }

    [Theory]
    [MemberData(nameof(Engines))]
    public void Gravity_pulls_a_window_to_the_floor_and_config_restores_it(string engine)
    {
        using var session = TinyCompSession.Start(PhysicsOn(engine));
        Assert.SkipWhen(session is null, "tinycomp or weston-simple-shm is not available beside the tests");

        session.Send("gravity 3000");
        Assert.Equal("GRAVITY 3000", session.WaitForLine("GRAVITY "));
        session.Send("throw 0 1");
        var rest = Rest(session);
        var (_, y) = session.WindowPosition();
        Assert.InRange(y + Number(rest, "h"), 718, 720);

        session.Send("gravity config");
        Assert.Equal("GRAVITY config", session.WaitForLine("GRAVITY "));
    }

    [Theory]
    [MemberData(nameof(Engines))]
    public void A_frozen_world_holds_a_thrown_window(string engine)
    {
        using var session = TinyCompSession.Start(PhysicsOn(engine));
        Assert.SkipWhen(session is null, "tinycomp or weston-simple-shm is not available beside the tests");
        var start = session!.WindowPosition();

        session.Send("freeze on");
        Assert.Equal("PHYSICS frozen=yes", session.WaitForLine("PHYSICS "));
        session.Send("throw 3000 0");
        Thread.Sleep(500);
        Assert.Equal(start, session.WindowPosition());

        session.Send("freeze off");
        _ = Rest(session);
        Assert.NotEqual(start, session.WindowPosition());
    }

    [Theory]
    [MemberData(nameof(Engines))]
    public void A_click_on_a_rotated_window_reaches_the_client_at_its_surface_coordinates(string engine)
    {
        var ssdwin = TinyCompCanvasProcessTests.SsdWin();
        using var session = ssdwin is null ? null : TinyCompSession.Start(PhysicsOn(engine), ssdwin, ["none"]);
        Assert.SkipWhen(session is null, "tinycomp or ssdwin is not available beside the tests");

        session!.Send("throw 200 0 3");
        session.WaitForLine("THROW ");
        Thread.Sleep(120);
        session.Send("freeze on");
        Assert.Equal("PHYSICS frozen=yes", session.WaitForLine("PHYSICS "));
        var body = Body(session);
        var angle = Number(body, "angle");
        Assert.True(Math.Abs(angle) > 0.05, $"the window turned {angle} before the freeze");

        var (x, y) = session.WindowPosition();
        var half = Number(body, "w") / 2;
        foreach (var (dx, dy) in new[] { (60.0, 0.0), (-30.0, 70.0), (40.0, -50.0) })
        {
            var cos = Math.Cos(angle);
            var sin = Math.Sin(angle);
            var screenX = Math.Round(x + half + (cos * dx) - (sin * dy), 2);
            var screenY = Math.Round(y + half + (sin * dx) + (cos * dy), 2);
            session.Send(string.Create(CultureInfo.InvariantCulture, $"move {screenX} {screenY}"));
            session.Send("button 272 1");
            session.Send("button 272 0");
            var line = session.WaitForClientLine("BUTTON ");
            Assert.NotNull(line);
            var parts = line!.Split(' ');
            var localX = double.Parse(parts[2], CultureInfo.InvariantCulture);
            var localY = double.Parse(parts[3], CultureInfo.InvariantCulture);
            Assert.True(
                Math.Abs(localX - (half + dx)) < 1.0 && Math.Abs(localY - (half + dy)) < 1.0,
                $"angle {angle}: clicked {dx},{dy} from the center, the client saw {localX},{localY}");
        }
    }

    [Theory]
    [MemberData(nameof(Engines))]
    public void A_park_blocked_by_another_window_still_arrives(string engine)
    {
        using var session = TinyCompSession.Start(Canvas + PhysicsOn(engine));
        Assert.SkipWhen(session is null, "tinycomp or weston-simple-shm is not available beside the tests");
        session!.Spawn("weston-simple-shm");
        Assert.NotNull(session.WaitForLine("MAPPED ", 5000));
        var ids = WindowIds(session);
        Assert.Equal(2, ids.Length);
        session.Send($"ipc windows/move {{\"id\":{ids[0]},\"x\":0,\"y\":100}}");

        session.Send("park left");
        var line = session.WaitForLine("PARK ", 15000);
        Assert.NotNull(line);
        var fields = Fields(line!);
        Assert.True(Number(fields, "guided") > 0, line);
        var target = int.Parse(fields["to"], CultureInfo.InvariantCulture);
        Thread.Sleep(300);
        session.Send("where");
        var parked = new List<int>();
        for (var i = 0; i < 2; i++)
        {
            var win = session.WaitForLine("WIN ");
            Assert.NotNull(win);
            parked.Add(int.Parse(win!.Split(' ')[2], CultureInfo.InvariantCulture));
        }

        Assert.Contains(target, parked);
    }

    [Theory]
    [MemberData(nameof(Engines))]
    public void A_throw_into_a_terrace_shelf_moves_to_the_shelf_world_and_stacks_at_the_anchor(string engine)
    {
        using var session = TinyCompSession.Start(Canvas + "window = \"terrace\"\n" + PhysicsOn(engine));
        Assert.SkipWhen(session is null, "tinycomp or weston-simple-shm is not available beside the tests");

        session!.Send("throw -4000 300");
        session.WaitForLine("THROW ");
        var rest = Rest(session);

        Assert.Equal("shelf-left", rest["world"]);
        var (_, y) = session.WindowPosition();
        Assert.Equal(0, y);
    }

    [Theory]
    [MemberData(nameof(Engines))]
    public void A_grid_drop_comes_to_rest_on_a_grid_cell(string engine)
    {
        using var session = TinyCompSession.Start(Canvas + "drag = \"grid\"\ngrid_cell = 64\n" + PhysicsOn(engine));
        Assert.SkipWhen(session is null, "tinycomp or weston-simple-shm is not available beside the tests");
        var (startX, startY) = session!.WindowPosition();
        var grabX = startX + 100;
        var grabY = startY + 100;
        var drag = new List<string> { $"move {grabX} {grabY}", "key 56 1", "button 272 1" };
        for (var i = 1; i <= 10; i++)
        {
            drag.Add($"move {grabX + (i * 9)} {grabY + (i * 4)}");
        }

        drag.Add("button 272 0");
        drag.Add("key 56 0");
        session.SendPaced(drag, 8);

        var line = session.WaitForLine("SNAP ", 10000);
        Assert.NotNull(line);
        _ = Rest(session);
        var (x, y) = session.WindowPosition();
        Assert.True(x % 64 == 0 && y % 64 == 0, $"rested at {x},{y} after {line}");
    }

    [Theory]
    [MemberData(nameof(Engines))]
    public void A_hard_grid_throw_at_an_edge_rests_on_a_cell_the_window_fits(string engine)
    {
        using var session = TinyCompSession.Start(Canvas + "drag = \"grid\"\ngrid_cell = 64\n" + PhysicsOn(engine));
        Assert.SkipWhen(session is null, "tinycomp or weston-simple-shm is not available beside the tests");
        var (startX, startY) = session!.WindowPosition();
        var grabX = startX + 100;
        var grabY = startY + 100;
        var drag = new List<string> { $"move {grabX} {grabY}", "key 56 1", "button 272 1" };
        for (var i = 1; i <= 6; i++)
        {
            drag.Add($"move {grabX + (i * 80)} {grabY + (i * 40)}");
        }

        drag.Add("button 272 0");
        drag.Add("key 56 0");
        session.SendPaced(drag, 8);

        var line = session.WaitForLine("SNAP ", 10000);
        Assert.NotNull(line);
        var rest = Rest(session);
        var (x, y) = session.WindowPosition();
        Assert.True(x % 64 == 0 && y % 64 == 0, $"rested at {x},{y} after {line}");
        Assert.Equal("no", rest.GetValueOrDefault("awake"));
    }

    [Fact]
    public void A_reload_that_changes_the_engine_rebinds_the_windows_where_they_rest()
    {
        using var session = TinyCompSession.Start(PhysicsOn("box2d"));
        Assert.SkipWhen(session is null, "tinycomp or weston-simple-shm is not available beside the tests");
        session!.Send("throw 900 0");
        session.WaitForLine("THROW ");
        _ = Rest(session);
        var before = session.WindowPosition();
        session.Send("physics-state");
        Assert.Contains("engine=box2d", session.WaitForLine("PHYSICS "));

        session.Rewrite(PhysicsOn("bepu"));
        session.Send("reload");
        Assert.Contains("physics-engine=bepu", session.WaitForLine("RELOAD "));
        session.Send("physics-state");
        var state = session.WaitForLine("PHYSICS ");
        Assert.Contains("engine=bepu", state);
        Assert.Contains("bodies=1", state);
        Assert.Equal(before, session.WindowPosition());

        session.Send("throw 900 0");
        session.WaitForLine("THROW ");
        _ = Rest(session);
        Assert.NotEqual(before, session.WindowPosition());
    }

    [Theory]
    [MemberData(nameof(Engines))]
    public void Physics_state_reports_every_body_as_json(string engine)
    {
        using var session = TinyCompSession.Start(PhysicsOn(engine));
        Assert.SkipWhen(session is null, "tinycomp or weston-simple-shm is not available beside the tests");
        session!.Send("throw 10 0");
        session.WaitForLine("THROW ");

        session.Send("ipc tinycomp/physics-state {}");
        var line = session.WaitForLine("OK tinycomp/physics-state ");
        Assert.NotNull(line);
        Assert.Contains("enabled=true", line, StringComparison.Ordinal);
        Assert.Contains("\"kind\":\"dynamic\"", line, StringComparison.Ordinal);
    }

    [Fact]
    public void A_manual_rotation_without_physics_maps_clicks_and_straightens()
    {
        var ssdwin = TinyCompCanvasProcessTests.SsdWin();
        using var session = ssdwin is null ? null : TinyCompSession.Start(string.Empty, ssdwin, ["none"]);
        Assert.SkipWhen(session is null, "tinycomp or ssdwin is not available beside the tests");

        session!.Send("rotate 30");
        Assert.StartsWith("ROTATE ", session.WaitForLine("ROTATE "));
        Thread.Sleep(800);
        session.Send("where");
        var tilt = session.WaitForLine("TILT ");
        Assert.NotNull(tilt);
        Assert.Equal(30, Number(Fields(tilt!), "angle"), 2);

        var (x, y) = session.WindowPosition();
        var angle = Math.PI / 6;
        const double Half = 128;
        foreach (var (dx, dy) in new[] { (60.0, 0.0), (-40.0, 50.0) })
        {
            var screenX = Math.Round(x + Half + (Math.Cos(angle) * dx) - (Math.Sin(angle) * dy), 2);
            var screenY = Math.Round(y + Half + (Math.Sin(angle) * dx) + (Math.Cos(angle) * dy), 2);
            session.Send(string.Create(CultureInfo.InvariantCulture, $"move {screenX} {screenY}"));
            session.Send("button 272 1");
            session.Send("button 272 0");
            var line = session.WaitForClientLine("BUTTON ");
            Assert.NotNull(line);
            var parts = line!.Split(' ');
            Assert.InRange(double.Parse(parts[2], CultureInfo.InvariantCulture), Half + dx - 1, Half + dx + 1);
            Assert.InRange(double.Parse(parts[3], CultureInfo.InvariantCulture), Half + dy - 1, Half + dy + 1);
        }

        session.Send("upright");
        Assert.StartsWith("UPRIGHT ", session.WaitForLine("UPRIGHT "));
        Thread.Sleep(800);
        session.Send("where");
        session.WaitForLine("WIN ");
        Assert.Null(session.WaitForLine("TILT ", 500));
    }

    [Theory]
    [InlineData("fullscreen", "off")]
    [InlineData("maximized", "off")]
    [InlineData("fullscreen", "bepu")]
    [InlineData("maximized", "box2d")]
    public void A_rotated_window_straightens_when_it_fills_the_output(string state, string engine)
    {
        var ssdwin = TinyCompCanvasProcessTests.SsdWin();
        var config = engine == "off" ? string.Empty : PhysicsOn(engine);
        using var session = ssdwin is null ? null : TinyCompSession.Start(config, ssdwin, ["none"]);
        Assert.SkipWhen(session is null, "tinycomp or ssdwin is not available beside the tests");

        session!.Send("rotate 30");
        Assert.StartsWith("ROTATE ", session.WaitForLine("ROTATE "));
        Thread.Sleep(800);
        session.Send("where");
        session.WaitForLine("WIN ");
        Assert.NotNull(session.WaitForLine("TILT ", 1000));

        var id = WindowIds(session)[0];
        session.Send($"ipc windows/set-state {{\"id\":{id},\"{state}\":true}}");
        Assert.StartsWith("UPRIGHT ", session.WaitForLine("UPRIGHT "));
        Thread.Sleep(800);
        session.Send("where");
        session.WaitForLine("WIN ");
        if (engine != "off")
        {
            var body = Fields(session.WaitForLine("PHYS ")!);
            if (body.ContainsKey("angle"))
            {
                Assert.Equal(0, Number(body, "angle"));
            }
        }

        Assert.Null(session.WaitForLine("TILT ", 500));
        if (state == "fullscreen")
        {
            Assert.Equal((0, 0), session.WindowPosition());
        }
    }

    [Theory]
    [MemberData(nameof(Engines))]
    public void A_manual_rotation_survives_a_throw_until_upright(string engine)
    {
        using var session = TinyCompSession.Start(PhysicsOn(engine));
        Assert.SkipWhen(session is null, "tinycomp or weston-simple-shm is not available beside the tests");

        session!.Send("rotate-by 20");
        session.WaitForLine("ROTATE ");
        Thread.Sleep(600);
        session.Send("throw 1500 0");
        session.WaitForLine("THROW ");
        var rest = Rest(session);
        Assert.Equal(20 * Math.PI / 180, Number(rest, "angle"), 3);

        session.Send("upright");
        session.WaitForLine("UPRIGHT ");
        Thread.Sleep(800);
        Assert.Equal(0, Number(Body(session), "angle"));
    }

    private static List<string> TouchTwist(double centerX, double centerY, double radius, double degrees, int steps = 24, int fingers = 2)
    {
        var lines = new List<string>();
        for (var i = 0; i <= steps; i++)
        {
            var angle = degrees * Math.PI / 180 * i / steps;
            for (var finger = 0; finger < fingers; finger++)
            {
                var x = centerX + (radius * Math.Cos(angle + (finger * Math.Tau / fingers)));
                var y = centerY + (radius * Math.Sin(angle + (finger * Math.Tau / fingers)));
                lines.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"ipc input/touch {{\"kind\":\"{(i == 0 ? "down" : "motion")}\",\"id\":{finger + 1},\"x\":{x:F2},\"y\":{y:F2}}}"));
            }

            lines.Add("ipc input/touch {\"kind\":\"frame\"}");
        }

        for (var finger = 0; finger < fingers; finger++)
        {
            lines.Add($"ipc input/touch {{\"kind\":\"up\",\"id\":{finger + 1}}}");
        }

        lines.Add("ipc input/touch {\"kind\":\"frame\"}");
        return lines;
    }

    private static (double X, double Y) Titlebar(TinyCompSession session)
    {
        session.Send("ipc windows/list {}");
        var line = session.WaitForLine("OK windows/list ");
        Assert.NotNull(line);
        using var document = JsonDocument.Parse(line!["OK windows/list windows=".Length..]);
        var window = document.RootElement[0];
        var frame = window.GetProperty("geometry");
        var client = window.GetProperty("client_geometry");
        var top = frame.GetProperty("y").GetDouble();
        return (
            frame.GetProperty("x").GetDouble() + (frame.GetProperty("width").GetDouble() / 2),
            top + ((client.GetProperty("y").GetDouble() - top) / 2));
    }

    private static Dictionary<string, string>? Tilt(TinyCompSession session)
    {
        session.Send("where");
        _ = session.WaitForLine("WIN ");
        var line = session.WaitForLine("TILT ", 1000);
        return line is null ? null : Fields(line);
    }

    [Theory]
    [MemberData(nameof(Engines))]
    public void A_touch_drag_on_the_titlebar_released_while_moving_throws_the_window(string engine)
    {
        var ssdwin = TinyCompCanvasProcessTests.SsdWin();
        using var session = ssdwin is null ? null : TinyCompSession.Start(PhysicsOn(engine), ssdwin, ["server"]);
        Assert.SkipWhen(session is null, "tinycomp or ssdwin is not available beside the tests");
        var (x, y) = Titlebar(session!);
        var (startX, _) = session!.WindowPosition();
        _ = session.WaitForLine("PHYS ");
        var lines = new List<string>();
        for (var i = 0; i <= 10; i++)
        {
            lines.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"ipc input/touch {{\"kind\":\"{(i == 0 ? "down" : "motion")}\",\"id\":1,\"x\":{x + (i * 20):F2},\"y\":{y:F2}}}"));
            lines.Add("ipc input/touch {\"kind\":\"frame\"}");
        }

        lines.Add("ipc input/touch {\"kind\":\"up\",\"id\":1}");
        lines.Add("ipc input/touch {\"kind\":\"frame\"}");
        session.SendPaced(lines, 8);

        var flying = Body(session);
        Assert.Equal("yes", flying.GetValueOrDefault("awake"));
        Assert.True(Number(flying, "vx") > 200, $"released at vx={flying.GetValueOrDefault("vx")}");
        _ = Rest(session);
        var (restX, _) = session.WindowPosition();
        Assert.True(restX > startX + 260, $"the throw carried the window from {startX} to {restX}");
    }

    [Fact]
    public void A_two_finger_twist_on_the_titlebar_turns_the_window_and_holds()
    {
        var ssdwin = TinyCompCanvasProcessTests.SsdWin();
        using var session = ssdwin is null ? null : TinyCompSession.Start(string.Empty, ssdwin, ["server"]);
        Assert.SkipWhen(session is null, "tinycomp or ssdwin is not available beside the tests");
        var (x, y) = Titlebar(session!);

        session!.SendPaced(TouchTwist(x, y, 50, 30), 8);
        Assert.StartsWith("TWIST ", session.WaitForLine("TWIST "));
        Thread.Sleep(1000);
        var tilt = Tilt(session);

        Assert.NotNull(tilt);
        Assert.InRange(Number(tilt!, "angle"), 29, 31);
        Assert.Equal("yes", tilt["held"]);
        Assert.Equal("no", tilt["spinning"]);
    }

    [Fact]
    public void A_three_finger_twist_on_the_client_turns_the_window()
    {
        var ssdwin = TinyCompCanvasProcessTests.SsdWin();
        using var session = ssdwin is null ? null : TinyCompSession.Start(string.Empty, ssdwin, ["none"]);
        Assert.SkipWhen(session is null, "tinycomp or ssdwin is not available beside the tests");
        var (x, y) = session!.WindowPosition();

        session.SendPaced(TouchTwist(x + 128, y + 128, 60, -40, fingers: 3), 8);
        Assert.StartsWith("TWIST ", session.WaitForLine("TWIST "));
        Thread.Sleep(1000);
        var tilt = Tilt(session);

        Assert.NotNull(tilt);
        Assert.InRange(Number(tilt!, "angle"), -41, -39);
    }

    [Theory]
    [InlineData(true, "off")]
    [InlineData(false, "off")]
    [InlineData(true, "bepu")]
    public void Two_fingers_move_a_window_only_after_a_hold(bool hold, string engine)
    {
        var ssdwin = TinyCompCanvasProcessTests.SsdWin();
        var config = engine == "off" ? string.Empty : PhysicsOn(engine);
        using var session = ssdwin is null ? null : TinyCompSession.Start(config, ssdwin, ["none"]);
        Assert.SkipWhen(session is null, "tinycomp or ssdwin is not available beside the tests");
        var (startX, startY) = session!.WindowPosition();
        var (ax, bx, y) = (startX + 90, startX + 150, startY + 128);

        string Touch(string kind, int id, double x) =>
            string.Create(CultureInfo.InvariantCulture, $"ipc input/touch {{\"kind\":\"{kind}\",\"id\":{id},\"x\":{x},\"y\":{y}}}");

        session.SendPaced([Touch("down", 1, ax), Touch("down", 2, bx), "ipc input/touch {\"kind\":\"frame\"}"], 8);
        if (hold)
        {
            Thread.Sleep(450);
        }

        var slide = new List<string>();
        for (var i = 1; i <= 10; i++)
        {
            slide.Add(Touch("motion", 1, ax + (i * 20)));
            slide.Add(Touch("motion", 2, bx + (i * 20)));
            slide.Add("ipc input/touch {\"kind\":\"frame\"}");
        }

        slide.Add(Touch("up", 1, 0).Replace(",\"x\":0", string.Empty, StringComparison.Ordinal));
        slide.Add("ipc input/touch {\"kind\":\"up\",\"id\":2}");
        slide.Add("ipc input/touch {\"kind\":\"frame\"}");
        session.SendPaced(slide, 30);
        Thread.Sleep(1500);

        var began = session.WaitForLine("HOLDMOVE ", hold ? 1000 : 500);
        var (endX, _) = session.WindowPosition();
        if (hold)
        {
            Assert.StartsWith("HOLDMOVE ", began);
            Assert.True(endX >= startX + 180, $"the hold-move carried the window from {startX} to {endX}");
        }
        else
        {
            Assert.Null(began);
            Assert.Equal(startX, endX);
        }
    }

    [Theory]
    [InlineData("off")]
    [InlineData("bepu")]
    public void A_held_two_finger_slide_that_turns_moves_and_rotates_the_window(string engine)
    {
        var ssdwin = TinyCompCanvasProcessTests.SsdWin();
        var config = engine == "off" ? string.Empty : PhysicsOn(engine);
        using var session = ssdwin is null ? null : TinyCompSession.Start(config, ssdwin, ["none"]);
        Assert.SkipWhen(session is null, "tinycomp or ssdwin is not available beside the tests");
        var (startX, startY) = session!.WindowPosition();
        var (grabX, grabY) = (startX + 128.0, startY + 128.0);

        string Touch(string kind, int id, double x, double y) =>
            string.Create(CultureInfo.InvariantCulture, $"ipc input/touch {{\"kind\":\"{kind}\",\"id\":{id},\"x\":{x:F2},\"y\":{y:F2}}}");

        session.SendPaced([Touch("down", 1, grabX - 30, grabY), Touch("down", 2, grabX + 30, grabY), "ipc input/touch {\"kind\":\"frame\"}"], 8);
        Thread.Sleep(450);
        var lines = new List<string>();
        const int Steps = 15;
        for (var i = 1; i <= Steps; i++)
        {
            var angle = (Math.PI / 6) * i / Steps;
            var (cx, cy) = (grabX + (i * 20), grabY + (i * 6));
            lines.Add(Touch("motion", 1, cx - (30 * Math.Cos(angle)), cy - (30 * Math.Sin(angle))));
            lines.Add(Touch("motion", 2, cx + (30 * Math.Cos(angle)), cy + (30 * Math.Sin(angle))));
            lines.Add("ipc input/touch {\"kind\":\"frame\"}");
        }

        session.SendPaced(lines, 16);
        Thread.Sleep(300);
        session.SendPaced(["ipc input/touch {\"kind\":\"up\",\"id\":1}", "ipc input/touch {\"kind\":\"up\",\"id\":2}", "ipc input/touch {\"kind\":\"frame\"}"], 8);

        var end = session.WaitForLine("HOLDMOVE end", 2000);
        Assert.NotNull(end);
        Assert.InRange(Number(Fields(end!), "angle"), 28, 32);
        Thread.Sleep(1500);
        var tilt = Tilt(session);
        Assert.NotNull(tilt);
        Assert.InRange(Number(tilt!, "angle"), 28, 32);
        Assert.Equal("yes", tilt["held"]);

        var (endX, endY) = session.WindowPosition();
        var pivot = (endX + 128 - (grabX + (Steps * 20)), endY + 128 - (grabY + (Steps * 6)));
        Assert.True(Math.Abs(pivot.Item1) <= 8 && Math.Abs(pivot.Item2) <= 8, $"the grabbed center drifted {pivot} from the fingers");
    }

    [Theory]
    [InlineData("titlebar_twist = false", "titlebar-twist")]
    [InlineData("window_twist = 0", "window-twist")]
    [InlineData("hold_move = false", "hold-move")]
    [InlineData("hold_ms = 1500", "hold-move")]
    [InlineData("hold_move_turn = false", "hold-turn")]
    public void Each_touch_gesture_obeys_its_switch(string setting, string gesture)
    {
        var ssdwin = TinyCompCanvasProcessTests.SsdWin();
        var decorations = gesture == "titlebar-twist" ? "server" : "none";
        using var session = ssdwin is null ? null : TinyCompSession.Start($"[touch]\n{setting}\n", ssdwin, [decorations]);
        Assert.SkipWhen(session is null, "tinycomp or ssdwin is not available beside the tests");
        var (startX, startY) = session!.WindowPosition();

        string Touch(string kind, int id, double x, double y) =>
            string.Create(CultureInfo.InvariantCulture, $"ipc input/touch {{\"kind\":\"{kind}\",\"id\":{id},\"x\":{x:F2},\"y\":{y:F2}}}");

        switch (gesture)
        {
            case "titlebar-twist":
                var (tx, ty) = Titlebar(session);
                session.SendPaced(TouchTwist(tx, ty, 50, 30), 8);
                break;
            case "window-twist":
                session.SendPaced(TouchTwist(startX + 128, startY + 128, 60, -40, fingers: 3), 8);
                break;
            default:
                var (gx, gy) = (startX + 128.0, startY + 128.0);
                session.SendPaced([Touch("down", 1, gx - 30, gy), Touch("down", 2, gx + 30, gy), "ipc input/touch {\"kind\":\"frame\"}"], 8);
                Thread.Sleep(450);
                var lines = new List<string>();
                for (var i = 1; i <= 15; i++)
                {
                    var angle = gesture == "hold-turn" ? (Math.PI / 6) * i / 15 : 0;
                    var (cx, cy) = (gx + (i * 20), gy);
                    lines.Add(Touch("motion", 1, cx - (30 * Math.Cos(angle)), cy - (30 * Math.Sin(angle))));
                    lines.Add(Touch("motion", 2, cx + (30 * Math.Cos(angle)), cy + (30 * Math.Sin(angle))));
                    lines.Add("ipc input/touch {\"kind\":\"frame\"}");
                }

                lines.Add("ipc input/touch {\"kind\":\"up\",\"id\":1}");
                lines.Add("ipc input/touch {\"kind\":\"up\",\"id\":2}");
                lines.Add("ipc input/touch {\"kind\":\"frame\"}");
                session.SendPaced(lines, 16);
                break;
        }

        Thread.Sleep(1200);
        var moved = session.WaitForLine("HOLDMOVE ", 300);
        var twisted = session.WaitForLine("TWIST ", 300);
        var tilt = Tilt(session);
        var (endX, _) = session.WindowPosition();
        if (gesture == "hold-turn")
        {
            Assert.NotNull(moved);
            Assert.True(endX > startX + 200, $"the move went from {startX} to {endX}");
            Assert.Null(tilt);
        }
        else
        {
            Assert.Null(moved);
            Assert.Null(twisted);
            Assert.Null(tilt);
            Assert.True(Math.Abs(endX - startX) <= 1, $"the window moved from {startX} to {endX}");
        }
    }

    [Fact]
    public void A_twist_on_the_client_stays_with_the_client()
    {
        var ssdwin = TinyCompCanvasProcessTests.SsdWin();
        using var session = ssdwin is null ? null : TinyCompSession.Start(string.Empty, ssdwin, ["server"]);
        Assert.SkipWhen(session is null, "tinycomp or ssdwin is not available beside the tests");
        var (x, _) = session!.WindowPosition();

        session.SendPaced(TouchTwist(x + 128, 198, 50, 40), 8);
        Thread.Sleep(500);
        Assert.Null(Tilt(session));
    }

    [Fact]
    public void A_spun_release_keeps_turning_until_stop_spin_catches_it()
    {
        var ssdwin = TinyCompCanvasProcessTests.SsdWin();
        using var session = ssdwin is null
            ? null
            : TinyCompSession.Start("[effects]\nrotate_release = \"spin\"\n", ssdwin, ["server"]);
        Assert.SkipWhen(session is null, "tinycomp or ssdwin is not available beside the tests");
        var (x, y) = Titlebar(session!);

        session!.SendPaced(TouchTwist(x, y, 50, 30, steps: 16), 8);
        session.WaitForLine("TWIST ");
        session.Send("stop-spin");
        var stop = session.WaitForLine("STOPSPIN ");
        Assert.NotNull(stop);
        Assert.DoesNotContain(" idle", stop, StringComparison.Ordinal);

        var caught = Number(Tilt(session)!, "angle");
        Assert.True(caught > 30.5, $"the spin carried past the fingers to {caught}");
        Thread.Sleep(600);
        var after = Tilt(session)!;
        Assert.Equal(caught, Number(after, "angle"), 2);
        Assert.Equal("no", after["spinning"]);
    }
}
