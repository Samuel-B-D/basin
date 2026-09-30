using Basin.Seat.Backends;
using Xunit;

namespace Basin.Tests;

public sealed class ButtonLatchTests
{
    [Fact]
    public void Two_seat_buttons_each_reach_the_client_released()
    {
        using var host = new CompositorTestHost(200, 100);
        var seen = new List<(uint Button, bool Pressed)>();
        host.Seat.Pointer.Buttoned += (button, pressed) => seen.Add((button, pressed));
        var latch = new ButtonLatch(host.Seat);

        Assert.True(latch.Press());
        latch.SeatButton(1, InputCodes.BtnLeft, true);
        Assert.False(latch.Press());
        latch.SeatButton(2, InputCodes.BtnRight, true);
        latch.SeatButton(3, InputCodes.BtnLeft, false);
        Assert.False(latch.Release());
        latch.SeatButton(4, InputCodes.BtnRight, false);
        Assert.True(latch.Release());

        Assert.Equal(
            [(InputCodes.BtnLeft, true), (InputCodes.BtnRight, true), (InputCodes.BtnLeft, false), (InputCodes.BtnRight, false)],
            seen);
        Assert.False(host.Seat.Pointer.HasImplicitGrab);
    }

    [Fact]
    public void A_release_the_seat_never_saw_pressed_is_dropped()
    {
        using var host = new CompositorTestHost(200, 100);
        var seen = new List<(uint Button, bool Pressed)>();
        host.Seat.Pointer.Buttoned += (button, pressed) => seen.Add((button, pressed));
        var latch = new ButtonLatch(host.Seat);

        Assert.True(latch.Press());
        latch.SeatButton(1, InputCodes.BtnLeft, false);
        Assert.True(latch.Release());

        Assert.Empty(seen);
        Assert.False(latch.SeatHolds);
    }
}
