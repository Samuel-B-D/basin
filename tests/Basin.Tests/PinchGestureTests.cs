using Basin.Seat;
using Xunit;

namespace Basin.Tests;

public sealed class PinchGestureTests
{
    private sealed class Recorder(Func<double, double, bool> accepts) : IPinchHandler
    {
        public List<double> Scales { get; } = [];

        public (double X, double Y, double Spread)? Began { get; private set; }

        public double? EndVelocity { get; private set; }

        public bool Cancelled { get; private set; }

        public bool Accepts(double x, double y) => accepts(x, y);

        public bool Begin(double centroidX, double centroidY, double spread, uint timeMs)
        {
            Began = (centroidX, centroidY, spread);
            return true;
        }

        public void Update(double scale, double centroidX, double centroidY, uint timeMs) => Scales.Add(scale);

        public void End(double scalePerSecond, uint timeMs) => EndVelocity = scalePerSecond;

        public void Cancel() => Cancelled = true;
    }

    private static void Down(ITouchGestures gesture, double spread = 200)
    {
        Assert.Equal(TouchGestureVerdict.Pass, gesture.Down(1, 0, 500 - (spread / 2), 300));
        Assert.Equal(TouchGestureVerdict.Pass, gesture.Down(2, 0, 500 + (spread / 2), 300));
    }

    private static bool Spread(ITouchGestures gesture, double from, double to, int steps, uint stepMs = 8)
    {
        var claimed = false;
        for (var i = 1; i <= steps; i++)
        {
            var spread = from + ((to - from) * i / steps);
            var time = (uint)(i * stepMs);
            claimed |= gesture.Motion(1, time, 500 - (spread / 2), 300) == TouchGestureVerdict.Claim;
            claimed |= gesture.Motion(2, time, 500 + (spread / 2), 300) == TouchGestureVerdict.Claim;
        }

        return claimed;
    }

    [Fact]
    public void A_spread_past_the_slop_claims_and_reports_the_scale()
    {
        var handler = new Recorder((_, _) => true);
        var gesture = new PinchGesture { Handler = handler };
        Down(gesture);

        Assert.True(Spread(gesture, 200, 400, 20));
        Assert.Equal((500.0, 300.0, 200.0), handler.Began);
        Assert.Equal(2.0, handler.Scales[^1], 6);
        Assert.Equal(2.0, gesture.Scale, 6);
        Assert.Equal(TouchGestureVerdict.Finish, gesture.Up(1, 168));
        Assert.Equal(TouchGestureVerdict.Owned, gesture.Up(2, 168));
        Assert.InRange(handler.EndVelocity!.Value, 5, 8);
        Assert.False(handler.Cancelled);
    }

    [Fact]
    public void A_pinch_in_reports_a_scale_below_one_and_a_negative_velocity()
    {
        var handler = new Recorder((_, _) => true);
        var gesture = new PinchGesture { Handler = handler };
        Down(gesture, 400);

        Assert.True(Spread(gesture, 400, 200, 20));
        Assert.Equal(0.5, handler.Scales[^1], 6);
        _ = gesture.Up(1, 168);
        Assert.True(handler.EndVelocity < -1);
    }

    [Fact]
    public void A_change_inside_the_slop_stays_with_the_client()
    {
        var handler = new Recorder((_, _) => true);
        var gesture = new PinchGesture { Handler = handler, Slop = 30 };
        Down(gesture);

        Assert.False(Spread(gesture, 200, 225, 10));
        Assert.Null(handler.Began);
        Assert.Equal(TouchGestureVerdict.Pass, gesture.Up(1, 100));
    }

    [Fact]
    public void Without_an_accepted_contact_a_pinch_passes_to_the_client()
    {
        var handler = new Recorder((_, _) => false);
        var gesture = new PinchGesture { Handler = handler };
        Down(gesture);

        Assert.False(Spread(gesture, 200, 500, 20));
        Assert.Null(handler.Began);
    }

    [Fact]
    public void Two_fingers_sliding_together_do_not_claim()
    {
        var handler = new Recorder((_, _) => true);
        var gesture = new PinchGesture { Handler = handler };
        Down(gesture);
        for (var i = 1; i <= 30; i++)
        {
            Assert.Equal(TouchGestureVerdict.Pass, gesture.Motion(1, (uint)(i * 8), 400 + (i * 15), 300 + (i * 2)));
            Assert.Equal(TouchGestureVerdict.Pass, gesture.Motion(2, (uint)(i * 8), 600 + (i * 15) + (i % 3), 300 + (i * 3)));
        }

        Assert.Null(handler.Began);
    }

