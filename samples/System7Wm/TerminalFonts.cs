using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace System7Wm;

internal static class TerminalFonts
{
    public static readonly int[] Sizes = [9, 12];

    public static bool IsSize(int size) => size is 9 or 12;

    public static FontFamily Family(int size)
    {
        var key = size == 12 ? "System7Monaco12FontFamily" : "System7MonacoFontFamily";
        return Application.Current?.TryFindResource(key, out var value) == true && value is FontFamily family
            ? family
            : new FontFamily("DejaVu Sans Mono, monospace");
    }
}
