namespace Basin.Notifications;

public sealed record NotificationImage(
    int Width,
    int Height,
    int RowStride,
    bool HasAlpha,
    int BitsPerSample,
    int Channels,
    ReadOnlyMemory<byte> Pixels);
