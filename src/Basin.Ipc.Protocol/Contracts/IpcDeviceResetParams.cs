using System.Text.Json.Serialization;

namespace Basin.Ipc;

public sealed class IpcDeviceResetParams : IIpcParams, IIpcReusable
{
    private ulong _device;

    private int _present;

    public IpcDeviceResetParams()
    {
    }

    public IpcDeviceResetParams(ulong device, IReadOnlyList<string>? keys = null)
    {
        Device = device;
        Keys = keys;
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

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Keys { get; set; }

    [JsonIgnore]
    public string? Missing =>
        (_present & 1) == 0 ? "'device' is required"
        : null;

    void IIpcReusable.Reset()
    {
        _device = default;
        _present = 0;
        Keys = null;
    }
}
