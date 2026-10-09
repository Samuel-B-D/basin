namespace LunaWm;

public sealed class LockScreenModel : ObservableModel
{
    private string _clock = "";
    private string _date = "";
    private bool _pin;
    private bool _padShown;
    private string _entry = "";
    private string _message = "";
    private bool _busy;
    private int _shakes;

    public string Clock
    {
        get => _clock;
        set => Set(ref _clock, value);
    }

    public string Date
    {
        get => _date;
        set => Set(ref _date, value);
    }

    public bool Pin
    {
        get => _pin;
        set
        {
            if (Set(ref _pin, value))
            {
                Raise(nameof(Password));
            }
        }
    }

    public bool Password => !_pin;

    public bool PadShown
    {
        get => _padShown;
        set
        {
            if (Set(ref _padShown, value))
            {
                Raise(nameof(RingShown));
            }
        }
    }

    public bool RingShown => !_padShown;

    public string Entry
    {
        get => _entry;
        set
        {
            if (Set(ref _entry, value))
            {
                Raise(nameof(Dots));
            }
        }
    }

    public string Dots => new('●', _entry.Length);

    public string Message
    {
        get => _message;
        set => Set(ref _message, value);
    }

    public bool Busy
    {
        get => _busy;
        set => Set(ref _busy, value);
    }

    public int Shakes
    {
        get => _shakes;
        set => Set(ref _shakes, value);
    }

    public event Action? Submitted;

    public event Action? PadRequested;

    public void Submit() => Submitted?.Invoke();

    public void RequestPad() => PadRequested?.Invoke();

    public void Digit(string digit)
    {
        if (!_busy && _entry.Length < 32)
        {
            Entry += digit;
        }
    }

    public void Backspace()
    {
        if (!_busy && _entry.Length > 0)
        {
            Entry = _entry[..^1];
        }
    }

    public void Clear()
    {
        Entry = "";
    }
}
