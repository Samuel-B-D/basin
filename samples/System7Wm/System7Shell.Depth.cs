using Basin.Diagnostics;
using Basin.Host;
using System7.Avalonia.Rendering;

namespace System7Wm;

internal sealed partial class System7Shell
{
    private readonly HashSet<OutputView> _depthViews = [];
    private DepthStage? _depthStage;
    private bool _depthUnavailableReported;

    internal bool ApplicationDepthAvailable => (_depthStage ??= new DepthStage(_renderer)).IsSupported;

    private void ApplyApplicationDepth()
    {
        var wanted = _config.DepthApplications && _config.Depth != System7ColorDepth.Rgb888;
        if (wanted && !ApplicationDepthAvailable)
        {
            if (!_depthUnavailableReported)
            {
                _depthUnavailableReported = true;
                BasinReport.Line($"APPDEPTH unavailable on {_options.Renderer}");
            }

            wanted = false;
        }

        if (wanted)
        {
            _depthStage!.Set(_config.Depth, _config.DepthDither);
        }

        foreach (var view in _outputs.Views)
        {
            if (view.Scene is not { } scene)
            {
                continue;
            }

            if (wanted && _depthViews.Add(view))
            {
                scene.AddPostStage(_depthStage!);
            }
            else if (!wanted && _depthViews.Remove(view))
            {
                scene.RemovePostStage(_depthStage!);
            }
        }

        _depthViews.RemoveWhere(view => !_outputs.Views.Contains(view));
        _outputs.ScheduleAll();
    }

    private void DisposeApplicationDepth()
    {
        foreach (var view in _depthViews)
        {
            view.Scene?.RemovePostStage(_depthStage!);
        }

        _depthViews.Clear();
        _depthStage?.Dispose();
        _depthStage = null;
    }
}
