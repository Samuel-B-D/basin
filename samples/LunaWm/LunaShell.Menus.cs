using System.Globalization;
using Basin;
using Basin.AppMenu;
using Basin.Session;
using Basin.SystemStatus;

namespace LunaWm;

internal sealed partial class LunaShell
{
    internal const double MenuWidth = 300;
    internal const double AppMenuWidth = 320;

    private string? _armed;

    private void StatusBarPressed(LunaView view, string what)
    {
        _report.Line($"BARPRESS {what}");
        if (_locked)
        {
            return;
        }

        switch (what)
        {
            case "title":
                if (!view.InCardView && view.Deck.ActiveCard is not null)
                {
                    ToggleMenu(view, "app");
                }

                break;
            case "notifications":
                ShowDashboard(view, !DashboardOpen(view));
                break;
            case "pull":
                ShowDashboard(view, true);
                break;
            default:
                ToggleMenu(view, what);
                break;
        }
    }

    private void SystemMenuCommand(LunaView view, string which, bool open)
    {
        if (open)
        {
            OpenMenu(view, which);
        }
        else
        {
            CloseMenu(view);
        }
    }

    private void ToggleMenu(LunaView view, string kind)
    {
        if (view.Menu is { Open: true } menu && menu.Kind == kind)
        {
            CloseMenu(view);
            return;
        }

        OpenMenu(view, kind);
    }

    internal bool MenuOpen(LunaView view) => view.Menu is { Open: true };

    private void OpenMenu(LunaView view, string kind)
    {
        if (kind is not ("battery" or "wifi" or "bluetooth" or "sound" or "device" or "app"))
        {
            _report.Line($"MENU unknown {kind}");
            return;
        }

        if (kind == "app" && (view.InCardView || view.Deck.ActiveCard is null))
        {
            _report.Line("MENU app none");
            return;
        }

        CloseOtherShellUi(view, keepMenu: true);
        var menu = view.Menu ??= CreateMenu(view);
        var reopen = menu.Open && menu.Kind != kind;
        menu.Kind = kind;
        menu.Model.Kind = kind;
        _armed = null;
        if (kind == "app")
        {
            _expanded.Clear();
            FillAppMenu(view, menu);
        }
        else
        {
            FillSystemMenu(view, menu, rebuild: true);
        }

        PlaceMenu(view, menu);
        menu.Chrome.Enabled = true;
        menu.Chrome.InputEnabled = true;
        if (!menu.Open || reopen)
        {
            menu.Open = true;
            menu.Fade.Start(LunaMotion.StatusBarMenuFade, reopen ? 1 : 0, 1, Now, 1);
            menu.Frame.Alpha = (float)menu.Fade.Value;
        }

        view.Scheduler?.ScheduleRepaint();
        _report.Line($"MENU {kind} on");
    }

    private void CloseMenu(LunaView view)
    {
        if (view.Menu is not { Open: true } menu)
        {
            return;
        }

        menu.Open = false;
        menu.Chrome.InputEnabled = false;
        menu.Fade.Start(LunaMotion.StatusBarMenuFade, menu.Fade.Value, 0, Now, 1);
        view.Scheduler?.ScheduleRepaint();
        _seat.Refresh();
        _report.Line($"MENU {menu.Kind} off");
    }

    private MenuSurface CreateMenu(LunaView view)
    {
        var menu = new MenuSurface(view.Menus, _ui, _chromeIndex);
        menu.Model.Dismissed += () => CloseMenu(view);
        menu.Model.Chosen += row => MenuChosen(view, menu, row);
        menu.Fade.Jump(0);
        menu.Frame.Alpha = 0;
        return menu;
    }

    private static void PlaceMenu(LunaView view, MenuSurface menu)
    {
        var width = menu.Kind == "app" ? AppMenuWidth : MenuWidth;
        menu.Model.PanelWidth = width;
        menu.Model.PanelLeft = menu.Kind == "app" ? 0 : Math.Max(0, view.Box.Width - width);
        menu.Place(new Box(0, LunaView.StatusBarHeight, view.Box.Width, Math.Max(1, view.Box.Height - LunaView.StatusBarHeight)), view.Scale);
    }

    private bool AdvanceMenu(LunaView view, long now)
    {
        if (view.Menu is not { } menu || !menu.Fade.Advance(now))
        {
            return false;
        }

        menu.Frame.Alpha = (float)menu.Fade.Value;
        if (!menu.Fade.IsRunning && !menu.Open)
        {
            menu.Chrome.Enabled = false;
        }

        return menu.Fade.IsRunning;
    }

    private void SettleMenu(LunaView view)
    {
        if (view.Menu is { } menu && menu.Fade.IsRunning)
        {
            menu.Fade.Settle();
            menu.Frame.Alpha = (float)menu.Fade.Value;
            menu.Chrome.Enabled = menu.Open;
        }
    }

