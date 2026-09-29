namespace Basin.Ipc;

public readonly record struct IpcDeviceResult(ReadOnlyMemory<IpcDeviceOutcome> Results);
