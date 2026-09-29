using Avalonia;
using Avalonia.Controls;
using System7.Avalonia.Rendering;

namespace System7Wm;

internal sealed class DesktopIcon
{
    public required string Key { get; init; }

    public required string Path { get; set; }

    public required string Label { get; set; }

    public required System7IconFamily Family { get; set; }

    public DesktopIconKind Kind { get; init; }

    public Point Position { get; set; }

    public bool Selected { get; set; }

    public Control? Visual { get; set; }

    public Image? Picture { get; set; }

    public TextBlock? Name { get; set; }

    public Border? NameBack { get; set; }

    public Rect Bounds => new(Position.X, Position.Y, 64, 48);

    public Rect PictureBounds => new(Position.X + 16, Position.Y, 32, 32);
}
