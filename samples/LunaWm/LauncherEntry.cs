namespace LunaWm;

internal sealed record LauncherEntry(string Id, string Name, string? Icon, IReadOnlyList<string> Categories, string? GenericName, IReadOnlyList<string> Keywords);
