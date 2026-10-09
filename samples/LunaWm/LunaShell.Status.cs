using Basin;
using Basin.Audio.PipeWire;
using Basin.Capabilities;
using Basin.SystemStatus;

namespace LunaWm;

internal sealed partial class LunaShell
{
    private SystemStatusBus? _statusBus;
    private BatteryStatus? _battery;
    private NetworkStatus? _network;
    private BluetoothStatus? _bluetooth;
    private AudioSession? _audio;
    private SinkVolume? _volume;
    private IOutputBrightness? _brightness;

    private void StartStatus()
    {
        _brightness = _services.Find<IOutputBrightness>();
        _statusBus = new SystemStatusBus(_host.Loop);
        _battery = BatteryStatus.TryCreate(_statusBus, out var batteryReason);
        _network = NetworkStatus.TryCreate(_statusBus, out var networkReason);
        _bluetooth = BluetoothStatus.TryCreate(_statusBus, out var bluetoothReason);
        foreach (var reason in new[] { batteryReason, networkReason, bluetoothReason })
        {
            if (reason is not null)
            {
                _log.Info($"status: {reason}");
            }
        }

        if (_battery is not null)
        {
            _battery.Changed += OnStatusChanged;
        }

        if (_network is not null)
        {
            _network.Changed += OnStatusChanged;
        }

        if (_bluetooth is not null)
        {
            _bluetooth.Changed += OnStatusChanged;
        }

        _audio = AudioSession.TryCreate(_host.Loop, out var audioReason, clientName: "luna-wm");
        if (_audio is null)
        {
            _log.Info($"status: no volume indicator: {audioReason}");
        }
        else
        {
            _volume = new SinkVolume(_audio);
            _volume.Changed += _ => OnStatusChanged();
        }

        if (_brightness is not null)
        {
            _brightness.BrightnessChanged += _ => OnStatusChanged();
        }

        OnStatusChanged();
    }

    private void StopStatus()
    {
        _volume?.Dispose();
        _volume = null;
        _audio?.Dispose();
        _audio = null;
        _battery?.Dispose();
        _network?.Dispose();
        _bluetooth?.Dispose();
        _statusBus?.Dispose();
        _battery = null;
        _network = null;
        _bluetooth = null;
        _statusBus = null;
    }

    private void OnStatusChanged()
    {
        foreach (var view in _views)
        {
            SyncIndicators(view);
            if (view.Menu is { Open: true } menu && menu.Kind != "app")
            {
                FillSystemMenu(view, menu, rebuild: false);
            }
        }
    }

    private void SyncIndicators(LunaView view)
    {
        if (view.Bar is not { } bar)
        {
            return;
        }

        var model = bar.Model;
        var battery = _battery is { IsAvailable: true, IsPresent: true } ? _battery : null;
        model.BatteryShown = battery is not null;
        if (battery is not null)
        {
            model.BatteryLevel = (int)Math.Round(Math.Clamp(battery.Percentage, 0, 100) * 11 / 100);
            model.BatteryCharging = battery.State is BatteryState.Charging or BatteryState.PendingCharge;
            model.BatteryError = battery.State == BatteryState.Unknown;
        }

        var wifi = _network is { IsAvailable: true, HasWirelessDevice: true } ? _network : null;
        model.WifiShown = wifi is not null;
        if (wifi is not null)
        {
            var strength = wifi.PrimaryKind == NetworkConnectionKind.Wireless ? wifi.PrimaryStrength ?? 0 : 0;
            model.WifiBars = !wifi.WirelessEnabled || wifi.PrimaryKind != NetworkConnectionKind.Wireless ? 0 : strength >= 67 ? 3 : strength >= 34 ? 2 : 1;
        }

        var bluetooth = _bluetooth is { IsAvailable: true, HasAdapter: true, Powered: true } ? _bluetooth : null;
        model.BluetoothShown = bluetooth is not null;
        model.BluetoothConnected = bluetooth?.AnyConnected == true;

        model.VolumeShown = _volume is { IsAvailable: true };
        if (_volume is { IsAvailable: true } volume)
        {
            model.Muted = volume.Muted;
            model.Volume = volume.Volume;
        }
    }

    internal string IndicatorState
    {
        get
        {
            var battery = _battery is null ? "absent" : _battery.IsPresent ? $"{_battery.Percentage:0}%{(_battery.State == BatteryState.Charging ? "+" : "")}" : "none";
            var wifi = _network is { IsAvailable: true, HasWirelessDevice: true } network ? (network.WirelessEnabled ? $"on:{network.PrimaryStrength?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"}" : "off") : "hidden";
            var bluetooth = _bluetooth is { IsAvailable: true, HasAdapter: true } adapter ? (adapter.Powered ? (adapter.AnyConnected ? "connected" : "on") : "off") : "hidden";
            var volume = _volume is { IsAvailable: true } sink ? $"{sink.Volume:0.##}{(sink.Muted ? ":muted" : "")}" : "hidden";
            return $"battery={battery} wifi={wifi} bluetooth={bluetooth} volume={volume}";
        }
    }
}