    [Fact]
    public void One_finger_still_and_one_moving_is_a_pinch()
    {
        var handler = new Recorder((_, _) => true);
        var gesture = new PinchGesture { Handler = handler };
        Down(gesture);
        var claimed = false;
        for (var i = 1; i <= 20; i++)
        {
            claimed |= gesture.Motion(2, (uint)(i * 8), 600 + (i * 5), 300) == TouchGestureVerdict.Claim;
        }

        Assert.True(claimed);
        Assert.Equal(1.5, handler.Scales[^1], 6);
    }

    [Fact]
    public void A_third_finger_after_the_claim_cancels()
    {
        var handler = new Recorder((_, _) => true);
        var gesture = new PinchGesture { Handler = handler };
        Down(gesture);
        Assert.True(Spread(gesture, 200, 300, 10));
        var count = handler.Scales.Count;

        Assert.Equal(TouchGestureVerdict.Owned, gesture.Down(3, 90, 100, 100));
        Assert.True(handler.Cancelled);
        Assert.Equal(TouchGestureVerdict.Owned, gesture.Motion(1, 100, 300, 300));
        Assert.Equal(count, handler.Scales.Count);
        Assert.Equal(TouchGestureVerdict.Owned, gesture.Up(1, 110));
        Assert.Null(handler.EndVelocity);
    }

    [Fact]
    public void A_third_finger_before_the_claim_gives_up()
    {
        var handler = new Recorder((_, _) => true);
        var gesture = new PinchGesture { Handler = handler };
        Down(gesture);
        _ = gesture.Down(3, 1, 10, 10);

        Assert.False(Spread(gesture, 200, 500, 20));
        Assert.Null(handler.Began);
    }

    [Fact]
    public void A_paused_release_carries_no_velocity()
    {
        var handler = new Recorder((_, _) => true);
        var gesture = new PinchGesture { Handler = handler };
        Down(gesture);
        _ = Spread(gesture, 200, 400, 20);

        _ = gesture.Up(1, (20 * 8) + 200);
        Assert.Equal(0, handler.EndVelocity);
    }

    private sealed class TwistRecorder : ITwistHandler
    {
        public bool Began { get; private set; }

        public bool Accepts(double x, double y) => true;

        public bool Begin(double anchorX, double anchorY, uint timeMs) => Began = true;

        public void Update(double radians, uint timeMs)
        {
        }

        public void End(bool cancelled, double radiansPerSecond, uint timeMs)
        {
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Beside_a_twist_two_fingers_pinch_or_turn_but_not_both(bool pinch)
    {
        var pinchHandler = new Recorder((_, _) => true);
        var twistHandler = new TwistRecorder();
        var set = new TouchGestureSet(new TwistGesture { Handler = twistHandler }, new PinchGesture { Handler = pinchHandler });
        Down(set);

        for (var i = 1; i <= 30; i++)
        {
            double ax, ay, bx, by;
            if (pinch)
            {
                var spread = 200 + (i * 6) + (i % 2);
                (ax, ay, bx, by) = (500 - (spread / 2), 300 + (i % 3), 500 + (spread / 2), 300);
            }
            else
            {
                var angle = 0.9 * i / 30;
                (ax, ay) = (500 - (100 * Math.Cos(angle)), 300 - (100 * Math.Sin(angle)));
                (bx, by) = (500 + (101 * Math.Cos(angle)), 300 + (101 * Math.Sin(angle)));
            }

            _ = set.Motion(1, (uint)(i * 8), ax, ay);
            _ = set.Motion(2, (uint)(i * 8), bx, by);
        }

        Assert.Equal(pinch, pinchHandler.Began is not null);
        Assert.Equal(!pinch, twistHandler.Began);
    }

    [Fact]
    public void A_claimed_pinch_allocates_nothing_per_sample()
    {
        var handler = new Recorder((_, _) => true);
        handler.Scales.Capacity = 4096;
        var gesture = new PinchGesture { Handler = handler };
        Down(gesture);
        _ = Spread(gesture, 200, 300, 10);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 11; i <= 1000; i++)
        {
            var spread = 300 + (i % 50);
            _ = gesture.Motion(1, (uint)(i * 8), 500 - (spread / 2.0), 300);
            _ = gesture.Motion(2, (uint)(i * 8), 500 + (spread / 2.0), 300);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
