namespace LunaWm;

public sealed class StatusBarModel : ObservableModel
{
    private string _title = "";
    private string _clock = "";
    private int _batteryLevel = 11;
    private bool _batteryCharging;
    private bool _batteryShown;
    private bool _batteryError;
    private int _wifiBars;
    private bool _wifiShown;
    private bool _wifiConnecting;
    private bool _bluetoothShown;
    private bool _bluetoothConnected;
    private bool _volumeShown;
    private bool _muted;
    private double _volume = 1;
    private string _banner = "";
    private bool _bannerShown;
    private Avalonia.Media.IImage? _bannerIcon;
    private bool _locked;

    public string Title
    {
        get => _title;
        set => Set(ref _title, value);
    }

    public string Clock
    {
        get => _clock;
        set => Set(ref _clock, value);
    }

    public int BatteryLevel
    {
        get => _batteryLevel;
        set => Set(ref _batteryLevel, value);
    }

    public bool BatteryCharging
    {
        get => _batteryCharging;
        set => Set(ref _batteryCharging, value);
    }

    public bool BatteryShown
    {
        get => _batteryShown;
        set => Set(ref _batteryShown, value);
    }

    public bool BatteryError
    {
        get => _batteryError;
        set => Set(ref _batteryError, value);
    }

    public int WifiBars
    {
        get => _wifiBars;
        set => Set(ref _wifiBars, value);
    }

    public bool WifiShown
    {
        get => _wifiShown;
        set => Set(ref _wifiShown, value);
    }

    public bool WifiConnecting
    {
        get => _wifiConnecting;
        set => Set(ref _wifiConnecting, value);
    }

    public bool BluetoothShown
    {
        get => _bluetoothShown;
        set => Set(ref _bluetoothShown, value);
    }

    public bool BluetoothConnected
    {
        get => _bluetoothConnected;
        set => Set(ref _bluetoothConnected, value);
    }

    public bool VolumeShown
    {
        get => _volumeShown;
        set => Set(ref _volumeShown, value);
    }

    public bool Muted
    {
        get => _muted;
        set => Set(ref _muted, value);
    }

    public double Volume
    {
        get => _volume;
        set => Set(ref _volume, value);
    }

    public System.Collections.ObjectModel.ObservableCollection<StatusIconModel> NotificationIcons { get; } = [];

    public string Banner
    {
        get => _banner;
        set => Set(ref _banner, value);
    }

    public bool BannerShown
    {
        get => _bannerShown;
        set
        {
            if (Set(ref _bannerShown, value))
            {
                Raise(nameof(IconsShown));
            }
        }
    }

    public bool IconsShown => !_bannerShown;

    public Avalonia.Media.IImage? BannerIcon
    {
        get => _bannerIcon;
        set
        {
            if (Set(ref _bannerIcon, value))
            {
                Raise(nameof(HasBannerIcon));
            }
        }
    }

    public bool HasBannerIcon => _bannerIcon is not null;

    public bool Locked
    {
        get => _locked;
        set
        {
            if (Set(ref _locked, value))
            {
                Raise(nameof(TitleShown));
            }
        }
    }

    public bool TitleShown => !_locked;

    public event Action<string>? Pressed;

    public void Press(string what) => Pressed?.Invoke(what);
}
