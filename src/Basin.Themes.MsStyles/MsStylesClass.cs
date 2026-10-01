namespace Basin.Themes.MsStyles;

public sealed class MsStylesClass
{
    private readonly Dictionary<(int Part, int State), MsStylesPartState> _blocks = [];
    private readonly List<MsStylesPartState> _order = [];

    internal MsStylesClass(MsStylesTheme theme, string app, string name)
    {
        Theme = theme;
        App = app;
        Name = name;
    }

    public MsStylesTheme Theme { get; }

    public string App { get; }

    public string Name { get; }

    public string FullName => App.Length == 0 ? Name : App + "::" + Name;

    public MsStylesClass? Next { get; internal set; }

    public bool IsGlobals => App.Length == 0 && string.Equals(Name, "globals", StringComparison.OrdinalIgnoreCase);

    internal IReadOnlyList<MsStylesPartState> Blocks => _order;

    public IEnumerable<(int Part, int State)> PartStates => _order.Select(b => (b.Part, b.State));

    public IEnumerable<ThemeProperty> PropertiesOf(int part, int state) =>
        _blocks.TryGetValue((part, state), out var block) ? block.Order.Select(p => (ThemeProperty)p.Id) : [];

    public bool HasPart(int part)
    {
        foreach (var block in _order)
        {
            if (block.Part == part)
            {
                return true;
            }
        }

        return false;
    }

    internal MsStylesPartState Block(int part, int state)
    {
        if (!_blocks.TryGetValue((part, state), out var block))
        {
            block = new MsStylesPartState(part, state);
            _blocks[(part, state)] = block;
            _order.Add(block);
        }

        return block;
    }

    internal MsStylesProperty? Find(int part, int state, int id)
    {
        while (true)
        {
            for (var cls = this; cls is not null; cls = cls.Next)
            {
                if (cls._blocks.TryGetValue((part, state), out var block) && block.Properties.TryGetValue(id, out var property))
                {
                    return property;
                }
            }

            if (state != 0)
            {
                state = 0;
            }
            else if (part != 0)
            {
                part = 0;
            }
            else
            {
                return null;
            }
        }
    }

    public PropertyOrigin OriginOf(int part, int state, ThemeProperty id) =>
        Find(part, state, (int)id)?.Origin ?? PropertyOrigin.NotFound;

    public ThemePrimitive PrimitiveOf(int part, int state, ThemeProperty id) =>
        Find(part, state, (int)id)?.Primitive ?? ThemePrimitive.None;

    public bool TryGetInt(int part, int state, ThemeProperty id, out int value)
    {
        if (Find(part, state, (int)id) is { Values.Length: > 0 } property &&
            property.Primitive is ThemePrimitive.Int or ThemePrimitive.Size or ThemePrimitive.Enum or ThemePrimitive.Bool or ThemePrimitive.Color)
        {
            value = property.Values[0];
            return true;
        }

        value = 0;
        return false;
    }

    public bool TryGetSize(int part, int state, ThemeProperty id, out int value)
    {
        if (Find(part, state, (int)id) is { Values.Length: > 0 } property &&
            property.Primitive is ThemePrimitive.Size or ThemePrimitive.Int)
        {
            value = property.Values[0];
            return true;
        }

        value = 0;
        return false;
    }

    public bool TryGetBool(int part, int state, ThemeProperty id, out bool value)
    {
        if (Find(part, state, (int)id) is { Values.Length: > 0 } property &&
            property.Primitive is ThemePrimitive.Bool or ThemePrimitive.Int)
        {
            value = property.Values[0] != 0;
            return true;
        }

        value = false;
        return false;
    }

    public bool TryGetEnum<TEnum>(int part, int state, ThemeProperty id, out TEnum value)
        where TEnum : struct, Enum
    {
        if (System.Runtime.CompilerServices.Unsafe.SizeOf<TEnum>() == sizeof(int) &&
            Find(part, state, (int)id) is { Values.Length: > 0 } property &&
            property.Primitive is ThemePrimitive.Enum or ThemePrimitive.Int)
        {
            var raw = property.Values[0];
            value = System.Runtime.CompilerServices.Unsafe.As<int, TEnum>(ref raw);
            return true;
        }

        value = default;
        return false;
    }

    public bool TryGetColor(int part, int state, ThemeProperty id, out MsStylesColor value)
    {
        if (Find(part, state, (int)id) is { Primitive: ThemePrimitive.Color, Values.Length: >= 3 } property)
        {
            value = new MsStylesColor((byte)property.Values[0], (byte)property.Values[1], (byte)property.Values[2]);
            return true;
        }

        value = default;
        return false;
    }

    public bool TryGetMargins(int part, int state, ThemeProperty id, out MsStylesMargins value)
    {
        if (Find(part, state, (int)id) is { Primitive: ThemePrimitive.Margins, Values.Length: >= 4 } property)
        {
            value = new MsStylesMargins(property.Values[0], property.Values[1], property.Values[2], property.Values[3]);
            return true;
        }

        value = default;
        return false;
    }

    public bool TryGetRect(int part, int state, ThemeProperty id, out MsStylesRect value)
    {
        if (Find(part, state, (int)id) is { Primitive: ThemePrimitive.Rect, Values.Length: >= 4 } property)
        {
            value = new MsStylesRect(property.Values[0], property.Values[1], property.Values[2], property.Values[3]);
            return true;
        }

        value = default;
        return false;
    }

    public bool TryGetPosition(int part, int state, ThemeProperty id, out MsStylesPosition value)
    {
        if (Find(part, state, (int)id) is { Primitive: ThemePrimitive.Position, Values.Length: >= 2 } property)
        {
            value = new MsStylesPosition(property.Values[0], property.Values[1]);
            return true;
        }

        value = default;
        return false;
    }

    public bool TryGetIntList(int part, int state, ThemeProperty id, out ReadOnlySpan<int> value)
    {
        if (Find(part, state, (int)id) is { Primitive: ThemePrimitive.IntList } property)
        {
            value = property.Values;
            return true;
        }

        value = default;
        return false;
    }

    public bool TryGetString(int part, int state, ThemeProperty id, out string value)
    {
        if (Find(part, state, (int)id) is { Text: { } text } property &&
            property.Primitive is ThemePrimitive.String or ThemePrimitive.Filename)
        {
            value = text;
            return true;
        }

        value = string.Empty;
        return false;
    }

    public bool TryGetFont(int part, int state, ThemeProperty id, out MsStylesFont value)
    {
        if (Find(part, state, (int)id) is { HasFont: true } property)
        {
            value = property.Font;
            return true;
        }

        value = default;
        return false;
    }

    public bool TryGetImage(int part, int state, ThemeProperty id, out MsStylesImage value)
    {
        if (Find(part, state, (int)id) is { HasImage: true } property)
        {
            value = property.Image;
            return true;
        }

        if (id == ThemeProperty.AtlasImage && Find(part, state, (int)ThemeProperty.DiskStream) is { HasImage: true } stream)
        {
            value = stream.Image;
            return true;
        }

        value = default;
        return false;
    }

    public override string ToString() => FullName;
}
