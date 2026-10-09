namespace Basin.SoftKeyboard;

public sealed record KeyTiming
{
    public static readonly KeyTiming Default = new();

    public int ShiftDoubleTapMs { get; init; } = 500;

    public int LongPressMs { get; init; } = 350;

    public int SlowLongPressMs { get; init; } = 750;

    public int RepeatDelayMs { get; init; } = 350;

    public int RepeatIntervalMs { get; init; } = 120;

    public int WordDeleteIntervalMs { get; init; } = 275;

    public int WordDeleteAfterMs { get; init; } = 1850;

    public int DoubleSpaceMs { get; init; } = 1000;

    public float GraceBand { get; init; } = 40;

    public int GraceBandMs { get; init; } = 500;

    public float TraceThreshold { get; init; } = 35;
}
