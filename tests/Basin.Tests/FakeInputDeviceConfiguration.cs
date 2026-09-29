using Basin.Capabilities;

namespace Basin.Tests;

internal sealed class FakeInputDeviceConfiguration : IInputDeviceConfiguration
{
    private readonly Dictionary<ulong, FakeDevice> _devices = [];
    private ulong _nextId;

    public event Action<InputDeviceInfo>? DeviceAdded;

    public event Action<ulong>? DeviceRemoved;

    public event Action<ulong, InputSetting>? Changed;

    public List<(ulong Device, InputSetting Setting, InputSettingValue Value)> Writes { get; } = [];

    public List<(ulong Device, InputSetting Setting)> UnsupportedReads { get; } = [];

    public List<(ulong Device, InputAccelCurveType Type, double Step, double[] Points)> Curves { get; } = [];

    public Func<ulong, InputSetting, InputSettingValue, bool>? Refuse { get; set; }

    public static (InputSetting, InputSettingValue)[] TouchpadDefaults =>
    [
        (InputSetting.Tap, InputSettingValue.Flag(false)),
        (InputSetting.Drag, InputSettingValue.Flag(true)),
        (InputSetting.NaturalScroll, InputSettingValue.Flag(false)),
        (InputSetting.ClickMethod, InputSettingValue.Of(InputClickMethod.ButtonAreas)),
        (InputSetting.ScrollMethod, InputSettingValue.Of(InputScrollMethod.TwoFinger)),
        (InputSetting.DisableWhileTyping, InputSettingValue.Flag(true)),
        (InputSetting.AccelProfile, InputSettingValue.Of(InputAccelProfile.Adaptive)),
        (InputSetting.AccelSpeed, InputSettingValue.Number(0)),
        (InputSetting.LeftHanded, InputSettingValue.Flag(false)),
    ];

    public static (InputSetting, InputSettingValue)[] MouseDefaults =>
    [
        (InputSetting.NaturalScroll, InputSettingValue.Flag(false)),
        (InputSetting.AccelProfile, InputSettingValue.Of(InputAccelProfile.Adaptive)),
        (InputSetting.AccelSpeed, InputSettingValue.Number(0)),
        (InputSetting.LeftHanded, InputSettingValue.Flag(false)),
        (InputSetting.MiddleEmulation, InputSettingValue.Flag(false)),
    ];

    public ulong AddTouchpad(string name = "Synaptics TM3276-022", uint vendor = 0x06cb, uint product = 0x0000) =>
        Add(name, InputDeviceCapability.Pointer | InputDeviceCapability.Gesture, InputDeviceType.Touchpad, TouchpadDefaults, vendor, product);

    public ulong AddMouse(string name = "Logitech G502", uint vendor = 0x046d, uint product = 0xc08b) =>
        Add(name, InputDeviceCapability.Pointer, InputDeviceType.Pointer, MouseDefaults, vendor, product);

    public ulong AddKeyboard(string name = "AT Translated Set 2 keyboard") =>
        Add(name, InputDeviceCapability.Keyboard, InputDeviceType.Keyboard, [(InputSetting.SendEvents, InputSettingValue.Of(InputSendEventsMode.Enabled))]);

    public ulong Add(
        string name,
        InputDeviceCapability capabilities,
        InputDeviceType type,
        (InputSetting Setting, InputSettingValue Default)[] settings,
        uint vendor = 0,
        uint product = 0,
        string? sysname = null,
        string? output = null)
    {
        var id = ++_nextId;
        var device = new FakeDevice(new InputDeviceInfo(id, name, capabilities, type, output, sysname ?? $"event{id}", vendor, product, 3));
        foreach (var (setting, value) in settings)
        {
            device.Defaults[setting] = value;
            device.Current[setting] = value;
        }

        _devices[id] = device;
        DeviceAdded?.Invoke(device.Info);
        return id;
    }

    public void Remove(ulong id)
    {
        if (_devices.Remove(id))
        {
            DeviceRemoved?.Invoke(id);
        }
    }

    public InputSettingValue Current(ulong id, InputSetting setting) => _devices[id].Current[setting];

    public int Enumerate(Span<InputDeviceInfo> devices)
    {
        if (_devices.Count > devices.Length)
        {
            return -1;
        }

        var written = 0;
        foreach (var device in _devices.Values)
        {
            devices[written++] = device.Info;
        }

        return written;
    }

    public InputSettingSupport Support(ulong deviceId, InputSetting setting) =>
        _devices.TryGetValue(deviceId, out var device) && device.Defaults.ContainsKey(setting)
            ? new InputSettingSupport(true, 0xff, 3)
            : InputSettingSupport.None;

    public bool TryGet(ulong deviceId, InputSetting setting, out InputSettingValue value) =>
        Read(deviceId, setting, current: true, out value);

    public bool TryGetDefault(ulong deviceId, InputSetting setting, out InputSettingValue value) =>
        Read(deviceId, setting, current: false, out value);

    public InputSettingResult Set(ulong deviceId, InputSetting setting, in InputSettingValue value)
    {
        if (!_devices.TryGetValue(deviceId, out var device))
        {
            return InputSettingResult.Invalid;
        }

        if (!device.Defaults.ContainsKey(setting))
        {
            return InputSettingResult.Unsupported;
        }

        if (Refuse?.Invoke(deviceId, setting, value) == true)
        {
            return InputSettingResult.Invalid;
        }

        Writes.Add((deviceId, setting, value));
        var previous = device.Current[setting];
        device.Current[setting] = value;
        if (!previous.Equals(value))
        {
            Changed?.Invoke(deviceId, setting);
        }

        return InputSettingResult.Success;
    }

    public InputSettingResult Reset(ulong deviceId, InputSetting setting) =>
        TryGetDefault(deviceId, setting, out var fallback) ? Set(deviceId, setting, fallback) : InputSettingResult.Unsupported;

    public InputSettingResult SetAccelCurve(ulong deviceId, InputAccelCurveType type, double step, ReadOnlySpan<double> points)
    {
        if (!_devices.ContainsKey(deviceId))
        {
            return InputSettingResult.Invalid;
        }

        Curves.Add((deviceId, type, step, points.ToArray()));
        return InputSettingResult.Success;
    }

    private bool Read(ulong deviceId, InputSetting setting, bool current, out InputSettingValue value)
    {
        value = default;
        if (!_devices.TryGetValue(deviceId, out var device))
        {
            return false;
        }

        if (!device.Defaults.ContainsKey(setting))
        {
            UnsupportedReads.Add((deviceId, setting));
            return false;
        }

        value = current ? device.Current[setting] : device.Defaults[setting];
        return true;
    }

    private sealed class FakeDevice(InputDeviceInfo info)
    {
        public InputDeviceInfo Info { get; } = info;

        public Dictionary<InputSetting, InputSettingValue> Defaults { get; } = [];

        public Dictionary<InputSetting, InputSettingValue> Current { get; } = [];
    }
}
