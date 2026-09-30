using Basin.Cli;

namespace EightWm;

internal sealed partial class Shell
{
    private void AttachColor()
    {
        if (!_colorPack.Attach(_services, _scene))
        {
            return;
        }

        _colorPack.Surfaces.CountChanged += attached => _report.Line(CompositorLines.Luts(attached));
        _colorPack.Manager.OutputDescriptionChanged += (global, description) =>
            _seat.DescribeCursor(global.Output, description);

        _outputs.Added += driver => DescribeOutput(ViewOf(driver));
        _outputs.Removed += view => _colorPack.RemoveOutput(view.Global);
        _outputs.LayoutChanged += RefreshLuts;
        foreach (var view in Views)
        {
            DescribeOutput(view);
        }
    }

    private void DescribeOutput(ShellView view) => _colorPack.AddOutput(view.Global, view.Output, view.SceneOutput);

    private void RefreshLuts() => _colorPack.RefreshSurfaces();
}
