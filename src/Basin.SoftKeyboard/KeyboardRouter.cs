using System.Text;
using Xkb;

namespace Basin.SoftKeyboard;

public sealed class KeyboardRouter
{
    public const uint BackspaceCode = 14;
    public const uint TabCode = 15;
    public const uint ReturnCode = 28;
    public const uint LeftCode = 105;
    public const uint RightCode = 106;

    private const uint XkbOffset = 8;
    private const uint ShiftMask = 1;
    private const uint ControlMask = 4;
    private const string NoPeriodAfter = ".,;:!?";

    private readonly SoftKeyboardTextInput _textInput;
    private readonly ISoftKeyOutput _output;
    private readonly KeyTiming _timing;
    private bool _spaceArmed;
    private long _lastSpace;
    private char _lastKey = 'a';

    public KeyboardRouter(SoftKeyboardTextInput textInput, ISoftKeyOutput output, KeyTiming? timing = null)
    {
        ArgumentNullException.ThrowIfNull(textInput);
        ArgumentNullException.ThrowIfNull(output);
        _textInput = textInput;
        _output = output;
        _timing = timing ?? KeyTiming.Default;
        textInput.FieldChanged += _ => ResetShortcuts();
    }

    public IKeyboardBackend? Backend { get; set; }

    public Basin.Capabilities.ITextInputCommit? Commit { get; set; }

    public bool SpacesToPeriod { get; set; } = true;

    public KeyRoute Route => Backend is { IsActive: true }
        ? KeyRoute.Backend
        : _textInput.IsActive ? KeyRoute.TextInput : KeyRoute.Keys;

    public event Action<TypedKey, KeyRoute>? Sent;

    public void Attach(SoftKeyboardEngine engine, Func<long>? clock = null)
    {
        ArgumentNullException.ThrowIfNull(engine);
        var now = clock ?? (static () => Environment.TickCount64);
        engine.Typed += key => Send(key, now());
        _textInput.FieldChanged += field =>
        {
            engine.State.SetAutoCap(field.Active && field.AutoCap);
            engine.State.NumLockField = field.NumLock;
            engine.State.ClearSymbolLock();
        };
    }

    public void Send(in TypedKey key, long now)
    {
        var route = Route;
        if (key.Cap.IsText && !key.Cap.IsShortcut && DoubleSpace(key, now))
        {
            Emit(route, new TypedKey(KeyCap.Backspace, false, false, false, false), (uint)now);
            Text(route, ". ", (uint)now);
            _lastKey = '\0';
            _spaceArmed = false;
            Sent?.Invoke(key, route);
            return;
        }

        Emit(route, key, (uint)now);
        Sent?.Invoke(key, route);
    }

    public KeyRoute CommitWord(string word, long now)
    {
        ArgumentNullException.ThrowIfNull(word);
        ResetShortcuts();
        if (Commit?.TryCommitString(word) == true)
        {
            return KeyRoute.TextInput;
        }

        var route = _textInput.IsActive ? KeyRoute.TextInput : KeyRoute.Keys;
        Text(route, word, (uint)now);
        return route;
    }

    public void DeleteCommitted(string committed, long now)
    {
        ArgumentNullException.ThrowIfNull(committed);
        if (committed.Length == 0)
        {
            return;
        }

        if (_textInput.IsActive)
        {
            _textInput.Delete((uint)Encoding.UTF8.GetByteCount(committed), 0);
            return;
        }

        var backspace = new TypedKey(KeyCap.Backspace, false, false, false, false);
        var route = Route;
        for (var i = 0; i < committed.EnumerateRunes().Count(); i++)
        {
            Emit(route, backspace, (uint)now);
        }
    }

    private void Emit(KeyRoute route, in TypedKey key, uint time)
    {
        var cap = key.Cap;
        if (cap.IsText)
        {
            Text(route, key.Text, time);
            return;
        }

        var (code, keysym) = cap.Function switch
        {
            KeyFunction.Backspace => (BackspaceCode, 0xff08u),
            KeyFunction.Return => (ReturnCode, 0xff0du),
            KeyFunction.Tab => (TabCode, 0xff09u),
            KeyFunction.Left => (LeftCode, 0xff51u),
            KeyFunction.Right => (RightCode, 0xff53u),
            _ => (0u, 0u),
        };
        if (code == 0)
        {
            return;
        }

        if (cap.Function == KeyFunction.Backspace)
        {
            _lastKey = 'a';
            _spaceArmed = false;
        }
        else
        {
            _lastKey = '\0';
            _spaceArmed = false;
        }

        switch (route)
        {
            case KeyRoute.Backend:
                var state = key.WordDelete ? ControlMask : key.ShiftHeld ? ShiftMask : 0;
                Backend!.ProcessKey(keysym, code + XkbOffset, state, release: false, time);
                Backend.ProcessKey(keysym, code + XkbOffset, state, release: true, time);
                break;
            case KeyRoute.TextInput when key.WordDelete && _textInput.Surrounding is not null:
                DeleteWord();
                break;
            default:
                if (key.WordDelete)
                {
                    _output.ControlKey(code, time);
                }
                else
                {
                    _output.Key(code, time);
                }

                break;
        }
    }

    private void Text(KeyRoute route, string text, uint time)
    {
        switch (route)
        {
            case KeyRoute.Backend:
                foreach (var rune in text.EnumerateRunes())
                {
                    var keysym = XkbKeysym.FromUtf32((uint)rune.Value).Value;
                    var keycode = _output.TryResolve(keysym, out var evdev, out var modifiers) ? evdev + XkbOffset : 0;
                    Backend!.ProcessKey(keysym, keycode, modifiers, release: false, time);
                    Backend.ProcessKey(keysym, keycode, modifiers, release: true, time);
                }

                break;
            case KeyRoute.TextInput:
                _textInput.Send(text);
                break;
            default:
                _output.Type(text, time);
                break;
        }
    }

    private void DeleteWord()
    {
        var before = FieldState.BeforeCursor(_textInput.Surrounding!, _textInput.Cursor);
        var end = before.Length;
        while (end > 0 && char.IsWhiteSpace(before[end - 1]))
        {
            end--;
        }

        while (end > 0 && !char.IsWhiteSpace(before[end - 1]))
        {
            end--;
        }

        var bytes = Encoding.UTF8.GetByteCount(before[end..]);
        if (bytes > 0)
        {
            _textInput.Delete((uint)bytes, 0);
        }
    }

    private bool DoubleSpace(in TypedKey key, long now)
    {
        if (!SpacesToPeriod || (_textInput.Field.Active && _textInput.Field.Kind != FieldKind.Text))
        {
            return false;
        }

        var space = key.Cap.IsSpace;
        if (space && _spaceArmed)
        {
            var fire = _lastSpace + _timing.DoubleSpaceMs > now;
            _spaceArmed = false;
            _lastKey = '\0';
            return fire;
        }

        if (space && _lastKey != '\0' && NoPeriodAfter.IndexOf(_lastKey) < 0)
        {
            _spaceArmed = true;
            _lastSpace = now;
        }
        else
        {
            _spaceArmed = false;
            _lastKey = space || key.Text.Length != 1 ? '\0' : key.Text[0];
        }

        return false;
    }

    private void ResetShortcuts()
    {
        _spaceArmed = false;
        _lastKey = 'a';
    }
}
