using System.Globalization;
using System.Text.Json;
using Basin;
using Xunit;
using static TinyComp.Tests.PhysicsRows;

namespace TinyComp.Tests;

public sealed class PhysicsRotationTests
{
    [Fact]
    public void A_manual_rotation_without_physics_maps_clicks_and_straightens()
    {
        var ssdwin = CanvasRows.SsdWin();
        using var session = ssdwin is null ? null : CanvasSession.Start(string.Empty, ssdwin, ["none"]);
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
        var ssdwin = CanvasRows.SsdWin();
        var config = engine == "off" ? string.Empty : PhysicsOn(engine);
        using var session = ssdwin is null ? null : CanvasSession.Start(config, ssdwin, ["none"]);
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
    [MemberData(nameof(PhysicsRows.Engines), MemberType = typeof(PhysicsRows))]
    public void A_manual_rotation_survives_a_throw_until_upright(string engine)
    {
        using var session = CanvasSession.Start(PhysicsOn(engine));
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
}
