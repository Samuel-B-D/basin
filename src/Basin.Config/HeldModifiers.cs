using Xkb;

namespace Basin.Config;

public static class HeldModifiers
{
    public static Modifiers From(XkbState? state)
    {
        if (state is null)
        {
            return Modifiers.None;
        }

        var held = Modifiers.None;
        if (state.IsModActive("Shift"))
        {
            held |= Modifiers.Shift;
        }

        if (state.IsModActive("Control"))
        {
            held |= Modifiers.Ctrl;
        }

        if (state.IsModActive("Mod1"))
        {
            held |= Modifiers.Mod1;
        }

        if (state.IsModActive("Mod3"))
        {
            held |= Modifiers.Mod3;
        }

        if (state.IsModActive("Mod4"))
        {
            held |= Modifiers.Mod4;
        }

        if (state.IsModActive("Mod5"))
        {
            held |= Modifiers.Mod5;
        }

        return held;
    }
}
