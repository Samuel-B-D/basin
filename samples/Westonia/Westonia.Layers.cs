using Basin;
using Basin.Desktop;
using Basin.Diagnostics;
using Basin.Scene;
using Basin.Shell.Xdg;
using Basin.Shell.Xdg.Protocol;

namespace Westonia;

internal sealed partial class Westonia
{
    private readonly Dictionary<IOutput, Box> _usable = [];
    private LayerShellSceneDriver _layerDriver = null!;
    private SessionLockSceneDriver _lockDriver = null!;
    private LayerSurface? _layerKeyboard;

    private void CreateLayerDrivers()
    {
        _layerDriver = new LayerShellSceneDriver(_services, _layout, layer => LayerTree(layer.Layer));
        _lockDriver = new SessionLockSceneDriver(_services, _layers.Lock, _layout, _layers.SetLocked);
    }

    private void WireLayerShell()
    {
        _layerDriver.Accept = _ => _outputs.Views.Count > 0;
        _layerDriver.DefaultOutput = _ => _outputs.Views.Count > 0 ? _outputs.Views[0].Global : null;
        _layerDriver.TrackPopups(Shell);
        _layerDriver.SceneCreated += OnLayerMapped;
        _layerDriver.PopupSceneCreated += (_, _, _) => RefreshSurfaceLuts();
        _layerDriver.Removed += OnLayerRemoved;
        _layerDriver.UsableAreaChanged += (output, usable) =>
        {
            var box = _layout.BoxOf(output);
            _usable[output] = usable.Translated(box.X, box.Y);
        };
        _layerDriver.Arranged += _outputs.ScheduleAll;
    }

    private SceneTree LayerTree(LayerKind kind) => kind switch
    {
        LayerKind.Background => _layers.LayerBackground,
        LayerKind.Bottom => _layers.LayerBottom,
        LayerKind.Top => _layers.LayerTop,
        _ => _layers.LayerOverlay,
    };

    private Box WorkAreaOf(IOutput output, Box box)
    {
        var work = _avalonia.WorkArea(box.X, box.Y, box.Width, box.Height);
        return _usable.TryGetValue(output, out var usable) && !usable.IsEmpty ? work.Intersect(usable) : work;
    }

    private void OnLayerMapped(LayerSurface layer, SceneSurface scene)
    {
        RefreshSurfaceLuts();
        BasinReport.Line($"LAYER + {layer.Namespace} {layer.Layer}");
        if (layer.KeyboardInteractivity == ZwlrLayerSurfaceV1.KeyboardInteractivity.None || _layers.IsLocked)
        {
            return;
        }

        _layerKeyboard = layer;
        _shell.KeyboardTarget = null;
        Seat.Keyboard.NotifyEnter(layer.Surface);
    }

    private void OnLayerRemoved(LayerSurface layer)
    {
        BasinReport.Line($"LAYER - {layer.Namespace}");
        if (!ReferenceEquals(_layerKeyboard, layer))
        {
            return;
        }

        _layerKeyboard = null;
        if (_layers.IsLocked || _shell.KeyboardTarget is not null)
        {
            return;
        }

        Seat.Keyboard.NotifyEnter(_shell.Focused is { } focused && focused.Window.IsMapped ? focused.Surface : null);
    }
}
