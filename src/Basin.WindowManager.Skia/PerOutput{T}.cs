using Wayland;

namespace Basin.WindowManager.Skia;

public sealed class PerOutput<T>
    where T : class, IDisposable
{
    private readonly ManagerSurfaceHost? _host;
    private readonly Func<WmOutput, WlOutput, T> _create;
    private readonly Dictionary<WmOutput, T> _items = [];
    private readonly List<WmOutput> _stale = [];

    public PerOutput(ManagerSurfaceHost? host, Func<WmOutput, WlOutput, T> create)
    {
        ArgumentNullException.ThrowIfNull(create);
        _host = host;
        _create = create;
    }

    public int Count => _items.Count;

    public Dictionary<WmOutput, T>.ValueCollection Values => _items.Values;

    public event Action<WmOutput, T>? Removing;

    public bool TryGetValue(WmOutput output, out T item) => _items.TryGetValue(output, out item!);

    public bool ContainsKey(WmOutput output) => _items.ContainsKey(output);

    public T? GetValueOrDefault(WmOutput output) => _items.GetValueOrDefault(output);

    public Dictionary<WmOutput, T>.Enumerator GetEnumerator() => _items.GetEnumerator();

    public void Reconcile(IReadOnlyList<WmOutput> outputs, bool wanted = true)
    {
        ArgumentNullException.ThrowIfNull(outputs);
        if (wanted && _host is { LayerShell: not null } host)
        {
            foreach (var output in outputs)
            {
                if (!output.IsRemoved && !_items.ContainsKey(output) && host.ProxyFor(output) is { } proxy)
                {
                    _items[output] = _create(output, proxy);
                }
            }
        }

        foreach (var (output, item) in _items)
        {
            if (output.IsRemoved || !wanted)
            {
                _stale.Add(output);
            }
        }

        foreach (var output in _stale)
        {
            Remove(output);
        }

        _stale.Clear();
    }

    public bool Remove(WmOutput output)
    {
        if (!_items.Remove(output, out var item))
        {
            return false;
        }

        Removing?.Invoke(output, item);
        item.Dispose();
        return true;
    }

    public void Clear()
    {
        foreach (var output in _items.Keys)
        {
            _stale.Add(output);
        }

        foreach (var output in _stale)
        {
            Remove(output);
        }

        _stale.Clear();
    }
}
