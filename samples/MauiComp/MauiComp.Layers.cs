using Basin;
using Basin.Desktop;
using Basin.Diagnostics;
using Basin.Scene;
using Basin.Shell.Xdg;
using Basin.Shell.Xdg.Protocol;

namespace MauiComp;

internal sealed partial class MauiComp
{
    private readonly Dictionary<IOutput, Box> _usable = [];
    private LayerShellSceneDriver _layerDriver = null!;
    private SessionLockSceneDriver _lockDriver = null!;
    private LayerSurface? _layerKeyboard;
    private bool _locked;

    private void CreateLayerDrivers()
    {
        _layerDriver = new LayerShellSceneDriver(_services, _layout, layer => LayerTree(layer.Layer));
        _lockDriver = new SessionLockSceneDriver(_services, _layers.Lock, _layout, SetLocked);
    }

    private void WireLayers()
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

        _lockDriver.Locked += () => BasinReport.Line($"LOCKED");
        _lockDriver.Unlocked += () =>
        {
            RestoreKeyboard();
            BasinReport.Line($"UNLOCKED");
        };
        _lockDriver.Abandoned += () => BasinReport.Line($"LOCK ABANDONED (staying blanked)");
    }

    private SceneTree LayerTree(LayerKind kind) => kind switch
    {
        LayerKind.Background => _layers.LayerBackground,
        LayerKind.Bottom => _layers.LayerBottom,
        LayerKind.Top => _layers.LayerTop,
        _ => _layers.LayerOverlay,
    };

    private void RelayoutLayers()
    {
        _layerDriver.Rearrange();
        _lockDriver.Reconfigure();
    }

    private Box WorkAreaOf(IOutput output, Box box)
    {
        var work = ShellOutputs.WorkArea(box);
        return _usable.TryGetValue(output, out var usable) && !usable.IsEmpty ? work.Intersect(usable) : work;
    }

    private void OnLayerMapped(LayerSurface layer, SceneSurface scene)
    {
        RefreshSurfaceLuts();
        BasinReport.Line($"LAYER + {layer.Namespace} {layer.Layer}");
        if (layer.KeyboardInteractivity == ZwlrLayerSurfaceV1.KeyboardInteractivity.None || _locked)
        {
            return;
        }

        _layerKeyboard = layer;
        _seat?.Router.SetKeyboardFocus(null);
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
        if (!_locked)
        {
            RestoreKeyboard();
        }
    }

    private void SetLocked(bool locked)
    {
        _locked = locked;
        _layers.SetLocked(locked);
        if (locked)
        {
            ResetCursorMode();
            _switcher?.Cancel();
            _startMenu?.Close();
            _runDialog?.Close();
            _seat?.Router.SetKeyboardFocus(null);
            _seat?.Router.PointerLeave();
        }

        _outputs.ScheduleAll();
    }

    private void RestoreKeyboard()
    {
        if (_layerKeyboard is { IsMapped: true } layer)
        {
            Seat.Keyboard.NotifyEnter(layer.Surface);
            return;
        }

        Seat.Keyboard.NotifyEnter(_focused is { } focused && focused.Window.IsMapped ? focused.Window.Surface : null);
    }
}
