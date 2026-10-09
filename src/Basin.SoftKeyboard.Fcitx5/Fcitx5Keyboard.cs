using Basin.Diagnostics;
using Tmds.DBus.Protocol;
using static Basin.SoftKeyboard.Fcitx5.Fcitx5Log;

namespace Basin.SoftKeyboard.Fcitx5;

public sealed class Fcitx5Keyboard : IKeyboardBackend, IDisposable
{
    public const string BusName = "org.fcitx.Fcitx5.VirtualKeyboard";
    public const string BackendName = "org.fcitx.Fcitx5.VirtualKeyboardBackend";
    public const string BackendPath = "/virtualkeyboard";
    public const string BackendInterface = "org.fcitx.Fcitx5.VirtualKeyboardBackend1";
    public const string ServiceName = "org.fcitx.Fcitx5";
    public const string ServiceInterface = "org.fcitx.Fcitx.VirtualKeyboard1";

    private const uint FullFunctionMode = 1;

    private readonly ThreadAffinity _thread = ThreadAffinity.Capture();
    private readonly CompositorSynchronizationContext _context;
    private readonly PanelObject _panel;
    private DBusConnection? _connection;
    private IDisposable? _watch;
    private volatile bool _disposed;

    private Fcitx5Keyboard(ICompositorEventLoop loop, string address)
    {
        Address = address;
        _context = new CompositorSynchronizationContext(loop);
        _panel = new PanelObject(this);
        BasinCounters.Track();
    }

    public string Address { get; }

    public bool OwnsName { get; private set; }

    public bool BackendPresent { get; private set; }

    public bool HasFocusedField { get; set; }

    public bool IsActive => OwnsName && BackendPresent && HasFocusedField && !_disposed;

    public Fcitx5Candidates Candidates { get; private set; } = Fcitx5Candidates.Empty;

    public bool CandidatesArrived { get; private set; }

    public string Preedit { get; private set; } = string.Empty;

    public int PreeditCaret { get; private set; } = -1;

    public string? InputMethod { get; private set; }

    public event Action<bool>? BackendChanged;

    public event Action? CandidatesChanged;

    public event Action? PreeditChanged;

    public event Action<string?>? InputMethodChanged;

    public event Action<bool>? ShowRequested;

    public static Fcitx5Keyboard? TryCreate(ICompositorEventLoop loop, out string? reason) =>
        TryCreate(loop, new Fcitx5KeyboardOptions(), out reason);

    public static Fcitx5Keyboard? TryCreate(ICompositorEventLoop loop, Fcitx5KeyboardOptions options, out string? reason)
    {
        ArgumentNullException.ThrowIfNull(loop);
        ArgumentNullException.ThrowIfNull(options);
        var address = options.Address;
        if (string.IsNullOrEmpty(address))
        {
            address = Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS");
        }

        if (string.IsNullOrEmpty(address))
        {
            reason = "no session bus: DBUS_SESSION_BUS_ADDRESS is unset";
            return null;
        }

        var keyboard = new Fcitx5Keyboard(loop, address);
        string? failure;
        try
        {
            var connecting = Task.Run(() => keyboard.ConnectAsync(address));
            failure = connecting.Wait(options.ConnectTimeout)
                ? connecting.Result
                : $"the session bus at {address} did not answer within {options.ConnectTimeout.TotalSeconds:0.#} s";
        }
        catch (AggregateException error)
        {
            failure = $"the session bus at {address} is not reachable: {error.InnerException?.Message ?? error.Message}";
        }

        if (failure is not null)
        {
            keyboard.Dispose();
            reason = failure;
            return null;
        }

        reason = null;
        return keyboard;
    }

    public void ProcessKey(uint keysym, uint keycode, uint state, bool release, uint timeMs)
    {
        if (_connection is not { } connection || !BackendPresent)
        {
            return;
        }

        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(BackendName, BackendPath, BackendInterface, "ProcessKeyEvent", "uuubu", MessageFlags.NoReplyExpected);
        writer.WriteUInt32(keysym);
        writer.WriteUInt32(keycode);
        writer.WriteUInt32(state);
        writer.WriteBool(release);
        writer.WriteUInt32(timeMs);
        connection.TrySendMessage(writer.CreateMessage());
    }

