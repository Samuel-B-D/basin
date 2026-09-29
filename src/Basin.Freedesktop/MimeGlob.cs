namespace Basin.Freedesktop;

internal readonly record struct MimeGlob(int Weight, string MimeType, string Pattern, bool CaseSensitive);
