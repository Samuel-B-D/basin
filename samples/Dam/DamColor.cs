using Basin.Cli;
using Basin.Diagnostics;
using Basin.Host;

namespace Dam;

internal sealed partial class Dam
{
    private void WireColor()
    {
        if (!_colorPack.Attach(_services, _scene))
        {
            return;
        }

        _colorPack.Surfaces.CountChanged += attached => BasinReport.Line(CompositorLines.Luts(attached));
        _outputs.Added += DescribeOutput;
        _outputs.Removed += view => _colorPack.RemoveOutput(view.Global);
        foreach (var view in _outputs.Views)
        {
            DescribeOutput(view);
        }
    }

    private void DescribeOutput(OutputView view) => _colorPack.AddOutput(view.Global, view.Output, view.Scene);
}
