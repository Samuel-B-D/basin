namespace Basin.Ipc;

public readonly record struct IpcDeviceList(ReadOnlyMemory<IpcDevice> Devices);
