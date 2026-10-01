namespace Basin.Capabilities;

public readonly record struct FrameInteraction(FramePart Hot, FramePart Pressed)
{
    public FramePart PreviousHot { get; init; }

    public FramePart PreviousPressed { get; init; }

    public long HoverChangedAt { get; init; }

    public long PressChangedAt { get; init; }

    public long ActiveChangedAt { get; init; }

    public long Now { get; init; }
}
