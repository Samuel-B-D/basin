namespace Basin.Capabilities;

public interface IInputDeviceConfiguration
{
    int Enumerate(Span<InputDeviceInfo> devices);

    bool TryGet(ulong deviceId, InputSetting setting, out InputSettingValue value);

    bool TryGetDefault(ulong deviceId, InputSetting setting, out InputSettingValue value);

    InputSettingSupport Support(ulong deviceId, InputSetting setting);

    InputSettingResult Set(ulong deviceId, InputSetting setting, in InputSettingValue value);

    InputSettingResult Reset(ulong deviceId, InputSetting setting);

    InputSettingResult SetAccelCurve(ulong deviceId, InputAccelCurveType type, double step, ReadOnlySpan<double> points);

    event Action<InputDeviceInfo>? DeviceAdded;

    event Action<ulong>? DeviceRemoved;

    event Action<ulong, InputSetting>? Changed;
}
