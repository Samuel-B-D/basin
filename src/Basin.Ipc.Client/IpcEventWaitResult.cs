namespace Basin.Ipc;

public sealed record IpcEventWaitResult(IReadOnlyList<IpcEvent> Events, bool Stopped);
