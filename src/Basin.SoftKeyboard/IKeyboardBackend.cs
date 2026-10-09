namespace Basin.SoftKeyboard;

public interface IKeyboardBackend
{
    bool IsActive { get; }

    void ProcessKey(uint keysym, uint keycode, uint state, bool release, uint timeMs);
}
