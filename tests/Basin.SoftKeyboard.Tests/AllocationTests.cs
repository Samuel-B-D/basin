using Xunit;

namespace Basin.SoftKeyboard.Tests;

public sealed class AllocationTests
{
    [Fact]
    public void Press_move_release_and_repeat_allocate_nothing()
    {
        var engine = Keys.Engine();
        var count = 0;
        engine.Typed += _ => count++;
        engine.Pressed += _ => count++;
        engine.Released += _ => count++;
        engine.Clicked += _ => count++;
        var (ax, ay) = Keys.Center(engine, "a");
        var (sx, sy) = Keys.Center(engine, "s");
        var (bx, by) = Keys.Center(engine, "backspace");
        long now = 0;

        void Round()
        {
            engine.TouchDown(1, ax, ay, now);
            engine.Frame(now);
            engine.TouchMotion(1, sx, sy, now + 10);
            engine.TouchDown(2, ax, ay, now + 20);
            engine.Frame(now + 20);
            engine.TouchUp(1, now + 30);
            engine.TouchUp(2, now + 40);
            engine.TouchDown(3, bx, by, now + 50);
            engine.Frame(now + 50);
            for (var t = now + 50; t < now + 2500; t += 10)
            {
                engine.Tick(t);
            }

            engine.TouchUp(3, now + 2500);
            now += 3000;
        }

        for (var i = 0; i < 20; i++)
        {
            Round();
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++)
        {
            Round();
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(count > 0);
    }
}
