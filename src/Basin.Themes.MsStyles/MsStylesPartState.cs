namespace Basin.Themes.MsStyles;

internal sealed class MsStylesPartState
{
    public MsStylesPartState(int part, int state)
    {
        Part = part;
        State = state;
    }

    public int Part { get; }

    public int State { get; }

    public Dictionary<int, MsStylesProperty> Properties { get; } = [];

    public List<MsStylesProperty> Order { get; } = [];

    public bool TryAdd(MsStylesProperty property)
    {
        if (!Properties.TryAdd(property.Id, property))
        {
            return false;
        }

        Order.Add(property);
        return true;
    }
}
