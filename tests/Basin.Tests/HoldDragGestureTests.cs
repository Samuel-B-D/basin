using Basin.Seat;
using Xunit;

namespace Basin.Tests;

public sealed class HoldDragGestureTests
{
    private sealed class Recorder(Func<double, double, bool> accepts) : IHoldDragHandler
    {
        public List<string> Log { get; } = [];

        public bool Accepts(double x, double y) => accepts(x, y);

        public bool Begin(double x, double y, uint timeMs)
        {
            Log.Add($"begin {x},{y}");
            return true;
        }

        public double Turned { get; private set; }

        public void Update(double x, double y, double radians, uint timeMs)
        {
            Turned += radians;
            Log.Add($"update {x},{y}");
        }

        public void End(bool cancelled, double radiansPerSecond, uint timeMs) => Log.Add($"end {(cancelled ? "true" : "false")}");
    }

    private static HoldDragGesture Pressed(Recorder handler)
    {
        var gesture = new HoldDragGesture { Handler = handler };
        Assert.Equal(TouchGestureVerdict.Pass, gesture.Down(1, 0, 100, 100));
        Assert.Equal(TouchGestureVerdict.Pass, gesture.Down(2, 5, 160, 100));
        return gesture;
    }

    [Fact]
    public void Two_fingers_held_then_slid_claim_and_follow_the_centroid()
    {
        var handler = new Recorder((_, _) => true);
        var gesture = Pressed(handler);

        Assert.Equal(TouchGestureVerdict.Pass, gesture.Motion(1, 200, 103, 101));
        Assert.Equal(TouchGestureVerdict.Claim, gesture.Motion(1, 420, 120, 100));
        Assert.Empty(handler.Log);
        Assert.Equal(TouchGestureVerdict.Owned, gesture.Motion(2, 430, 180, 100));
        Assert.Equal(TouchGestureVerdict.Finish, gesture.Up(1, 500));
        Assert.Equal(TouchGestureVerdict.Owned, gesture.Up(2, 510));

        Assert.Equal(["begin 130,100", "update 150,100", "end false"], handler.Log);
        Assert.False(gesture.IsClaimed);
    }

    [Fact]
    public void Fingers_turning_while_they_slide_report_the_turn()
    {
        var handler = new Recorder((_, _) => true);
        var gesture = Pressed(handler);
        Assert.Equal(TouchGestureVerdict.Claim, gesture.Motion(1, 400, 120, 100));
        for (var i = 1; i <= 20; i++)
        {
            var angle = 0.5 * i / 20;
            var centerX = 145 + (i * 5);
            var (cos, sin) = (Math.Cos(angle), Math.Sin(angle));
            _ = gesture.Motion(1, (uint)(400 + (i * 8)), centerX - (30 * cos), 100 - (30 * sin));
            _ = gesture.Motion(2, (uint)(400 + (i * 8)), centerX + (30 * cos), 100 + (30 * sin));
        }

        Assert.Equal(TouchGestureVerdict.Finish, gesture.Up(1, 570));
        Assert.Equal(0.5, handler.Turned, 2);
    }

    [Fact]
    public void A_lift_right_after_the_claim_begins_and_ends_nothing()
    {
        var handler = new Recorder((_, _) => true);
        var gesture = Pressed(handler);

        Assert.Equal(TouchGestureVerdict.Claim, gesture.Motion(1, 420, 130, 100));
        Assert.Equal(TouchGestureVerdict.Finish, gesture.Up(1, 430));
        Assert.Equal(TouchGestureVerdict.Owned, gesture.Up(2, 440));
        Assert.Empty(handler.Log);
    }

    [Fact]
    public void A_slide_before_the_hold_stays_with_the_app()
    {
        var handler = new Recorder((_, _) => true);
        var gesture = Pressed(handler);

        Assert.Equal(TouchGestureVerdict.Pass, gesture.Motion(1, 100, 130, 100));
        Assert.Equal(TouchGestureVerdict.Pass, gesture.Motion(1, 500, 200, 100));
        Assert.Empty(handler.Log);
    }

    [Fact]
    public void Fingers_off_a_window_never_claim()
    {
        var handler = new Recorder((x, _) => x < 150);
        var gesture = Pressed(handler);

        Assert.Equal(TouchGestureVerdict.Pass, gesture.Motion(1, 500, 200, 100));
        Assert.Empty(handler.Log);
    }

    [Fact]
    public void A_nudge_after_the_hold_is_not_a_move()
    {
        var handler = new Recorder((_, _) => true);
        var gesture = Pressed(handler);

        Assert.Equal(TouchGestureVerdict.Pass, gesture.Motion(1, 500, 110, 100));
        Assert.Equal(TouchGestureVerdict.Pass, gesture.Up(1, 600));
        Assert.Empty(handler.Log);
    }

    [Fact]
    public void A_third_finger_during_the_hold_gives_up()
    {
        var handler = new Recorder((_, _) => true);
        var gesture = Pressed(handler);
        Assert.Equal(TouchGestureVerdict.Pass, gesture.Down(3, 100, 300, 300));

        Assert.Equal(TouchGestureVerdict.Pass, gesture.Motion(1, 500, 200, 100));
        Assert.Empty(handler.Log);
    }
}
