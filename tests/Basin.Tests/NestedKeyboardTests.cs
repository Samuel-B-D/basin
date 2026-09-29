using Basin.Backend.Wayland;
using Xunit;

namespace Basin.Tests;

public class NestedKeyboardTests
{
    private static readonly NestedParentOptions Typing = NestedParentOptions.Undecorating with { Keyboard = true };

    private static List<string> Record(NestedBackendTestHost host)
    {
        var events = new List<string>();
        host.Backend.KeyboardAdded += keyboard =>
        {
            keyboard.Key += (_, key, pressed) => events.Add($"key {key} {(pressed ? "down" : "up")}");
            keyboard.Enter += _ => events.Add("enter");
            keyboard.Leave += () => events.Add("leave");
        };
        return events;
    }

    [Fact]
    public void A_key_held_when_the_parent_takes_focus_away_is_released_before_the_leave()
    {
        using var host = new NestedBackendTestHost(Typing);
        _ = host.CreateOutput();
        var events = Record(host);
        host.Pump();
        host.Parent.Invoke(() =>
        {
            host.Parent.Seat!.Keyboard.NotifyEnter(host.Parent.Toplevels[0].Surface);
            host.Parent.Seat!.Keyboard.NotifyKey(10, InputCodes.KeyLeftMeta, pressed: true);
        });
        host.Pump();
        host.Parent.Invoke(() => host.Parent.Seat!.Keyboard.NotifyClearFocus());
        host.Pump();

        Assert.Equal(["enter", "key 125 down", "key 125 up", "leave"], events);
    }

    [Fact]
    public void Keys_the_parent_reports_held_on_enter_arrive_as_presses()
    {
        using var host = new NestedBackendTestHost(Typing);
        _ = host.CreateOutput();
        var events = Record(host);
        host.Pump();
        host.Parent.Invoke(() =>
        {
            host.Parent.Seat!.Keyboard.NotifyKey(10, InputCodes.KeyLeftShift, pressed: true);
            host.Parent.Seat!.Keyboard.NotifyEnter(host.Parent.Toplevels[0].Surface);
        });
        host.Pump();
        host.Parent.Invoke(() => host.Parent.Seat!.Keyboard.NotifyKey(20, InputCodes.KeyLeftShift, pressed: false));
        host.Pump();

        Assert.Equal(["enter", "key 42 down", "key 42 up"], events);
    }
}
