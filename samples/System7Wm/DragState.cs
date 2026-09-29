using Basin;
using Basin.Host;

namespace System7Wm;

internal sealed class DragState
{
    public required ShellWindow Window { get; init; }

    public required OutputView View { get; init; }

    public bool Grow { get; init; }

    public double StartX { get; init; }

    public double StartY { get; init; }

    public Box Origin { get; init; }

    public Box Proposed { get; set; }

    public bool Valid { get; set; } = true;

    public OutputView? Under { get; set; }

    public bool Moved { get; set; }
}
