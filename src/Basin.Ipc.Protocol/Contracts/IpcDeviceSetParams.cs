using System.Text.Json;
using System.Text.Json.Serialization;

namespace Basin.Ipc;

public sealed class IpcDeviceSetParams : IIpcParams, IIpcReusable
{
    private Dictionary<string, JsonElement>? _settings;

    public IpcDeviceSetParams()
    {
    }

    public IpcDeviceSetParams(ulong? device, string? type, Dictionary<string, JsonElement> settings)
    {
        Device = device;
        Type = type;
        Settings = settings;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public ulong? Device { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; set; }

    public Dictionary<string, JsonElement>? Settings
    {
        get => _settings;
        set => _settings = value;
    }

    [JsonIgnore]
    public string? Missing =>
        _settings is null ? "'settings' is required"
        : Device is null && Type is null ? "'device' or 'type' is required"
        : null;

    void IIpcReusable.Reset()
    {
        Device = null;
        Type = null;
        _settings = null;
    }
}
