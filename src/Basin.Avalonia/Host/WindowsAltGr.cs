namespace Basin.Avalonia;

// Windows reports AltGr as a synthesized Left Ctrl press followed at once by
// Right Alt, and repeats both while AltGr is held. Forwarded as is, a client
// sees Ctrl held under every AltGr keystroke, so it reads Ctrl+Alt+key rather
// than the key's third level. This picks the phantom Ctrl out by its timing,
// as HostModifierState does for the headless backend.
internal sealed class WindowsAltGr
{
    private const uint KeyLeftCtrl = InputCodes.KeyLeftCtrl;
    private const uint KeyRightAlt = InputCodes.KeyRightAlt;
    private const long PhantomWindowMillis = 50;

    private readonly bool _enabled = OperatingSystem.IsWindows();
    private long _controlDownAt = long.MinValue;
    private bool _phantom;

    // True when this event is the phantom Ctrl, or a repeat of it, and must
    // not reach the client.
    public bool Swallows(uint code, bool pressed)
    {
        if (!_enabled || code != KeyLeftCtrl)
        {
            return false;
        }

        if (!pressed)
        {
            var phantom = _phantom;
            _phantom = false;
            return phantom;
        }

        if (_phantom)
        {
            return true;
        }

        _controlDownAt = Environment.TickCount64;
        return false;
    }

    // Focus left the window, so the phantom's release will not arrive here.
    public void Reset() => _phantom = false;

    // True when this Right Alt press shows the Left Ctrl just before it was
    // the phantom, which the caller then releases on the client.
    public bool UnmasksControl(uint code, bool pressed, bool controlHeld)
    {
        if (!_enabled || code != KeyRightAlt || !pressed || !controlHeld || _phantom)
        {
            return false;
        }

        if (Environment.TickCount64 - _controlDownAt > PhantomWindowMillis)
        {
            return false;
        }

        _phantom = true;
        return true;
    }
}
