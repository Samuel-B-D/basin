using Avalonia.Data.Converters;

namespace LunaWm;

public static class LunaConverters
{
    public static readonly IValueConverter CheckOpacity =
        new FuncValueConverter<bool, double>(on => on ? 1.0 : 0.0);
}