    public void SelectCandidate(int index) => CallBackend("SelectCandidate", index);

    public void PreviousPage() => CallBackend("PrevPage");

    public void NextPage() => CallBackend("NextPage");

    public void SetVisible(bool visible)
    {
        if (_connection is not { } connection)
        {
            return;
        }

        using (var writer = connection.GetMessageWriter())
        {
            writer.WriteMethodCallHeader(ServiceName, BackendPath, ServiceInterface, visible ? "ShowVirtualKeyboard" : "HideVirtualKeyboard", null, MessageFlags.NoReplyExpected);
            connection.TrySendMessage(writer.CreateMessage());
        }

        if (BackendPresent)
        {
            using var writer = connection.GetMessageWriter();
            writer.WriteMethodCallHeader(BackendName, BackendPath, BackendInterface, "ProcessVisibilityEvent", "b", MessageFlags.NoReplyExpected);
            writer.WriteBool(visible);
            connection.TrySendMessage(writer.CreateMessage());
        }
    }

    public void ClearCandidates()
    {
        _thread.Assert();
        if (Candidates.Words.Count == 0 && Preedit.Length == 0)
        {
            return;
        }

        Candidates = Fcitx5Candidates.Empty;
        Preedit = string.Empty;
        CandidatesChanged?.Invoke();
        PreeditChanged?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _watch?.Dispose();
        _watch = null;
        Interlocked.Exchange(ref _connection, null)?.Dispose();
        OwnsName = false;
        BackendPresent = false;
        _context.Dispose();
        BasinCounters.Untrack();
    }

    internal void Post(Action<Fcitx5Keyboard> work)
    {
        if (_disposed)
        {
            return;
        }

        _context.Post(_ =>
        {
            if (!_disposed)
            {
                work(this);
            }
        }, null);
    }

    internal void OnShowRequested(bool show) => ShowRequested?.Invoke(show);

    internal void OnPreeditCaret(int caret)
    {
        PreeditCaret = caret;
        PreeditChanged?.Invoke();
    }

    internal void OnPreedit(string preedit)
    {
        Preedit = preedit;
        PreeditChanged?.Invoke();
    }

    internal void OnCandidates(Fcitx5Candidates candidates)
    {
        Candidates = candidates;
        if (candidates.Words.Count > 0)
        {
            CandidatesArrived = true;
        }

        CandidatesChanged?.Invoke();
    }

    internal void OnInputMethod(string name)
    {
        InputMethod = name;
        InputMethodChanged?.Invoke(name);
    }

    internal void OnInputMethodGone(string name)
    {
        if (InputMethod == name)
        {
            InputMethod = null;
            InputMethodChanged?.Invoke(null);
        }
    }

    private void OnBackendOwner(string? owner)
    {
        var present = !string.IsNullOrEmpty(owner);
        if (present == BackendPresent)
        {
            return;
        }

        BackendPresent = present;
        Log.Info($"fcitx5 virtual keyboard backend {(present ? $"appeared as {owner}" : "went away")}");
        if (present)
        {
            CallBackend("SetVirtualKeyboardFunctionMode", FullFunctionMode);
            if (_connection is { } connection)
            {
                _ = QueryInputMethodAsync(connection);
            }
        }
        else
        {
            Candidates = Fcitx5Candidates.Empty;
            Preedit = string.Empty;
        }

        BackendChanged?.Invoke(present);
    }

    private void CallBackend(string member)
    {
        if (_connection is not { } connection || !BackendPresent)
        {
            return;
        }

        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(BackendName, BackendPath, BackendInterface, member, null, MessageFlags.NoReplyExpected);
        connection.TrySendMessage(writer.CreateMessage());
    }

    private void CallBackend(string member, int value)
    {
        if (_connection is not { } connection || !BackendPresent)
        {
            return;
        }

        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(BackendName, BackendPath, BackendInterface, member, "i", MessageFlags.NoReplyExpected);
        writer.WriteInt32(value);
        connection.TrySendMessage(writer.CreateMessage());
    }

