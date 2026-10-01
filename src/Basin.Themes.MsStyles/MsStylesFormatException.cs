namespace Basin.Themes.MsStyles;

public sealed class MsStylesFormatException : Exception
{
    public MsStylesFormatException(string message, long offset = -1)
        : base(offset >= 0 ? $"{message} (at offset 0x{offset:x})" : message)
    {
        Offset = offset;
    }

    public MsStylesFormatException(string message, Exception inner)
        : base(message, inner)
    {
        Offset = -1;
    }

    public long Offset { get; }
}
