namespace Basin.Ipc;

public readonly record struct IpcDeviceChanged(ulong Device, string Key, string Value);
