using System.Globalization;
using System.Text.Json;
using Basin;
using Xunit;
using static TinyComp.Tests.PhysicsRows;

namespace TinyComp.Tests;

public sealed class PhysicsGridTests
{
    [Theory]
    [MemberData(nameof(PhysicsRows.Engines), MemberType = typeof(PhysicsRows))]
    public void A_park_blocked_by_another_window_still_arrives(string engine)
    {
        using var session = CanvasSession.Start(Canvas + PhysicsOn(engine));
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
    [MemberData(nameof(PhysicsRows.Engines), MemberType = typeof(PhysicsRows))]
    public void A_throw_into_a_terrace_shelf_moves_to_the_shelf_world_and_stacks_at_the_anchor(string engine)
    {
        using var session = CanvasSession.Start(Canvas + "window = \"terrace\"\n" + PhysicsOn(engine));
        Assert.SkipWhen(session is null, "tinycomp or weston-simple-shm is not available beside the tests");

        session!.Send("throw -4000 300");
        session.WaitForLine("THROW ");
        var rest = Rest(session);

        Assert.Equal("shelf-left", rest["world"]);
        var (_, y) = session.WindowPosition();
        Assert.Equal(0, y);
    }

    [Theory]
    [MemberData(nameof(PhysicsRows.Engines), MemberType = typeof(PhysicsRows))]
    public void A_grid_drop_comes_to_rest_on_a_grid_cell(string engine)
    {
        using var session = CanvasSession.Start(Canvas + "drag = \"grid\"\ngrid_cell = 64\n" + PhysicsOn(engine));
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
    [MemberData(nameof(PhysicsRows.Engines), MemberType = typeof(PhysicsRows))]
    public void A_hard_grid_throw_at_an_edge_rests_on_a_cell_the_window_fits(string engine)
    {
        using var session = CanvasSession.Start(Canvas + "drag = \"grid\"\ngrid_cell = 64\n" + PhysicsOn(engine));
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
}
