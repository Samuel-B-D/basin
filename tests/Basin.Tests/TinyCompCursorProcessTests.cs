using Xunit;
using TinyCompSession = Basin.Tests.TinyCompCanvasProcessTests.CanvasSession;

namespace Basin.Tests;

public sealed class TinyCompCursorProcessTests
{
    private static string Hidden(TinyCompSession session)
    {
        session.Send("stats");
        var line = session.WaitForLine("STATS cursor ");
        Assert.NotNull(line);
        var field = line!.Split(' ').FirstOrDefault(part => part.StartsWith("hidden=", StringComparison.Ordinal));
        Assert.NotNull(field);
        return field!["hidden=".Length..];
    }

    private static void Tap(TinyCompSession session, double x, double y) =>
        session.SendPaced(
            [
                $"ipc input/touch {{\"kind\":\"down\",\"id\":1,\"x\":{x},\"y\":{y}}}",
                "ipc input/touch {\"kind\":\"frame\"}",
                "ipc input/touch {\"kind\":\"up\",\"id\":1}",
                "ipc input/touch {\"kind\":\"frame\"}",
            ],
            8);

    [Fact]
    public void A_touch_hides_the_cursor_and_the_mouse_brings_it_back()
    {
        using var session = TinyCompSession.Start(string.Empty);
        Assert.SkipWhen(session is null, "tinycomp or weston-simple-shm is not available beside the tests");
        Assert.Equal("no", Hidden(session!));

        Tap(session!, 900, 600);
        Assert.Equal("yes", Hidden(session!));

        session!.Send("move 500 400");
        Assert.Equal("no", Hidden(session));

        Tap(session, 900, 600);
        Assert.Equal("yes", Hidden(session));
        session.Send("button 272 1");
        session.Send("button 272 0");
        Assert.Equal("no", Hidden(session));
    }

    [Fact]
    public void With_the_key_off_a_touch_leaves_the_cursor_showing()
    {
        using var session = TinyCompSession.Start("[touch]\nhide_cursor = false\n");
        Assert.SkipWhen(session is null, "tinycomp or weston-simple-shm is not available beside the tests");

        Tap(session!, 900, 600);
        Assert.Equal("no", Hidden(session!));
    }
}
