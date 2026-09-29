namespace Basin.AppMenu;

public sealed class AppMenuAddresses
{
    private readonly Dictionary<Surface, AppMenuAddress> _addresses = [];

    public bool ShowsMenuBar { get; set; }

    public event Action<Surface, AppMenuAddress?>? Changed;

    public int Count => _addresses.Count;

    public AppMenuAddress? AddressOf(Surface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        return _addresses.GetValueOrDefault(surface);
    }

    public void Set(Surface surface, AppMenuAddress? address)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (address is null)
        {
            if (_addresses.Remove(surface))
            {
                Changed?.Invoke(surface, null);
            }

            return;
        }

        if (_addresses.TryGetValue(surface, out var previous) && previous == address)
        {
            return;
        }

        if (previous is null)
        {
            surface.Destroyed += () => Set(surface, null);
        }

        _addresses[surface] = address;
        Changed?.Invoke(surface, address);
    }
}
