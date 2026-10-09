namespace Basin.SoftKeyboard;

public sealed class KeyCell
{
    public static readonly KeyCell Empty = new(0, null, null, null, KeyClass.Function);

    public KeyCell(float weight, KeyCap? plain, KeyCap? alternate, IReadOnlyList<KeyCap>? longPress, KeyClass keyClass)
    {
        Weight = weight;
        Plain = plain;
        Alternate = alternate ?? plain;
        LongPress = longPress is { Count: > 0 } ? longPress : null;
        Class = keyClass;
    }

    public float Weight { get; }

    public KeyCap? Plain { get; }

    public KeyCap? Alternate { get; }

    public IReadOnlyList<KeyCap>? LongPress { get; }

    public KeyClass Class { get; }

    public bool IsKey => Weight != 0 && Plain is not null;

    public bool IsInvisible => Weight < 0;

    public bool IsVisible => Weight > 0 && Plain is not null;

    public KeyCell WithWeight(float weight) => new(weight, Plain, Alternate, LongPress, Class);
}