    private void FillSystemMenu(LunaView view, MenuSurface menu, bool rebuild)
    {
        var rows = menu.Model.Rows;
        if (!rebuild && rows.Count > 0 && menu.Kind is "sound" or "battery")
        {
            SyncSliders(view, menu);
            return;
        }

        rows.Clear();
        switch (menu.Kind)
        {
            case "battery":
                rows.Add(new HeaderRow { Label = "Battery", Detail = BatteryText() });
                if (_brightness is { } brightness && brightness.Supports(view.Output))
                {
                    rows.Add(new DividerRow());
                    rows.Add(new SliderRow { Key = "brightness", Label = "Brightness", Value = BrightnessOf(view), Changed = value => SetBrightness(view, value) });
                }

                rows.Add(new DividerRow());
                AddDeviceRows(rows);
                break;
            case "device":
                rows.Add(new HeaderRow { Label = "Device", Detail = Environment.MachineName });
                rows.Add(new DividerRow());
                AddDeviceRows(rows);
                break;
            case "wifi":
                FillWifi(rows);
                break;
            case "bluetooth":
                FillBluetooth(rows);
                break;
            case "sound":
                if (_volume is not { IsAvailable: true } volume)
                {
                    rows.Add(new HeaderRow { Label = "Sound" });
                    rows.Add(new NoteRow { Label = "No sound output" });
                    break;
                }

                rows.Add(new HeaderRow { Label = "Sound", Detail = $"{Math.Round(volume.Volume * 100)}%" });
                rows.Add(new SliderRow { Key = "volume", Label = "Volume", Value = Math.Min(1, volume.Volume), Changed = value => _volume?.SetVolume((float)value) });
                rows.Add(new DividerRow());
                rows.Add(new ToggleRow { Key = "mute", Label = "Mute", On = volume.Muted, Changed = muted => _volume?.SetMuted(muted) });
                break;
        }
    }

    private void SyncSliders(LunaView view, MenuSurface menu)
    {
        foreach (var row in menu.Model.Rows)
        {
            switch (row)
            {
                case SliderRow { Key: "volume" } volumeRow when _volume is { IsAvailable: true } volume:
                    volumeRow.Sync(Math.Min(1, volume.Volume));
                    break;
                case SliderRow { Key: "brightness" } brightnessRow:
                    brightnessRow.Sync(BrightnessOf(view));
                    break;
                case ToggleRow { Key: "mute" } mute when _volume is { } sink:
                    mute.On = sink.Muted;
                    break;
                case HeaderRow header when menu.Kind == "battery":
                    header.Detail = BatteryText();
                    break;
                case HeaderRow header when menu.Kind == "sound" && _volume is { } sound:
                    header.Detail = $"{Math.Round(sound.Volume * 100)}%";
                    break;
            }
        }
    }

    private string BatteryText()
    {
        if (_battery is not { IsAvailable: true, IsPresent: true } battery)
        {
            return _battery is { IsAvailable: true } ? "No battery" : "Not available";
        }

        var percent = $"{Math.Round(battery.Percentage).ToString(CultureInfo.InvariantCulture)}%";
        return battery.State switch
        {
            BatteryState.Charging => $"Charging, {percent}",
            BatteryState.Full => "Charged",
            _ => percent,
        };
    }

    private double BrightnessOf(LunaView view)
    {
        if (_brightness is not { } brightness || !brightness.TryGet(view.Output, out var value))
        {
            return 1;
        }

        var max = brightness.Max(view.Output);
        return max == 0 ? 1 : value / (double)max;
    }

    private void SetBrightness(LunaView view, double value)
    {
        if (_brightness is { } brightness && brightness.Supports(view.Output))
        {
            var max = brightness.Max(view.Output);
            brightness.Set(view.Output, (uint)Math.Clamp(Math.Round(value * max), 1, max));
        }
    }

    private void AddDeviceRows(System.Collections.ObjectModel.ObservableCollection<MenuRow> rows)
    {
        rows.Add(new ActionRow { Key = "lock", Label = "Lock", Invoked = Lock });
        rows.Add(new ActionRow { Key = "restart", Label = "Restart" });
        rows.Add(new ActionRow { Key = "shutdown", Label = "Shut Down" });
    }

