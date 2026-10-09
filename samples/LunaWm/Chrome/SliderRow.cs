namespace LunaWm;

public sealed class SliderRow : MenuRow
{
    private double _value;
    private bool _syncing;

    public double Value
    {
        get => _value;
        set
        {
            if (Set(ref _value, value) && !_syncing)
            {
                Changed?.Invoke(value);
            }
        }
    }

    public Action<double>? Changed { get; init; }

    public void Sync(double value)
    {
        _syncing = true;
        try
        {
            Value = value;
        }
        finally
        {
            _syncing = false;
        }
    }
}
