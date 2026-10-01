namespace Basin.Themes.MsStyles;

public readonly record struct MsStylesImage(int Id, string Name, ReadOnlyMemory<byte> Bytes, MsStylesImageFormat Format, bool Premultiplied = false);
