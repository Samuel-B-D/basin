using Basin.Shell.River.Protocol;

namespace Basin.Shell.River;

internal static class RiverModifiers
{
    public static RiverSeatV1.Modifiers From(Basin.Seat.SeatKeyboard keyboard)
    {
        var state = keyboard.State;
        if (state is null)
        {
            return RiverSeatV1.Modifiers.None;
        }

        var result = RiverSeatV1.Modifiers.None;
        if (state.IsModActive("Shift"))
        {
            result |= RiverSeatV1.Modifiers.Shift;
        }

        if (state.IsModActive("Control"))
        {
            result |= RiverSeatV1.Modifiers.Ctrl;
        }

        if (state.IsModActive("Mod1"))
        {
            result |= RiverSeatV1.Modifiers.Mod1;
        }

        if (state.IsModActive("Mod3"))
        {
            result |= RiverSeatV1.Modifiers.Mod3;
        }

        if (state.IsModActive("Mod4"))
        {
            result |= RiverSeatV1.Modifiers.Mod4;
        }

        if (state.IsModActive("Mod5"))
        {
            result |= RiverSeatV1.Modifiers.Mod5;
        }

        return result;
    }
}
