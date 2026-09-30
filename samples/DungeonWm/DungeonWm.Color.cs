using Basin.Cli;
using Basin.Diagnostics;
using Basin.Host;

namespace DungeonWm;

internal sealed partial class DungeonWm
{
    private void WireColor()
    {
        if (!_colorPack.Attach(_services, _scene))
        {
            return;
        }

        _colorPack.Surfaces.CountChanged += attached => BasinReport.Line(CompositorLines.Luts(attached));
        _colorPack.Manager.OutputDescriptionChanged +=
            (global, description) => _cursor.Describe(global.Output, description);
        _layerDriver.SceneCreated += (_, _) => RefreshSurfaceLuts();
        _layerDriver.PopupSceneCreated += (_, _, _) => RefreshSurfaceLuts();
        _xDriver.Adopted += (_, _, _) => RefreshSurfaceLuts();
        _outputs.Added += DescribeOutput;
        _outputs.Removed += view => _colorPack.RemoveOutput(view.Global);
        foreach (var view in _outputs.Views)
        {
            DescribeOutput(view);
        }
    }

    private void DescribeOutput(OutputView view) => _colorPack.AddOutput(view.Global, view.Output, view.Scene);

    private void RefreshSurfaceLuts() => _colorPack.RefreshSurfaces();
}
