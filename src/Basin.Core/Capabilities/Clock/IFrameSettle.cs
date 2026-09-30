namespace Basin.Capabilities;

public interface IFrameSettle
{
    bool IsSettled { get; }

    long Presented { get; }

    event Action? Settled;
}
