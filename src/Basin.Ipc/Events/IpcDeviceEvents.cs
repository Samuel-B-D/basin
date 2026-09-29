using Basin.Capabilities;

namespace Basin.Ipc;

internal sealed class IpcDeviceEvents : IpcCoalescedSource
{
    private readonly IInputDeviceConfiguration _configuration;
    private readonly Action<InputDeviceInfo> _added;
    private readonly Action<ulong> _removed;
    private readonly Action<ulong, InputSetting> _changed;
    private readonly List<Pending> _pending = [];
    private readonly HashSet<(ulong Device, InputSetting Setting)> _changes = [];

    public IpcDeviceEvents(IpcServer server, IInputDeviceConfiguration configuration)
        : base(server)
    {
        _configuration = configuration;
        _added = OnAdded;
        _removed = OnRemoved;
        _changed = OnChanged;
    }

    protected override void Attach()
    {
        _configuration.DeviceAdded += _added;
        _configuration.DeviceRemoved += _removed;
        _configuration.Changed += _changed;
    }

    protected override void Detach()
    {
        _configuration.DeviceAdded -= _added;
        _configuration.DeviceRemoved -= _removed;
        _configuration.Changed -= _changed;
        base.Detach();
    }

    protected override void Reset()
    {
        _pending.Clear();
        _changes.Clear();
    }

    protected override void Flush()
    {
        var sources = _configuration as IInputSettingSources;
        foreach (var pending in _pending)
        {
            switch (pending.Kind)
            {
                case PendingKind.Added:
                    Bus.Emit(IpcEventNames.DeviceAdded, IpcDeviceMethods.Describe(pending.Info, sources), IpcJsonContext.Default.IpcDevice);
                    break;
                case PendingKind.Removed:
                    Bus.Emit(IpcEventNames.DeviceRemoved, new IpcDeviceId(pending.Info.Id), IpcJsonContext.Default.IpcDeviceId);
                    break;
                default:
                    if (_configuration.TryGet(pending.Info.Id, pending.Setting, out var value))
                    {
                        Bus.Emit(
                            IpcEventNames.DeviceChanged,
                            new IpcDeviceChanged(pending.Info.Id, InputSettingNames.Key(pending.Setting), InputSettingNames.Format(pending.Setting, value)),
                            IpcJsonContext.Default.IpcDeviceChanged);
                    }

                    break;
            }
        }

        Reset();
    }

    private void OnAdded(InputDeviceInfo info)
    {
        _pending.Add(new Pending(PendingKind.Added, info, default));
        Arm();
    }

    private void OnRemoved(ulong id)
    {
        _pending.Add(new Pending(PendingKind.Removed, new InputDeviceInfo(id, string.Empty, default, default, null), default));
        Arm();
    }

    private void OnChanged(ulong id, InputSetting setting)
    {
        if (_changes.Add((id, setting)))
        {
            _pending.Add(new Pending(PendingKind.Changed, new InputDeviceInfo(id, string.Empty, default, default, null), setting));
        }

        Arm();
    }

    private enum PendingKind
    {
        Added,
        Removed,
        Changed,
    }

    private readonly record struct Pending(PendingKind Kind, InputDeviceInfo Info, InputSetting Setting);
}
