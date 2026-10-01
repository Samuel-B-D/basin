namespace Basin.Themes.MsStyles;

public sealed class MsStylesDiagnostics
{
    private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> Counts => _counts;

    public int Total { get; private set; }

    public int CountOf(string kind)
    {
        var prefix = kind + ":";
        var total = 0;
        foreach (var (key, count) in _counts)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
            {
                total += count;
            }
        }

        return total;
    }

    public void AddOnce(string kind, string what)
    {
        if (!_counts.ContainsKey(kind + ":" + what))
        {
            Add(kind, what);
        }
    }

    public void Add(string kind, string what)
    {
        var key = kind + ":" + what;
        _counts[key] = _counts.TryGetValue(key, out var count) ? count + 1 : 1;
        Total++;
    }

    public override string ToString() =>
        string.Join(Environment.NewLine, _counts.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key} x{p.Value}"));
}
