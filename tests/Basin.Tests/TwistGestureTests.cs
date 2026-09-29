using Basin.Seat;
using Xunit;

namespace Basin.Tests;

public sealed class TwistGestureTests
{
    private sealed class Recorder(Func<double, double, bool> accepts) : ITwistHandler
    {
        public List<double> Turns { get; } = [];

        public (double X, double Y)? Anchor { get; private set; }

        public (bool Cancelled, double Spin)? Ended { get; private set; }

        public bool Accepts(double x, double y) => accepts(x, y);

        public bool Begin(double anchorX, double anchorY, uint timeMs)
        {
            Anchor = (anchorX, anchorY);
            return true;
        }

        public void Update(double radians, uint timeMs) => Turns.Add(radians);

        public void End(bool cancelled, double radiansPerSecond, uint timeMs) => Ended = (cancelled, radiansPerSecond);
    }

    private static (double X, double Y) On(double centerX, double centerY, double radius, double angle, int finger) =>
        (centerX + (radius * Math.Cos(angle + (finger * Math.PI))), centerY + (radius * Math.Sin(angle + (finger * Math.PI))));

    private static TouchGestureVerdict Twist(TwistGesture gesture, double to, int steps, uint start = 0, uint stepMs = 8)
    {
        var verdict = TouchGestureVerdict.Pass;
        for (var i = 1; i <= steps; i++)
        {
            var angle = to * i / steps;
            var time = start + (uint)(i * stepMs);
            var (ax, ay) = On(500, 300, 100, angle, 0);
            var (bx, by) = On(500, 300, 100, angle, 1);
            var first = gesture.Motion(1, time, ax, ay);
            verdict = first == TouchGestureVerdict.Claim ? first : gesture.Motion(2, time, bx, by);
            if (first == TouchGestureVerdict.Claim)
            {
                _ = gesture.Motion(2, time, bx, by);
            }
        }

        return verdict;
    }

    private static void Down(TwistGesture gesture)
    {
        var (ax, ay) = On(500, 300, 100, 0, 0);
        var (bx, by) = On(500, 300, 100, 0, 1);
        Assert.Equal(TouchGestureVerdict.Pass, gesture.Down(1, 0, ax, ay));
        Assert.Equal(TouchGestureVerdict.Pass, gesture.Down(2, 0, bx, by));
    }

    [Fact]
    public void A_twist_past_the_slop_claims_and_reports_every_turn()
    {
        var handler = new Recorder((x, _) => x > 550);
        var gesture = new TwistGesture { Handler = handler };
        Down(gesture);

        var sawClaim = false;
        for (var i = 1; i <= 20; i++)
        {
            var angle = 0.6 * i / 20;
            var (ax, ay) = On(500, 300, 100, angle, 0);
            var (bx, by) = On(500, 300, 100, angle, 1);
            sawClaim |= gesture.Motion(1, (uint)(i * 8), ax, ay) == TouchGestureVerdict.Claim;
            sawClaim |= gesture.Motion(2, (uint)(i * 8), bx, by) == TouchGestureVerdict.Claim;
        }

        Assert.True(sawClaim);
        Assert.Equal((600.0, 300.0), handler.Anchor);
        Assert.Equal(0.6, handler.Turns.Sum(), 6);
        Assert.Equal(TouchGestureVerdict.Finish, gesture.Up(1, 170));
        Assert.Equal(TouchGestureVerdict.Owned, gesture.Up(2, 170));
        Assert.False(handler.Ended!.Value.Cancelled);
        Assert.InRange(handler.Ended.Value.Spin, 3, 5);
    }

    [Fact]
    public void Without_an_accepted_contact_a_twist_passes_to_the_client()
    {
        var handler = new Recorder((_, _) => false);
        var gesture = new TwistGesture { Handler = handler };
        Down(gesture);

        Assert.Equal(TouchGestureVerdict.Pass, Twist(gesture, 1.0, 20));
        Assert.Empty(handler.Turns);
        Assert.Null(handler.Anchor);
    }

    [Fact]
    public void A_small_turn_stays_with_the_client()
    {
        var handler = new Recorder((_, _) => true);
        var gesture = new TwistGesture { Handler = handler, Slop = 0.2 };
        Down(gesture);

        Assert.Equal(TouchGestureVerdict.Pass, Twist(gesture, 0.15, 10));
        Assert.Null(handler.Anchor);
    }

    private static (double X, double Y) Spoke(double centerX, double centerY, double angle, int finger) =>
        (centerX + (80 * Math.Cos(angle + (finger * Math.Tau / 3))), centerY + (80 * Math.Sin(angle + (finger * Math.Tau / 3))));

