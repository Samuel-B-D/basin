using System.Diagnostics;

namespace Basin.Avalonia;

// Windows reports AltGr as a synthesized Left Ctrl press followed at once by
// Right Alt, and repeats both while AltGr is held. Forwarded as is, a client
// sees Ctrl held under every AltGr keystroke, so it reads Ctrl+Alt+key rather
// than the key's third level. This picks the phantom Ctrl out by its timing,
// as HostModifierState does for the headless backend.
//
// Windows also treats Ctrl+Alt as AltGr, and software keyboards (Input Leap,
// Synergy) type AltGr characters by injecting Left Alt and Left Ctrl back to
// back around the key. Two such presses closer together than a person makes
// them are forwarded as Right Alt instead, which the client reads as AltGr.
internal sealed class WindowsAltGr
{
    private const uint KeyLeftCtrl = InputCodes.KeyLeftCtrl;
    private const uint KeyLeftAlt = InputCodes.KeyLeftAlt;
    private const uint KeyRightAlt = InputCodes.KeyRightAlt;
    private const long PhantomWindowMillis = 50;
    private const double ChordWindowMillis = 8;

    private readonly bool _enabled = OperatingSystem.IsWindows();
    private long _controlDownAt = long.MinValue;
    private bool _phantom;

    private long _chordControlDownAt;
    private long _chordAltDownAt;
    private bool _chordControl;
    private bool _chordAlt;
    private bool _chordRightAlt;

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

    // Focus left the window, so the releases will not arrive here.
    public void Reset()
    {
        _phantom = false;
        _chordControl = false;
        _chordAlt = false;
        _chordRightAlt = false;
    }

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

    // For a Left Ctrl or Left Alt press, given the keys the client holds.
    // Start: it completes an injected Ctrl+Alt chord; the caller does not
    // forward it, releases whichever of the two the client holds, and holds
    // Right Alt in their place. Swallow: a repeat of a chord key.
    public AltGrChord Chord(uint code, IReadOnlySet<uint> heldKeys)
    {
        if (!_enabled || (code != KeyLeftCtrl && code != KeyLeftAlt))
        {
            return AltGrChord.None;
        }

        if (code == KeyLeftCtrl ? _chordControl : _chordAlt)
        {
            return AltGrChord.Swallow;
        }

        var now = Stopwatch.GetTimestamp();
        if (!heldKeys.Contains(code))
        {
            if (code == KeyLeftCtrl)
            {
                _chordControlDownAt = now;
            }
            else
            {
                _chordAltDownAt = now;
            }
        }

        var other = code == KeyLeftCtrl ? KeyLeftAlt : KeyLeftCtrl;
        if (!heldKeys.Contains(other) || _chordControl || _chordAlt)
        {
            return AltGrChord.None;
        }

        var otherDownAt = code == KeyLeftCtrl ? _chordAltDownAt : _chordControlDownAt;
        if (Stopwatch.GetElapsedTime(otherDownAt, now).TotalMilliseconds > ChordWindowMillis)
        {
            return AltGrChord.None;
        }

        _chordControl = true;
        _chordAlt = true;
        _chordRightAlt = true;
        return AltGrChord.Start;
    }

    // For a key release: true when the key was part of a chord and must not
    // reach the client. releaseRightAlt is set for the first of the two
    // releases, when the caller releases the Right Alt standing in for them.
    public bool EndsChord(uint code, out bool releaseRightAlt)
    {
        releaseRightAlt = false;
        if (code == KeyLeftCtrl && _chordControl)
        {
            _chordControl = false;
        }
        else if (code == KeyLeftAlt && _chordAlt)
        {
            _chordAlt = false;
        }
        else
        {
            return false;
        }

        releaseRightAlt = _chordRightAlt;
        _chordRightAlt = false;
        return true;
    }
}

internal enum AltGrChord
{
    None,
    Start,
    Swallow,
}
