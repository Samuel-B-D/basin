namespace LunaWm;

public sealed class ToggleRow : MenuRow
{
    private bool _on;

    public bool On
    {
        get => _on;
        set
        {
            if (Set(ref _on, value))
            {
                Raise(nameof(State));
            }
        }
    }

    public string State => _on ? "On" : "Off";

    public Action<bool>? Changed { get; init; }

    public void Toggle()
    {
        On = !On;
        Changed?.Invoke(On);
    }
}
