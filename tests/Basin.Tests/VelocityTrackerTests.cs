using Basin.Seat;
using Xunit;

namespace Basin.Tests;

public sealed class VelocityTrackerTests
{
    private const long Ms = 1_000_000;

    [Fact]
    public void Steady_motion_reports_its_speed()
    {
        var tracker = new VelocityTracker();
        for (var i = 0; i <= 10; i++)
        {
            tracker.Add(i * 10, i * -5, i * 10 * Ms);
        }

        var (x, y) = tracker.Velocity(100 * Ms);
        Assert.Equal(1000, x, 6);
        Assert.Equal(-500, y, 6);
    }

    [Fact]
    public void A_pause_before_release_gives_zero()
    {
        var tracker = new VelocityTracker();
        tracker.Add(0, 0, 0);
        tracker.Add(50, 0, 10 * Ms);

        Assert.NotEqual(0, tracker.Velocity(20 * Ms).X);
        Assert.Equal((0.0, 0.0), tracker.Velocity(70 * Ms));
    }

    [Fact]
    public void A_single_sample_has_no_velocity()
    {
        var tracker = new VelocityTracker();
        tracker.Add(100, 100, 0);

        Assert.False(tracker.HasVelocity);
        Assert.Equal((0.0, 0.0), tracker.Velocity(0));
    }

    [Fact]
    public void Smoothing_matches_the_exponential_average()
    {
        var tracker = new VelocityTracker();
        tracker.Add(0, 0, 0);
        tracker.Add(10, 0, 10 * Ms);
        tracker.Add(40, 0, 20 * Ms);

        Assert.Equal(1000 + (VelocityTracker.Smoothing * (3000 - 1000)), tracker.Velocity(20 * Ms).X, 6);
    }

    [Fact]
    public void A_sample_at_the_same_time_replaces_the_newest_position()
    {
        var tracker = new VelocityTracker();
        tracker.Add(0, 0, 0);
        tracker.Add(10, 0, 10 * Ms);
        tracker.Add(20, 0, 10 * Ms);
        tracker.Add(30, 0, 20 * Ms);

        Assert.Equal(3, tracker.Count);
        Assert.Equal(1000, tracker.Velocity(20 * Ms).X, 6);
    }

    [Fact]
    public void A_gap_longer_than_the_cutoff_restarts_the_average()
    {
        var tracker = new VelocityTracker();
        tracker.Add(0, 0, 0);
        tracker.Add(100, 0, 10 * Ms);
        tracker.Add(100, 0, 500 * Ms);

        Assert.False(tracker.HasVelocity);
        tracker.Add(110, 0, 510 * Ms);
        Assert.Equal(1000, tracker.Velocity(510 * Ms).X, 6);
    }

    [Fact]
    public void A_minimum_span_measures_across_several_samples()
    {
        var tracker = new VelocityTracker { MinSpanNanos = 8 * Ms };
        var x = 0.0;
        for (var i = 0; i <= 20; i++)
        {
            x += i % 2 == 0 ? 1 : 3;
            tracker.Add(x, 0, i * Ms);
        }

        Assert.Equal(2000, tracker.Velocity(20 * Ms).X, 0);
    }

    [Fact]
    public void Reset_forgets_everything()
    {
        var tracker = new VelocityTracker();
        tracker.Add(0, 0, 0);
        tracker.Add(10, 0, 10 * Ms);
        tracker.Reset();

        Assert.Equal(0, tracker.Count);
        Assert.Equal((0.0, 0.0), tracker.Velocity(10 * Ms));
    }

    [Fact]
    public void Adding_samples_allocates_nothing()
    {
        var tracker = new VelocityTracker { MinSpanNanos = 4 * Ms };
        tracker.Add(0, 0, 0);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 1; i < 1000; i++)
        {
            tracker.Add(i, i, i * Ms);
            _ = tracker.Velocity(i * Ms);
        }

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }
}
