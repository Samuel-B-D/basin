namespace Basin.Ipc;

public readonly record struct IpcDeviceOutcome(ulong Device, string Key, string Result);
