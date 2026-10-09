using Basin.Capabilities;

namespace Basin.SoftKeyboard;

public sealed class SoftKeyboardTextInput : ITextInputMethod
{
    private uint _pendingHint;
    private uint _pendingPurpose;
    private string? _pendingText;
    private int _pendingCursor;

    public Surface? ActiveSurface { get; private set; }

    public FieldState Field { get; private set; }

    public string? Surrounding { get; private set; }

    public int Cursor { get; private set; }

    public Box? CaretRectangle { get; private set; }

    public bool ExternalInputMethod { get; set; }

    public bool IsActive => ActiveSurface is not null && !ExternalInputMethod;

    public bool IsAvailable => true;

    public bool HasKeyboardGrab => false;

    public event Action<PreeditString>? Preedit;

    public event Action<string>? CommitString;

    public event Action<uint, uint>? DeleteSurroundingText;

    public event Action? Done;

    public event Action? AvailabilityChanged
    {
        add { }
        remove { }
    }

    public event Action<Surface>? Activated;

    public event Action? Deactivated;

    public event Action<FieldState>? FieldChanged;

    public void Activate(Surface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (ReferenceEquals(ActiveSurface, surface))
        {
            return;
        }

        ActiveSurface = surface;
        _pendingHint = 0;
        _pendingPurpose = 0;
        _pendingText = null;
        _pendingCursor = 0;
        Activated?.Invoke(surface);
    }

    public void Deactivate(Surface surface)
    {
        if (!ReferenceEquals(ActiveSurface, surface))
        {
            return;
        }

        ActiveSurface = null;
        Surrounding = null;
        Cursor = 0;
        Field = FieldState.Inactive;
        Deactivated?.Invoke();
        FieldChanged?.Invoke(Field);
    }

    public void SurroundingText(string text, uint cursor, uint anchor)
    {
        _pendingText = text;
        _pendingCursor = (int)cursor;
    }

    public void ContentType(uint hint, uint purpose)
    {
        _pendingHint = hint;
        _pendingPurpose = purpose;
    }

    public void CursorRectangle(in Box rect) => CaretRectangle = rect;

    public void Commit(uint serial)
    {
        if (ActiveSurface is null)
        {
            return;
        }

        Surrounding = _pendingText;
        Cursor = _pendingCursor;
        var field = new FieldState(
            true,
            FieldState.KindOf(_pendingPurpose),
            FieldState.AutoCapFor(_pendingHint, _pendingPurpose, Surrounding, Cursor),
            _pendingHint,
            _pendingPurpose);
        Field = field;
        FieldChanged?.Invoke(field);
    }

    public void ForwardKey(uint timeMs, uint keycode, bool pressed)
    {
    }

    public void ForwardModifiers(uint depressed, uint latched, uint locked, uint group)
    {
    }

    public void Send(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        CommitString?.Invoke(text);
        Done?.Invoke();
    }

    public void SendPreedit(string? text, int cursor)
    {
        Preedit?.Invoke(new PreeditString(text ?? string.Empty, cursor, cursor));
        Done?.Invoke();
    }

    public void Delete(uint beforeBytes, uint afterBytes)
    {
        DeleteSurroundingText?.Invoke(beforeBytes, afterBytes);
        Done?.Invoke();
    }
}