    [Fact]
    public void Three_fingers_turning_together_claim_and_report_the_turn()
    {
        var handler = new Recorder((_, _) => true);
        var gesture = new TwistGesture { Handler = handler, Fingers = 3 };
        for (var f = 0; f < 3; f++)
        {
            var (x, y) = Spoke(500, 300, 0, f);
            Assert.Equal(TouchGestureVerdict.Pass, gesture.Down(f + 1, 0, x, y));
        }

        var sawClaim = false;
        for (var i = 1; i <= 20; i++)
        {
            for (var f = 0; f < 3; f++)
            {
                var (x, y) = Spoke(500, 300, -0.8 * i / 20, f);
                sawClaim |= gesture.Motion(f + 1, (uint)(i * 8), x, y) == TouchGestureVerdict.Claim;
            }
        }

        Assert.True(sawClaim);
        Assert.Equal(-0.8, handler.Turns.Sum(), 6);
        Assert.Equal(TouchGestureVerdict.Finish, gesture.Up(1, 170));
    }

    [Fact]
    public void Three_fingers_sliding_never_claim_even_with_a_wobble()
    {
        var handler = new Recorder((_, _) => true);
        var gesture = new TwistGesture { Handler = handler, Fingers = 3 };
        (double X, double Y)[] start = [(400, 300), (440, 290), (480, 305)];
        for (var f = 0; f < 3; f++)
        {
            Assert.Equal(TouchGestureVerdict.Pass, gesture.Down(f + 1, 0, start[f].X, start[f].Y));
        }

        for (var i = 1; i <= 30; i++)
        {
            for (var f = 0; f < 3; f++)
            {
                var wobble = f == 1 ? 6 * Math.Sin(i * 0.9) : 0;
                Assert.Equal(TouchGestureVerdict.Pass, gesture.Motion(f + 1, (uint)(i * 8), start[f].X + (i * 12), start[f].Y + wobble));
            }
        }

        Assert.Null(handler.Anchor);
    }

    [Fact]
    public void Two_fingers_sliding_on_the_frame_do_not_claim()
    {
        var handler = new Recorder((_, _) => true);
        var gesture = new TwistGesture { Handler = handler };
        Assert.Equal(TouchGestureVerdict.Pass, gesture.Down(1, 0, 400, 300));
        Assert.Equal(TouchGestureVerdict.Pass, gesture.Down(2, 0, 460, 300));
        for (var i = 1; i <= 20; i++)
        {
            Assert.Equal(TouchGestureVerdict.Pass, gesture.Motion(1, (uint)(i * 8), 400 + (i * 15), 300 + (i * 3)));
            Assert.Equal(TouchGestureVerdict.Pass, gesture.Motion(2, (uint)(i * 8), 460 + (i * 15), 300 + (i * 4)));
        }

        Assert.Null(handler.Anchor);
    }

    private sealed class SwipeRecorder : ICentroidSwipeHandler
    {
        public bool Began { get; private set; }

        public bool Begin(double centroidX, double centroidY, uint timeMs) => Began = true;

        public void Update(double dx, double dy, uint timeMs)
        {
        }

        public void End(bool cancelled, uint timeMs)
        {
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Beside_the_workspace_swipe_three_fingers_slide_or_turn_but_not_both(bool turn)
    {
        var twist = new Recorder((_, _) => true);
        var swipe = new SwipeRecorder();
        var set = new TouchGestureSet(
            new CentroidSwipeGesture { Fingers = 3, Slop = 24, Handler = swipe },
            new TwistGesture { Handler = twist, Fingers = 3 });
        for (var f = 0; f < 3; f++)
        {
            var (x, y) = Spoke(500, 300, 0, f);
            _ = set.Down(f + 1, 0, x, y);
        }

        for (var i = 1; i <= 30; i++)
        {
            for (var f = 0; f < 3; f++)
            {
                var (x, y) = turn ? Spoke(500, 300, 0.9 * i / 30, f) : Spoke(500 + (i * 10), 300 + (f == 1 ? 5 * Math.Sin(i) : 0), 0, f);
                _ = set.Motion(f + 1, (uint)(i * 8), x, y);
            }
        }

        Assert.Equal(turn, twist.Anchor is not null);
        Assert.Equal(!turn, swipe.Began);
    }

    [Fact]
    public void A_third_finger_before_the_claim_gives_up()
    {
        var handler = new Recorder((_, _) => true);
        var gesture = new TwistGesture { Handler = handler };
        Down(gesture);
        _ = gesture.Down(3, 1, 10, 10);

        Assert.Equal(TouchGestureVerdict.Pass, Twist(gesture, 1.0, 20));
        Assert.Null(handler.Anchor);
    }

    [Fact]
    public void A_paused_release_carries_no_spin_and_a_cancel_says_so()
    {
        var handler = new Recorder((_, _) => true);
        var gesture = new TwistGesture { Handler = handler };
        Down(gesture);
        _ = Twist(gesture, 0.8, 20);

        _ = gesture.Up(1, 20 * 8 + 200);
        Assert.Equal(0, handler.Ended!.Value.Spin);

        var cancelled = new Recorder((_, _) => true);
        var again = new TwistGesture { Handler = cancelled };
        Down(again);
        _ = Twist(again, 0.8, 20);
        again.Cancel();
        Assert.True(cancelled.Ended!.Value.Cancelled);
    }
}
