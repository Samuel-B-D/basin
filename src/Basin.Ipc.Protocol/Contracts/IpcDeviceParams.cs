using System.Text.Json.Serialization;

namespace Basin.Ipc;

public sealed class IpcDeviceParams : IIpcParams, IIpcReusable
{
    private ulong _device;

    private int _present;

    public IpcDeviceParams()
    {
    }

    public IpcDeviceParams(ulong device)
    {
        Device = device;
    }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public ulong Device
    {
        get => _device;
        set
        {
            _device = value;
            _present |= 1;
        }
    }

    [JsonIgnore]
    public string? Missing =>
        (_present & 1) == 0 ? "'device' is required"
        : null;

    void IIpcReusable.Reset()
    {
        _device = default;
        _present = 0;
    }
}
