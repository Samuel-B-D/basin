using Xunit;

namespace Basin.SoftKeyboard.Tests;

public sealed class TraceEngineTests
{
    [Fact]
    public void Thirty_five_px_of_travel_from_a_letter_turns_the_touch_into_a_trace()
    {
        var engine = Keys.Engine();
        engine.TraceEnabled = true;
        var typed = Keys.Record(engine);
        var ended = 0;
        engine.TraceEnded += () => ended++;
        var (x, y) = Keys.Center(engine, "h");
        engine.TouchDown(1, x, y, 0);
        engine.Frame(0);
        engine.TouchMotion(1, x + 34, y, 10);
        Assert.False(engine.IsTracing);
        engine.TouchMotion(1, x + 36, y, 20);
        Assert.True(engine.IsTracing);
        for (var i = 1; i <= 20; i++)
        {
            engine.TouchMotion(1, x + 36 + (i * 10), y - (i * 3), 20 + i);
        }

        Assert.True(engine.TracePath.Length > 10);
        engine.TouchUp(1, 100);
        Assert.Equal(1, ended);
        Assert.Empty(typed);
        Assert.False(engine.IsTracing);
    }

    [Fact]
    public void Without_trace_the_same_travel_slides_and_types_a_key()
    {
        var engine = Keys.Engine();
        var typed = Keys.Record(engine);
        var (x, y) = Keys.Center(engine, "h");
        engine.TouchDown(1, x, y, 0);
        engine.Frame(0);
        engine.TouchMotion(1, x + 93, y, 20);
        engine.TouchUp(1, 40);
        Assert.Equal("j", Keys.Text(typed));
    }

    [Fact]
    public void A_trace_never_starts_from_a_function_key_or_with_a_second_finger_down()
    {
        var engine = Keys.Engine();
        engine.TraceEnabled = true;
        var (sx, sy) = Keys.Center(engine, " ");
        engine.TouchDown(1, sx, sy, 0);
        engine.Frame(0);
        engine.TouchMotion(1, sx + 100, sy, 10);
        Assert.False(engine.IsTracing);
        engine.TouchUp(1, 20);

        var (ax, ay) = Keys.Center(engine, "a");
        var (hx, hy) = Keys.Center(engine, "h");
        engine.TouchDown(2, ax, ay, 100);
        engine.Frame(100);
        engine.TouchDown(3, hx, hy, 110);
        engine.Frame(110);
        engine.TouchMotion(3, hx + 80, hy, 120);
        Assert.False(engine.IsTracing);
    }

    [Fact]
    public void Collecting_a_trace_allocates_nothing()
    {
        var engine = Keys.Engine();
        engine.TraceEnabled = true;
        var (x, y) = Keys.Center(engine, "h");
        void Round()
        {
            engine.TouchDown(1, x, y, 0);
            engine.Frame(0);
            for (var i = 0; i < 300; i++)
            {
                engine.TouchMotion(1, x + (float)(Math.Sin(i * 0.1) * 200), y + (float)(Math.Cos(i * 0.07) * 60), i);
            }

            engine.TouchUp(1, 400);
        }

        Round();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 20; i++)
        {
            Round();
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
