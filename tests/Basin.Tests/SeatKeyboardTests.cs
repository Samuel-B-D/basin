using Wayland;
using Xunit;

namespace Basin.Tests;

public sealed class SeatKeyboardTests
{
    [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "close")]
    private static extern int LibcClose(int fd);

    [Fact]
    public void Two_devices_with_different_repeat_send_repeat_info_on_a_switch_only()
    {
        using var host = new CompositorTestHost();
        host.Seat.Keyboard.SetKeymap();
        host.Seat.Keyboard.SetRepeatInfo(25, 600);
        var client = host.Client;
        var keyboard = client.Seat!.GetKeyboard();
        var repeats = new List<(int Rate, int Delay)>();
        var keymaps = 0;
        keyboard.Keymap += (_, e) =>
        {
            keymaps++;
            LibcClose(e.Fd);
        };
        keyboard.RepeatInfo += (_, e) => repeats.Add((e.Rate, e.Delay));
        host.PumpUntil(() => keymaps == 1 && repeats.Count == 1);
        Assert.Equal((25, 600), repeats[0]);

        var slow = host.Seat.Keyboard.CreateDevice();
        slow.SetRepeatInfo(10, 500);
        var plain = host.Seat.Keyboard.CreateDevice();

        host.Seat.Keyboard.Activate(slow);
        host.Seat.Keyboard.NotifyKey(10, 30, WlKeyboard.KeyState.Pressed);
        host.PumpUntil(() => repeats.Count == 2);
        Assert.Equal((10, 500), repeats[1]);

        host.Seat.Keyboard.Activate(slow);
        host.Seat.Keyboard.NotifyKey(20, 30, WlKeyboard.KeyState.Released);
        host.PumpToClient();
        host.PumpToClient();
        Assert.Equal(2, repeats.Count);

        host.Seat.Keyboard.Activate(plain);
        host.PumpUntil(() => repeats.Count == 3);
        Assert.Equal((25, 600), repeats[2]);
        Assert.Equal((25, 600), host.Seat.Keyboard.RepeatInfo);

        host.Seat.Keyboard.Activate(null);
        host.PumpToClient();
        host.PumpToClient();
        Assert.Equal(3, repeats.Count);

        slow.Dispose();
        plain.Dispose();
    }

    [Fact]
    public void A_device_keymap_by_name_applies_while_it_is_active_and_clears_back_to_the_seat()
    {
        using var host = new CompositorTestHost();
        host.Seat.Keyboard.SetKeymap(new Basin.Capabilities.KeymapNames(Layout: "us"));
        var device = host.Seat.Keyboard.CreateDevice();

        Assert.True(device.SetKeymap(new Basin.Capabilities.KeymapNames(Layout: "de")));
        host.Seat.Keyboard.Activate(device);
        Assert.Equal("z", host.Seat.Keyboard.KeysymFor(21).ToString());

        device.ClearKeymap();
        Assert.Equal("y", host.Seat.Keyboard.KeysymFor(21).ToString());

        host.Seat.Keyboard.Activate(device);
        Assert.Equal("y", host.Seat.Keyboard.KeysymFor(21).ToString());
        device.Dispose();
    }
}
