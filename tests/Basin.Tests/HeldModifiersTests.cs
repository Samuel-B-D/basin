using Basin.Capabilities;
using Basin.Config;
using Xunit;

namespace Basin.Tests;

public sealed class HeldModifiersTests
{
    [Fact]
    public void The_six_modifiers_come_from_the_xkb_state()
    {
        using var host = new CompositorTestHost();
        var keyboard = host.Seat.Keyboard;
        keyboard.SetKeymap(new KeymapNames(Layout: "us", Options: "lv3:ralt_switch"));
        Assert.Equal(Modifiers.None, HeldModifiers.From(keyboard.State));

        keyboard.NotifyKey(1, 42, true);
        keyboard.NotifyKey(2, 100, true);
        Assert.Equal(Modifiers.Shift | Modifiers.Mod5, HeldModifiers.From(keyboard.State));

        keyboard.NotifyKey(3, 100, false);
        keyboard.NotifyKey(4, 29, true);
        keyboard.NotifyKey(5, 125, true);
        Assert.Equal(Modifiers.Shift | Modifiers.Ctrl | Modifiers.Super, HeldModifiers.From(keyboard.State));
        Assert.Equal(Modifiers.None, HeldModifiers.From(null));
    }
}