    private void CallBackend(string member, uint value)
    {
        if (_connection is not { } connection || !BackendPresent)
        {
            return;
        }

        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(BackendName, BackendPath, BackendInterface, member, "u", MessageFlags.NoReplyExpected);
        writer.WriteUInt32(value);
        connection.TrySendMessage(writer.CreateMessage());
    }

    private async Task<string?> ConnectAsync(string address)
    {
        DBusConnection? connection = null;
        try
        {
            connection = new DBusConnection(new DBusConnectionOptions(address)
            {
                AutoConnect = false,
                OnException = context =>
                {
                    if (!_disposed)
                    {
                        Log.Debug($"fcitx5 bus: {context.Exception.Message}");
                    }
                },
            });
            await connection.ConnectAsync().ConfigureAwait(false);
            connection.AddMethodHandler(_panel);
            if (!await connection.TryRequestNameAsync(BusName, RequestNameOptions.None).ConfigureAwait(false))
            {
                var owner = await OwnerAsync(connection, BusName).ConfigureAwait(false);
                connection.Dispose();
                return $"{BusName} is owned by {owner ?? "another process"}";
            }

            _watch = await connection.AddMatchAsync(
                new MatchRule
                {
                    Type = MessageType.Signal,
                    Sender = "org.freedesktop.DBus",
                    Path = "/org/freedesktop/DBus",
                    Interface = "org.freedesktop.DBus",
                    Member = "NameOwnerChanged",
                    Arg0 = BackendName,
                },
                static (Message message, object? _) =>
                {
                    var reader = message.GetBodyReader();
                    reader.ReadString();
                    reader.ReadString();
                    return reader.ReadString();
                },
                (Action<Notification<string>>)(notification =>
                {
                    if (notification.HasValue)
                    {
                        var owner = notification.Value;
                        Post(keyboard => keyboard.OnBackendOwner(owner));
                    }
                }),
                false, ObserverFlags.None, null).ConfigureAwait(false);
            var current = await OwnerAsync(connection, BackendName).ConfigureAwait(false);
            OwnsName = true;
            _connection = connection;
            if (_disposed)
            {
                Interlocked.Exchange(ref _connection, null)?.Dispose();
                return "closed while connecting";
            }

            Post(keyboard => keyboard.OnBackendOwner(current));
            return null;
        }
        catch (Exception error)
        {
            connection?.Dispose();
            return $"the session bus at {address} is not reachable: {error.Message}";
        }
    }

    private async Task QueryInputMethodAsync(DBusConnection connection)
    {
        try
        {
            var name = await connection.CallMethodAsync(
                ControllerCall(connection),
                static (Message message, object? _) => message.GetBodyReader().ReadString(),
                null).ConfigureAwait(false);
            Post(keyboard =>
            {
                if (keyboard.InputMethod is null && name.Length > 0)
                {
                    keyboard.OnInputMethod(name);
                }
            });
        }
        catch (Exception error) when (error is DBusExceptionBase or ObjectDisposedException)
        {
            Log.Debug($"fcitx5 CurrentInputMethod: {error.Message}");
        }
    }

    private static MessageBuffer ControllerCall(DBusConnection connection)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(ServiceName, "/controller", "org.fcitx.Fcitx.Controller1", "CurrentInputMethod", null);
        return writer.CreateMessage();
    }

    private static async Task<string?> OwnerAsync(DBusConnection connection, string name)
    {
        try
        {
            return await connection.CallMethodAsync(
                OwnerCall(connection, name),
                static (Message message, object? _) => message.GetBodyReader().ReadString(),
                null).ConfigureAwait(false);
        }
        catch (DBusExceptionBase)
        {
            return null;
        }
    }

    private static MessageBuffer OwnerCall(DBusConnection connection, string name)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader("org.freedesktop.DBus", "/org/freedesktop/DBus", "org.freedesktop.DBus", "GetNameOwner", "s");
        writer.WriteString(name);
        return writer.CreateMessage();
    }
}
