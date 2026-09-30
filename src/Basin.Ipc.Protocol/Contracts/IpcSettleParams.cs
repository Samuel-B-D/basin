using System.Text.Json.Serialization;

namespace Basin.Ipc;

public sealed class IpcSettleParams : IIpcParams, IIpcReusable
{
    public IpcSettleParams()
    {
    }

    public IpcSettleParams(long? timeoutMs)
    {
        TimeoutMs = timeoutMs;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? TimeoutMs { get; set; }

    [JsonIgnore]
    public string? Missing => null;

    void IIpcReusable.Reset()
    {
        TimeoutMs = null;
    }
}
