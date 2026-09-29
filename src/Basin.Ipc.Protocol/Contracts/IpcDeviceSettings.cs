namespace Basin.Ipc;

public readonly record struct IpcDeviceSettings(ulong Device, string Name, ReadOnlyMemory<IpcDeviceSetting> Settings);
