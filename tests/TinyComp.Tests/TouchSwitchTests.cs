using System.Globalization;
using System.Text.Json;
using Basin;
using Xunit;
using static TinyComp.Tests.PhysicsRows;

namespace TinyComp.Tests;

public sealed class TouchSwitchTests
{
    [Theory]
    [InlineData("titlebar_twist = false", "titlebar-twist")]
    [InlineData("window_twist = 0", "window-twist")]
    [InlineData("hold_move = false", "hold-move")]
    [InlineData("hold_ms = 1500", "hold-move")]
    [InlineData("hold_move_turn = false", "hold-turn")]
    public void Each_touch_gesture_obeys_its_switch(string setting, string gesture)
    {
        var ssdwin = CanvasRows.SsdWin();
        var decorations = gesture == "titlebar-twist" ? "server" : "none";
        using var session = ssdwin is null ? null : CanvasSession.Start($"[touch]\n{setting}\n", ssdwin, [decorations]);
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
}