    private void FillWifi(System.Collections.ObjectModel.ObservableCollection<MenuRow> rows)
    {
        if (_network is not { IsAvailable: true, HasWirelessDevice: true } network)
        {
            rows.Add(new HeaderRow { Label = "Wi-Fi" });
            rows.Add(new NoteRow { Label = _network?.Reason ?? "No Wi-Fi device" });
            return;
        }

        rows.Add(new ToggleRow { Key = "wifi", Label = "Wi-Fi", On = network.WirelessEnabled, Changed = on => _ = network.SetWirelessEnabled(on) });
        if (network.WirelessEnabled)
        {
            rows.Add(new DividerRow());
            foreach (var point in network.AccessPoints)
            {
                var secured = point.Security != AccessPointSecurity.Open;
                rows.Add(new ActionRow
                {
                    Key = $"ap:{point.Ssid}",
                    Label = point.Ssid,
                    Detail = $"{(secured ? "secured  " : "")}{point.Strength}%",
                    Checked = point.IsActive,
                    Invoked = () => JoinNetwork(network, point),
                });
            }
        }

        rows.Add(new DividerRow());
        rows.Add(new NoteRow { Key = "settings", Label = "Use your network settings to join a new secured network." });
    }

    private void JoinNetwork(NetworkStatus network, NetworkAccessPoint point)
    {
        if (point.IsActive)
        {
            return;
        }

        if (point.Saved is not null)
        {
            _ = network.ActivateSaved(point);
            _report.Line($"WIFI join {point.Ssid} saved");
            return;
        }

        if (point.Security == AccessPointSecurity.Open)
        {
            _ = network.ActivateOpen(point);
            _report.Line($"WIFI join {point.Ssid} open");
            return;
        }

        _report.Line($"WIFI join {point.Ssid} needs-settings");
    }

    private void FillBluetooth(System.Collections.ObjectModel.ObservableCollection<MenuRow> rows)
    {
        if (_bluetooth is not { IsAvailable: true, HasAdapter: true } bluetooth)
        {
            rows.Add(new HeaderRow { Label = "Bluetooth" });
            rows.Add(new NoteRow { Label = _bluetooth?.Reason ?? "No Bluetooth adapter" });
            return;
        }

        rows.Add(new ToggleRow { Key = "bluetooth", Label = "Bluetooth", On = bluetooth.Powered, Changed = on => _ = bluetooth.SetPowered(on) });
        if (!bluetooth.Powered)
        {
            return;
        }

        rows.Add(new DividerRow());
        if (bluetooth.Devices.Count == 0)
        {
            rows.Add(new NoteRow { Label = "No paired devices" });
        }

        foreach (var device in bluetooth.Devices)
        {
            rows.Add(new ActionRow
            {
                Key = $"bt:{device.Address}",
                Label = device.Name,
                Detail = device.Connected ? "Connected" : "",
                Checked = device.Connected,
                Invoked = () =>
                {
                    _ = device.Connected ? bluetooth.Disconnect(device) : bluetooth.Connect(device);
                    _report.Line($"BLUETOOTH {(device.Connected ? "disconnect" : "connect")} {device.Address}");
                },
            });
        }
    }

    private void MenuChosen(LunaView view, MenuSurface menu, MenuRow row)
    {
        _report.Line($"MENUCHOSE {menu.Kind} {(row.Key.Length > 0 ? row.Key : row.Label)}");
        switch (row)
        {
            case ActionRow { Key: "restart" or "shutdown" } power:
                if (_armed != power.Key)
                {
                    _armed = power.Key;
                    power.Label = power.Key == "restart" ? "Tap again to restart" : "Tap again to shut down";
                    return;
                }

                _armed = null;
                CloseMenu(view);
                RunPower(power.Key == "restart" ? LogindAction.Reboot : LogindAction.PowerOff);
                return;
            case ActionRow { Key: "lock" }:
                CloseMenu(view);
                return;
            case ActionRow when menu.Kind == "app":
                AppMenuRowChosen(view, menu, (ActionRow)row);
                return;
        }
    }

    private void RunPower(LogindAction action)
    {
        _report.Line($"POWER {action.ToString().ToLowerInvariant()}");
        if (Environment.GetEnvironmentVariable("LUNAWM_NO_POWER") == "1" || !LogindActions.Available)
        {
            _log.Info($"{action} skipped: no logind, or LUNAWM_NO_POWER is set");
            return;
        }

        _ = LogindActions.RunAsync(action);
    }

    private void PrintMenuState(LunaView view)
    {
        if (view.Menu is not { Open: true } menu)
        {
            _report.Line("  menu closed");
            return;
        }

        _report.Line($"  menu {menu.Kind} rows={menu.Model.Rows.Count}");
        foreach (var row in menu.Model.Rows)
        {
            var text = row switch
            {
                HeaderRow header => $"header {header.Label} | {header.Detail}",
                ToggleRow toggle => $"toggle {toggle.Label} {toggle.State}",
                SliderRow slider => $"slider {slider.Label} {slider.Value:0.##}",
                ActionRow action => $"action {action.Label}{(action.Checked ? " checked" : "")}{(action.Detail.Length > 0 ? $" | {action.Detail}" : "")}",
                NoteRow note => $"note {note.Label}",
                _ => "divider",
            };
            _report.Line($"  row {text}");
        }
    }
}
