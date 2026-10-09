namespace Basin.SoftKeyboard;

public interface ISoftKeyOutput
{
    void Key(uint keycode, uint timeMs);

    void ControlKey(uint keycode, uint timeMs);

    int Type(string text, uint timeMs);

    bool TryResolve(uint keysym, out uint keycode, out uint modifiers);
}
