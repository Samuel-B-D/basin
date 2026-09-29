namespace Basin.Ipc;

public readonly record struct IpcDevice(
    ulong Id,
    string Name,
    string Type,
    ReadOnlyMemory<string> Capabilities,
    string? Sysname,
    uint Vendor,
    uint Product,
    uint Bus,
    string? Output,
    ReadOnlyMemory<int> Rules);
