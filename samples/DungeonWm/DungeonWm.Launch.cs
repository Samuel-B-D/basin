using Basin.Desktop;
using Basin.Diagnostics;
using Basin.Ipc;
using Basin.Shell.Xdg;
using Basin.Shell.Xdg.Protocol;

namespace DungeonWm;

internal sealed partial class DungeonWm
{
    private LayerSurface? _layerKeyboard;
    private string? _layerOpener;
    private string? _lastSpawn;

    private void Spawn(string action, string command)
    {
        if (_ipc is not { } ipc)
        {
            _log.Warn($"cannot run '{command}': no process tracker");
            return;
        }

        if (ipc.Processes.Spawn(new IpcLaunch(["/bin/sh", "-c", command]), out var error) is not { } process)
        {
            _log.Warn($"cannot run '{command}': {error}");
            return;
        }

        _lastSpawn = action;
        BasinReport.Line($"SPAWN {process.Pid} {command}");
    }

    private void TrackLayerKeyboard(LayerShellSceneDriver driver)
    {
        driver.SceneCreated += (layer, _) =>
        {
            if (layer.KeyboardInteractivity == ZwlrLayerSurfaceV1.KeyboardInteractivity.None)
            {
                return;
            }

            _layerKeyboard = layer;
            _layerOpener = _lastSpawn;
            _seat.Keyboard.NotifyEnter(layer.Surface);
            BasinReport.Line($"KEYBOARD layer {layer.Namespace}");
            SyncMode();
        };
        driver.Removed += layer =>
        {
            if (!ReferenceEquals(_layerKeyboard, layer))
            {
                return;
            }

            _layerKeyboard = null;
            _layerOpener = null;
            if (_focused is { } focused)
            {
                _seat.Keyboard.NotifyEnter(focused.Surface);
                BasinReport.Line($"KEYBOARD window {focused.AppId}");
            }
            else
            {
                _seat.Keyboard.NotifyClearFocus();
                BasinReport.Line($"KEYBOARD none");
            }

            SyncMode();
        };
    }

    private bool LayerHoldsKeyboard => _layerKeyboard is not null;

    private bool LayerKeepsKeyboard =>
        _layerKeyboard is { KeyboardInteractivity: ZwlrLayerSurfaceV1.KeyboardInteractivity.Exclusive };
}
