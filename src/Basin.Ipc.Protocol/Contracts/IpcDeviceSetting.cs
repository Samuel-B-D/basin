namespace Basin.Ipc;

public readonly record struct IpcDeviceSetting(
    string Key,
    string Current,
    string Default,
    string Source,
    ReadOnlyMemory<string> Values);
