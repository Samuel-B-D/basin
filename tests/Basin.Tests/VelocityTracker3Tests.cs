using System.Numerics;
using Basin.Kinematics;
using Xunit;

namespace Basin.Tests;

public sealed class VelocityTracker3Tests
{
    [Fact]
    public void A_steady_motion_reads_its_velocity_until_it_goes_stale()
    {
        var tracker = new VelocityTracker3();
        for (var i = 0; i < 6; i++)
        {
            tracker.Add(new Vector3(i * 0.1f, 0, -i * 0.05f), i * 16_000_000L);
        }

        var velocity = tracker.Velocity(5 * 16_000_000L);
        Assert.Equal(6.25f, velocity.X, 2);
        Assert.Equal(-3.125f, velocity.Z, 2);
        Assert.Equal(Vector3.Zero, tracker.Velocity((5 * 16_000_000L) + VelocityTracker3.StaleNanos));
    }

    [Fact]
    public void A_gap_longer_than_stale_starts_over()
    {
        var tracker = new VelocityTracker3();
        tracker.Add(Vector3.Zero, 0);
        tracker.Add(Vector3.One, 10_000_000);
        tracker.Add(new Vector3(5), 10_000_000 + VelocityTracker3.StaleNanos);
        Assert.False(tracker.HasVelocity);
        Assert.Equal(1, tracker.Count);
    }
}
