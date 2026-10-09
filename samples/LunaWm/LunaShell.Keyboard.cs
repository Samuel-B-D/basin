using System.Text.Json;
using Basin;
using Basin.Audio.PipeWire;
using Basin.Desktop;
using Basin.Ipc;
using Basin.Scene;
using Basin.SoftKeyboard;

namespace LunaWm;

internal sealed partial class LunaShell
{
    private const string KeyboardEvent = "lunawm/keyboard";
    private const double KeyboardEdgeTravel = 60;

    private readonly SoftKeyboardTextInput _softInput = new();
    private readonly List<KeyboardLayout> _kbLayouts = [];
    private readonly KeyboardTouch[] _kbTouches = new KeyboardTouch[SoftKeyboardEngine.MaxTouches];
    private SoftKeyboardEngine _kb = null!;
    private KeyboardRouter _kbRouter = null!;
    private SoftKeyboardTimer? _kbTimer;
    private KeyboardModel _kbModel = null!;
    private TextInputManager? _textInputs;
    private InputMethodRelay? _imRelay;
    private Action _kbIdle = null!;
    private bool _kbIdleQueued;
    private int _kbLayoutIndex;
    private KeyboardSize _kbSize = KeyboardSize.Medium;
    private bool _kbSizeChosen;
    private string? _kbSavedLayout;
    private LunaView? _kbView;
    private bool _kbManual;
    private bool _kbAuto;
    private bool _kbPending;
    private bool _kbProbing;
    private long _kbClockFloor;
    private ShiftMode _kbShiftReported;
    private BottomRow _kbBottom;
    private LastInput _lastInput = LastInput.Pointer;
    private SoundClip? _kbClick;
    private SoundClip? _kbSpace;
    private SoundClip? _kbDelete;
    private SoundClip? _kbReturn;
    private SoundClip? _kbTick;

    internal enum LastInput
    {
        Pointer,

        Touch,

        Keyboard,
    }

    private struct KeyboardTouch
    {
        public int Id;
        public bool Active;
        public bool Edge;
        public bool Claimed;
        public bool Bar;
        public double LastX;
        public double X0;
        public double Y0;
        public uint Began;
        public Basin.Seat.VelocityTracker Tracker;
    }

    internal long KeyboardNow => Math.Max(Environment.TickCount64, _kbClockFloor);

    internal bool KeyboardShown => _kbView is { Keyboard.Shown: true };

    private void InstallKeyboard(ProtocolPack pack)
    {
        foreach (var module in pack)
        {
            if (module is InputMethodModule inputMethod)
            {
                inputMethod.Fallback = _softInput;
            }
        }
    }

    private void AttachKeyboard()
    {
        _textInputs = _services.Find<TextInputManager>();
        _imRelay = _services.Find<InputMethodRelay>();
        Seat.Keyboard.FocusChanged += OnKeyboardFocus;
        Seat.Keyboard.KeymapChanged += OnKeymapForKeyboard;
        if (_imRelay is { } relay)
        {
            relay.AvailabilityChanged += OnInputMethodAvailability;
        }

        if (_config.Keyboard.Enabled)
        {
            Seat.SetCapability(Basin.Seat.SeatCapability.Keyboard, true);
        }

        LoadKeyboardState();
        LoadKeyboardLayouts();
        _kb = new SoftKeyboardEngine(CurrentKeyboardGrid(), _config.Keyboard.Timing) { Diamonds = _config.Keyboard.Diamonds };
        _kbModel = new KeyboardModel(_kb) { Layout = CurrentKeyboardLayout, Size = _kbSize };
        _kbRouter = new KeyboardRouter(_softInput, new SeatKeyOutput(Seat.Keyboard))
        {
            SpacesToPeriod = _config.Keyboard.SpacesToPeriod,
            Commit = _textInputs,
        };
        _kbRouter.Attach(_kb, () => KeyboardNow);
        _kbRouter.Sent += OnKeySent;
        StartFcitx5();
        AttachTrace();
        _kbTimer = new SoftKeyboardTimer(_host.Loop, _kb, () => KeyboardNow);
        _kbIdle = () =>
        {
            _kbIdleQueued = false;
            FlushKeyboard();
        };
        _kb.Invalidated += _kbModel.Invalidate;
        _kb.State.Changed += OnKeyboardStateChanged;
        _kb.Clicked += PlayKeySound;
        _kb.LongPressOpened += (_, _) => PlayClip(_kbTick);
        _kb.HideRequested += () => HideKeyboard("key");
        _kb.SizeChosen += ChooseKeyboardSize;
        _kb.LanguageNext += () => SelectKeyboardLayout((_kbLayoutIndex + 1) % Math.Max(1, _kbLayouts.Count));
        _kb.LanguageChosen += SelectKeyboardLayout;
        _softInput.Activated += OnTextInputActivated;
        _softInput.Deactivated += OnTextInputDeactivated;
        _softInput.FieldChanged += OnFieldChanged;
        foreach (var view in _views)
        {
            AttachKeyboardSurface(view);
        }
    }

    private void StopKeyboard()
    {
        StopFcitx5();
        StopTrace();
        _kbTimer?.Dispose();
        _kbTimer = null;
    }

    private void AttachKeyboardSurface(LunaView view)
    {
        if (_kbModel is null || view.Keyboard is not null)
        {
            return;
        }

        view.Keyboard = new KeyboardSurface(view.InputMethod, _ui, _chromeIndex, _kbModel);
        view.Keyboard.Slide.Jump(0);
    }

    private KeyboardLayout CurrentKeyboardLayout => _kbLayouts[Math.Clamp(_kbLayoutIndex, 0, _kbLayouts.Count - 1)];

    private void LoadKeyboardLayouts()
    {
        _kbLayouts.Clear();
        var ids = _config.Keyboard.Layouts.Count > 0
            ? _config.Keyboard.Layouts
            : [KeyboardLayouts.DefaultFor(Environment.GetEnvironmentVariable("LC_ALL") is { Length: > 0 } all ? all : Environment.GetEnvironmentVariable("LANG"))];
        var user = KeyboardConfig.UserLayoutDirectory();
        foreach (var id in ids)
        {
            var layout = id == KeyboardLayouts.Xkb ? BuildXkbLayout() : KeyboardLayouts.Load(id, user, _log);
            if (layout is null)
            {
                _log.Warn($"keyboard: layout '{id}' cannot be loaded");
                continue;
            }

            _kbLayouts.Add(layout);
        }

        if (_kbLayouts.Count == 0)
        {
            _kbLayouts.Add(KeyboardLayouts.Load("us-qwerty")!);
        }

        _kbLayoutIndex = 0;
        if (_kbSavedLayout is { } saved)
        {
            var index = _kbLayouts.FindIndex(layout => layout.Id == saved);
            if (index >= 0)
            {
                _kbLayoutIndex = index;
            }
        }
    }

    private KeyboardLayout? BuildXkbLayout()
    {
        if (Seat.Keyboard.Keymap is not { } keymap || KeyboardLayouts.Load("us-qwerty") is not { } symbols)
        {
            return null;
        }

        var layout = Seat.Keyboard.State?.SerializeLayout(Xkb.XkbStateComponent.LayoutEffective) ?? 0;
        return XkbGrid.Build(keymap, layout, symbols);
    }

    private KeyGrid CurrentKeyboardGrid()
    {
        IReadOnlyList<KeyCap>? languages = null;
        if (_kbLayouts.Count >= 2)
        {
            var caps = new KeyCap[_kbLayouts.Count];
            for (var i = 0; i < caps.Length; i++)
            {
                caps[i] = KeyCap.LanguageChoice(i, _kbLayouts[i].LanguageLabel);
            }

            languages = caps;
        }

        _kbBottom = _softInput.Field.BottomRow;
        var layout = CurrentKeyboardLayout;
        return layout.Grid(_kbBottom, languages, layout.LanguageLabel);
    }

    private void RebuildKeyboardGrid()
    {
        _kb.Grid = CurrentKeyboardGrid();
        _kb.State.LayoutNumLock = CurrentKeyboardLayout.NumLock;
        _kbModel.Layout = CurrentKeyboardLayout;
        if (_kbView is { } view)
        {
            PlaceKeyboard(view, immediate: true);
        }

        _kbModel.Invalidate();
    }

    private void SelectKeyboardLayout(int index)
    {
        if (index < 0 || index >= _kbLayouts.Count)
        {
            return;
        }

        _kbLayoutIndex = index;
        _kbSavedLayout = CurrentKeyboardLayout.Id;
        SaveKeyboardState();
        RebuildKeyboardGrid();
        UpdateTraceEnabled();
        _report.Line($"LAYOUT {CurrentKeyboardLayout.Id}");
    }

    private void OnKeymapForKeyboard()
    {
        if (_kb is null)
        {
            return;
        }

        var xkb = _kbLayouts.FindIndex(layout => layout.Id == KeyboardLayouts.Xkb);
        if (xkb < 0 || BuildXkbLayout() is not { } rebuilt)
        {
            return;
        }

        _kbLayouts[xkb] = rebuilt;
        if (xkb == _kbLayoutIndex)
        {
            RebuildKeyboardGrid();
        }
    }

    private void ChooseKeyboardSize(KeyboardSize size)
    {
        _kbSize = size;
        _kbSizeChosen = true;
        _kbModel.Size = size;
        SaveKeyboardState();
        if (_kbView is { } view)
        {
            PlaceKeyboard(view, immediate: true);
        }

        _report.Line($"KBSIZE {KeyboardConfig.SizeName(size)}");
        ReportKeyboard();
    }

    private void OnKeyboardFocus(Surface? surface)
    {
        _textInputs?.NotifyFocus(surface);
        if (_kbView is { } view && (surface is null || OwnerOf(surface) is not { } card || !ReferenceEquals((card.Parent ?? card).Home, view) ||
            !ReferenceEquals(card.Parent ?? card, view.Deck.ActiveCard)))
        {
            _kbManual = false;
            _kbAuto = false;
        }

        ApplyKeyboard();
    }

    private void OnInputMethodAvailability()
    {
        _softInput.ExternalInputMethod = _imRelay?.HasInputMethodClient ?? false;
        SyncFcitx5Field();
        ReportKeyboard();
    }

    private void OnTextInputActivated(Surface surface)
    {
        SyncFcitx5Field();
        if (_lastInput == LastInput.Touch)
        {
            _kbAuto = true;
        }

        ApplyKeyboard();
    }

    private void OnTextInputDeactivated()
    {
        SyncFcitx5Field();
        _kbAuto = false;
        ApplyKeyboard();
    }

    private void OnFieldChanged(FieldState field)
    {
        if (field.BottomRow != _kbBottom && _kb is not null)
        {
            RebuildKeyboardGrid();
        }
    }

    internal void NoteInput(LastInput input) => _lastInput = input;

    internal bool IsInputMethodKeyboard(object? tag) => _imRelay?.IsInputMethodClient(tag) ?? false;

    internal void NoteHardwareKey(bool pressed)
    {
        if (!pressed)
        {
            return;
        }

        _lastInput = LastInput.Keyboard;
        if (_kbManual || _kbAuto)
        {
            _kbManual = false;
            _kbAuto = false;
            ApplyKeyboard();
        }
    }

    internal void NoteClientTouch(Surface surface)
    {
        _lastInput = LastInput.Touch;
        if (_softInput.ActiveSurface is { } active && OwnerOf(active) is { } owner && ReferenceEquals(OwnerOf(surface), owner) && !_kbAuto)
        {
            _kbAuto = true;
            ApplyKeyboard();
        }
    }

    internal void ToggleKeyboard()
    {
        if (KeyboardShown)
        {
            HideKeyboard("toggle");
            return;
        }

        RaiseKeyboard();
    }

    internal void RaiseKeyboard()
    {
        _kbManual = true;
        ApplyKeyboard();
    }

    internal void HideKeyboard(string why)
    {
        _kbManual = false;
        _kbAuto = false;
        ApplyKeyboard();
        _report.Line($"KBHIDE {why}");
    }

    private LunaView? KeyboardTarget()
    {
        if (!_config.Keyboard.Enabled || _locked || _sessionLocked || (!_kbManual && !_kbAuto))
        {
            return null;
        }

        if (Focused is not { } card || (card.Parent ?? card).Home is not { } view || view.InCardView)
        {
            return null;
        }

        if (LauncherOpen(view) || JustTypeOpen(view) || MenuOpen(view) || DashboardOpen(view))
        {
            return null;
        }

        return view;
    }

    private void ApplyKeyboard()
    {
        if (_kb is null)
        {
            return;
        }

        var target = KeyboardTarget();
        if (ReferenceEquals(target, _kbView))
        {
            return;
        }

        if (_seat.LiveContacts > 0)
        {
            _kbPending = true;
            return;
        }

        _kbPending = false;
        if (_kbView is { } old)
        {
            CloseKeyboardOn(old, animate: target is null);
        }

        _kbView = target;
        if (target is not null)
        {
            OpenKeyboardOn(target);
        }
        else
        {
            _kbManual = false;
            _kb.Reset();
            _kb.State.ClearSymbolLock();
        }

        ReportKeyboard();
    }

    internal void KeyboardTouchesEnded()
    {
        if (_kbPending)
        {
            ApplyKeyboard();
        }
    }

    private KeyboardMetrics KeyboardMetricsFor(LunaView view) =>
        KeyboardMetrics.For(_kbSize, view.Box.Width, view.Box.Height, _kbSizeChosen);

    private int KeyboardInset(LunaView view) => KeyboardMetricsFor(view).Height + (int)_kbModel.BarHeight;

    private void PlaceKeyboard(LunaView view, bool immediate)
    {
        if (view.Keyboard is not { } surface)
        {
            return;
        }

        var metrics = KeyboardMetricsFor(view);
        var inset = metrics.Height + (int)_kbModel.BarHeight;
        surface.Height = inset;
        _kbModel.Width = view.Box.Width;
        _kbModel.Height = inset;
        _kbModel.Padding = metrics.Padding;
        _kb.Layout(view.Box.Width, metrics.KeymapHeight, metrics.Padding);
        var room = (int)KeyboardModel.PopupRoom;
        surface.Chrome.Place(new Box(0, view.Box.Height - inset - room, view.Box.Width, inset + room), view.Scale);
        surface.Frame.Matrix = RenderTransform.Translation(0, (1 - surface.Slide.Value) * inset);
        if (immediate && surface.Shown && view.CardInset != inset)
        {
            view.CardInset = inset;
            ApplyCardInset(view);
        }

        _kbModel.Invalidate();
    }

    private void OpenKeyboardOn(LunaView view)
    {
        if (view.Keyboard is not { } surface)
        {
            return;
        }

        surface.Shown = true;
        _kbModel.BarHeight = Fcitx5BarHeight;
        Fcitx5Visible(true);
        UpdateTraceEnabled();
        PlaceKeyboard(view, immediate: false);
        surface.Chrome.Enabled = true;
        surface.Slide.Start(LunaMotion.PositiveSpace, surface.Slide.Value, 1, Now, TimeScale);
        surface.Frame.Matrix = RenderTransform.Translation(0, (1 - surface.Slide.Value) * surface.Height);
        if (!surface.Slide.IsRunning)
        {
            view.CardInset = surface.Height;
            ApplyCardInset(view);
        }

        view.Scheduler?.ScheduleRepaint();
    }

    private void CloseKeyboardOn(LunaView view, bool animate)
    {
        if (view.Keyboard is not { } surface)
        {
            return;
        }

        surface.Shown = false;
        CancelKeyboardTouches();
        Fcitx5Visible(false);
        if (view.CardInset != 0)
        {
            view.CardInset = 0;
            ApplyCardInset(view);
        }

        if (animate)
        {
            surface.Slide.Start(LunaMotion.PositiveSpace, surface.Slide.Value, 0, Now, TimeScale);
        }
        else
        {
            surface.Slide.Jump(0);
        }

        if (!surface.Slide.IsRunning)
        {
            surface.Chrome.Enabled = false;
        }

        view.Scheduler?.ScheduleRepaint();
    }

    private void ApplyCardInset(LunaView view)
    {
        if (view.Deck.ActiveCard is { Fullscreen: false } card)
        {
            SizeCard(card, view);
        }

        if (!view.InCardView)
        {
            LayoutCards(view, animate: false);
        }

        _report.Line($"INSET {view.Output.Name} {view.CardInset}");
    }

    private bool AdvanceKeyboard(LunaView view, long now)
    {
        var candidates = ReferenceEquals(view, _kbView) && (AdvanceCandidates(now) | AdvanceTrace(now));
        if (view.Keyboard is not { } surface || !surface.Slide.Advance(now))
        {
            return candidates;
        }

        surface.Frame.Matrix = RenderTransform.Translation(0, (1 - surface.Slide.Value) * surface.Height);
        if (!surface.Slide.IsRunning)
        {
            if (surface.Shown)
            {
                view.CardInset = surface.Height;
                ApplyCardInset(view);
            }
            else
            {
                surface.Chrome.Enabled = false;
            }
        }

        return surface.Slide.IsRunning || candidates;
    }

    private void SettleKeyboard(LunaView view)
    {
        if (view.Keyboard is { } surface && surface.Slide.IsRunning)
        {
            surface.Slide.Settle();
            AdvanceKeyboard(view, long.MaxValue);
            surface.Frame.Matrix = RenderTransform.Translation(0, (1 - surface.Slide.Value) * surface.Height);
            if (surface.Shown && view.CardInset != surface.Height)
            {
                view.CardInset = surface.Height;
                ApplyCardInset(view);
            }
            else if (!surface.Shown)
            {
                surface.Chrome.Enabled = false;
            }
        }
    }

    private Box AppBoxFor(LunaView view, Card card)
    {
        var box = view.AppBox;
        if (view.CardInset > 0 && ReferenceEquals(card, view.Deck.ActiveCard))
        {
            return box with { Height = Math.Max(1, box.Height - view.CardInset) };
        }

        return box;
    }

    private bool KeymapOrigin(LunaView view, out double x, out double y)
    {
        x = view.Box.X;
        y = 0;
        if (view.Keyboard is not { Shown: true } surface)
        {
            return false;
        }

        y = view.Box.Y + view.Box.Height - (surface.Height * surface.Slide.Value) + _kbModel.BarHeight + _kbModel.Padding;
        return true;
    }

    internal bool KeyboardCovers(double x, double y)
    {
        if (_kbView is not { } view || !KeymapOrigin(view, out var ox, out var oy))
        {
            return false;
        }

        var local = x - view.Box.X;
        return local >= 0 && local < view.Box.Width && y >= oy - _kbModel.Padding - _kbModel.BarHeight && y < view.Box.Y + view.Box.Height;
    }

    internal bool KeyboardTouchDown(int id, double x, double y, uint timeMs)
    {
        if (_kbView is not { } view || !KeymapOrigin(view, out var ox, out var oy))
        {
            return false;
        }

        var now = KeyboardNow;
        var kx = (float)(x - ox);
        var ky = (float)(y - oy);
        var barTop = oy - _kbModel.Padding - _kbModel.BarHeight;
        if (_kbModel.BarHeight > 0 && !_kb.IsLongPressOpen && x >= view.Box.X && y >= barTop && y < oy - _kbModel.Padding)
        {
            var barSlot = KeyboardSlot(id, allocate: true);
            if (barSlot < 0)
            {
                return false;
            }

            _kbTouches[barSlot] = new KeyboardTouch { Id = id, Active = true, Bar = true, X0 = x, Y0 = y, LastX = x, Began = timeMs };
            BarTouchDown();
            return true;
        }

        if (!_kb.Accepts(kx, ky, now))
        {
            return false;
        }

        var slot = KeyboardSlot(id, allocate: true);
        if (slot < 0)
        {
            return false;
        }

        ref var touch = ref _kbTouches[slot];
        touch = new KeyboardTouch
        {
            Id = id,
            Active = true,
            Edge = y >= view.Box.Y + view.Box.Height - _config.EdgeBand,
            X0 = x,
            Y0 = y,
            Began = timeMs,
        };
        touch.Tracker.Reset();
        touch.Tracker.Add(x, y, 0);
        _kb.TouchDown(id, kx, ky, now);
        if (!_kbIdleQueued)
        {
            _kbIdleQueued = true;
            _host.Loop.AddIdle(_kbIdle);
        }

        return true;
    }

    internal void KeyboardTouchMotion(int id, double x, double y, uint timeMs)
    {
        var slot = KeyboardSlot(id, allocate: false);
        if (slot < 0 || _kbView is not { } view)
        {
            return;
        }

        ref var touch = ref _kbTouches[slot];
        if (touch.Bar)
        {
            BarTouchMove(x - touch.LastX);
            touch.LastX = x;
            return;
        }

        touch.Tracker.Add(x, y, (long)(uint)(timeMs - touch.Began) * 1_000_000);
        if (touch.Claimed)
        {
            EdgeMove(x, y, timeMs);
            return;
        }

        if (touch.Edge)
        {
            var inward = touch.Y0 - y;
            var along = x - touch.X0;
            var axis = inward >= KeyboardEdgeTravel && inward >= Math.Abs(along) ? EdgeAxis.Inward
                : Math.Abs(along) >= KeyboardEdgeTravel && Math.Abs(along) > inward ? EdgeAxis.Along
                : (EdgeAxis?)null;
            if (axis is { } claim)
            {
                _kb.Cancel(id);
                _edge = new EdgeState { View = view, Edge = LunaEdge.Bottom };
                if (EdgeClaim(LunaEdge.Bottom, claim, touch.X0, touch.Y0, timeMs))
                {
                    touch.Claimed = true;
                    EdgeMove(x, y, timeMs);
                    return;
                }

                touch.Edge = false;
            }
        }

        if (KeymapOrigin(view, out var ox, out var oy))
        {
            _kb.TouchMotion(id, (float)(x - ox), (float)(y - oy), KeyboardNow);
        }
    }

    internal void KeyboardTouchUp(int id, uint timeMs)
    {
        var slot = KeyboardSlot(id, allocate: false);
        if (slot < 0)
        {
            return;
        }

        ref var touch = ref _kbTouches[slot];
        if (touch.Bar)
        {
            touch.Active = false;
            BarTouchUp(touch.X0, touch.LastX);
            return;
        }

        var claimed = touch.Claimed;
        var (vx, vy) = touch.Tracker.Velocity((long)(uint)(timeMs - touch.Began) * 1_000_000);
        touch.Active = false;
        if (claimed)
        {
            EdgeEnd(vx, vy, timeMs, cancelled: false);
            return;
        }

        _kb.TouchUp(id, KeyboardNow);
    }

    internal void CancelKeyboardTouches()
    {
        if (_kb is null)
        {
            return;
        }

        for (var i = 0; i < _kbTouches.Length; i++)
        {
            if (_kbTouches[i].Active && _kbTouches[i].Claimed)
            {
                EdgeEnd(0, 0, 0, cancelled: true);
            }

            _kbTouches[i].Active = false;
        }

        _kb.CancelAll();
        _kb.Reset();
    }

    private int KeyboardSlot(int id, bool allocate)
    {
        var free = -1;
        for (var i = 0; i < _kbTouches.Length; i++)
        {
            if (_kbTouches[i].Active && _kbTouches[i].Id == id)
            {
                return i;
            }

            if (free < 0 && !_kbTouches[i].Active)
            {
                free = i;
            }
        }

        return allocate ? free : -1;
    }

    private void FlushKeyboard()
    {
        _kb.Frame(KeyboardNow);
    }

    private void OnKeySent(TypedKey key, KeyRoute route)
    {
        _lastInput = LastInput.Touch;
        if (_kbProbing)
        {
            return;
        }

        _report.Line($"KEY {KeyName(key.Cap, key.Text)} {RouteName(route)}{(key.WordDelete ? " word" : "")}");
    }

    private void OnKeyboardStateChanged()
    {
        _kbModel.Invalidate();
        var shift = _kb.State.Shift;
        if (shift != _kbShiftReported)
        {
            _kbShiftReported = shift;
            if (!_kbProbing)
            {
                _report.Line($"SHIFT {shift.ToString().ToLowerInvariant()}");
            }
        }
    }

    private static string KeyName(KeyCap cap, string text) => cap.Function switch
    {
        KeyFunction.Text when cap.IsSpace => "space",
        KeyFunction.Text => text,
        _ => cap.Name,
    };

    private static string RouteName(KeyRoute route) => route switch
    {
        KeyRoute.Backend => "fcitx5",
        KeyRoute.TextInput => "text-input",
        _ => "keys",
    };


    private void ReportKeyboard()
    {
        if (_kb is null)
        {
            return;
        }

        var height = _kbView is { } view ? KeyboardInset(view) : 0;
        _report.Line($"KEYBOARD {(_kbView is null ? "off" : "on")} {height} layout={CurrentKeyboardLayout.Id} size={KeyboardConfig.SizeName(_kbSize)} route={RouteName(_kbRouter.Route)} {PredictState}");
        RaiseKeyboardEvent(height);
    }

    private void RaiseKeyboardEvent(int height)
    {
        if (_ipc is not { } ipc || !ipc.Events.HasSubscribers(KeyboardEvent))
        {
            return;
        }

        var json = ipc.Events.BeginEvent(KeyboardEvent);
        json.WriteStartObject();
        json.WriteString("state"u8, _kbView is null ? "off" : "on");
        json.WriteNumber("height"u8, height);
        if (_kbView is { } view)
        {
            json.WriteString("output"u8, view.Output.Name);
        }

        json.WriteString("layout"u8, CurrentKeyboardLayout.Id);
        json.WriteString("size"u8, KeyboardConfig.SizeName(_kbSize));
        json.WriteEndObject();
        ipc.Events.EndEvent();
    }

    private void PlayKeySound(KeyCap cap)
    {
        if (_kbProbing)
        {
            return;
        }

        PlayClip(cap.Function switch
        {
            KeyFunction.Backspace => _kbDelete,
            KeyFunction.Return => _kbReturn,
            KeyFunction.Text when cap.IsSpace => _kbSpace,
            _ => _kbClick,
        });
    }

    private void PlayClip(SoundClip? clip)
    {
        if (!_config.Keyboard.Sounds || Environment.GetEnvironmentVariable("BASIN_MUTE") == "1")
        {
            return;
        }

        if (!EnsureSounds())
        {
            return;
        }

        if (_kbClick is null)
        {
            _kbClick = LoadClip("click_001");
            _kbSpace = LoadClip("click_002");
            _kbDelete = LoadClip("click_003");
            _kbReturn = LoadClip("click_004");
            _kbTick = LoadClip("tick_001");
            clip ??= _kbClick;
        }

        if (clip is not null)
        {
            _soundPlayer!.Play(clip, SoundPlacement.Centered);
        }
    }

    private void LoadKeyboardState()
    {
        _kbSize = _config.Keyboard.Size;
        _kbSizeChosen = false;
        _kbSavedLayout = null;
        try
        {
            var path = KeyboardStatePath();
            if (!File.Exists(path))
            {
                return;
            }

            var file = JsonSerializer.Deserialize(File.ReadAllText(path), KeyboardJsonContext.Default.KeyboardFile);
            if (file?.Size is { } size)
            {
                _kbSize = KeyboardConfig.ParseSize(size, _kbSize);
                _kbSizeChosen = true;
            }

            _kbSavedLayout = file?.Layout;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            _log.Warn($"keyboard: cannot read the state file: {error.Message}");
        }
    }

    private void SaveKeyboardState()
    {
        try
        {
            var path = KeyboardStatePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var file = new KeyboardFile
            {
                Size = _kbSizeChosen ? KeyboardConfig.SizeName(_kbSize) : null,
                Layout = _kbSavedLayout,
            };
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(file, KeyboardJsonContext.Default.KeyboardFile));
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            _log.Warn($"keyboard: cannot write the state file: {error.Message}");
        }
    }

    private static string KeyboardStatePath() => Path.Combine(Path.GetDirectoryName(LauncherState.DefaultPath())!, "keyboard.json");

    private void ReloadKeyboard(LunaConfig previous)
    {
        if (_kb is null)
        {
            return;
        }

        if (_config.Keyboard.Fcitx5 != previous.Keyboard.Fcitx5)
        {
            if (_config.Keyboard.Fcitx5)
            {
                StartFcitx5();
            }
            else
            {
                StopFcitx5();
            }
        }

        var savedSize = _kbSize;
        var chosen = _kbSizeChosen;
        _kb.Timing = _config.Keyboard.Timing;
        _kb.Diamonds = _config.Keyboard.Diamonds;
        _kbRouter.SpacesToPeriod = _config.Keyboard.SpacesToPeriod;
        if (!_config.Keyboard.Layouts.SequenceEqual(previous.Keyboard.Layouts))
        {
            LoadKeyboardLayouts();
            RebuildKeyboardGrid();
        }

        if (!chosen)
        {
            _kbSize = _config.Keyboard.Size;
            _kbModel.Size = _kbSize;
            if (_kbView is { } view)
            {
                PlaceKeyboard(view, immediate: true);
            }
        }
        else if (_config.Keyboard.Size != previous.Keyboard.Size && _config.Keyboard.Size != savedSize)
        {
            _report.Line($"KBSIZE {KeyboardConfig.SizeName(savedSize)} kept keyboard.size={KeyboardConfig.SizeName(_config.Keyboard.Size)}");
        }

        ApplyKeyboard();
    }

    private void PrintKeyboardState()
    {
        if (_kb is null)
        {
            return;
        }

        _report.Line($"  keyboard {(_kbView is null ? "off" : "on")} layout={CurrentKeyboardLayout.Id} size={KeyboardConfig.SizeName(_kbSize)} shift={_kb.State.Shift.ToString().ToLowerInvariant()} symbol={(_kb.State.SymbolLock ? "lock" : "off")} field={_softInput.Field.Kind.ToString().ToLowerInvariant()} autocap={(_kb.State.AutoCap ? "on" : "off")} last={_lastInput.ToString().ToLowerInvariant()}");
    }

    private int FindKeyboardCell(string name)
    {
        var grid = _kb.Grid;
        var wanted = name switch
        {
            "space" => " ",
            "sym" => "symbol",
            "lang" => "language",
            "enter" => "return",
            _ => name,
        };
        for (var cell = 0; cell < KeyGrid.CellCount; cell++)
        {
            if (grid[cell] is { IsVisible: true, Plain: { } plain } &&
                (plain.IsText ? plain.Text == wanted || plain.Text == wanted.ToLowerInvariant() : plain.Name == wanted))
            {
                return cell;
            }
        }

        return -1;
    }

    private long KeyboardStep(int milliseconds)
    {
        _kbClockFloor = KeyboardNow + milliseconds;
        return _kbClockFloor;
    }

    private bool KeyboardTap(int cell, int stepMs = 600)
    {
        if (cell < 0 || _kbView is null)
        {
            return false;
        }

        _lastInput = LastInput.Touch;
        _kb.TapCell(KeyboardCommandTouch, cell, KeyboardStep(stepMs));
        return true;
    }

    private const int KeyboardCommandTouch = 900;

    private bool KeyboardTypeCharacter(char c)
    {
        var text = c.ToString();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var cell = ProducingCell(text);
            if (cell >= 0)
            {
                return KeyboardTap(cell);
            }

            if (LongPressFor(text, out var holder, out var index))
            {
                KeyboardHoldCell(holder, 0);
                _kb.ChooseLongPress(index, KeyboardStep(100));
                return true;
            }

            var state = _kb.State;
            if (char.IsLetter(c) && !CellFor(char.ToLowerInvariant(c).ToString(), alternate: true, out _))
            {
                KeyboardTap(FindKeyboardCell("shift"));
                continue;
            }

            if (CellFor(text, alternate: true, out var alternateCell))
            {
                var plain = _kb.Grid[alternateCell].Plain!;
                KeyboardTap(FindKeyboardCell(plain.IsLetter ? "symbol" : "shift"));
                continue;
            }

            if (CellFor(text, alternate: false, out _))
            {
                KeyboardTap(FindKeyboardCell(state.SymbolActive ? "symbol" : "shift"));
                continue;
            }

            return false;
        }

        return false;
    }

    private int ProducingCell(string text)
    {
        var state = _kb.State;
        for (var cell = 0; cell < KeyGrid.CellCount; cell++)
        {
            if (_kb.Grid[cell] is not { IsVisible: true } key || state.Resolve(key) is not { IsText: true } cap)
            {
                continue;
            }

            var produced = cap.Text.Length == 1 && state.Capital ? cap.UpperText : cap.Text;
            if (produced == text)
            {
                return cell;
            }
        }

        return -1;
    }

    private bool CellFor(string text, bool alternate, out int found)
    {
        for (var cell = 0; cell < KeyGrid.CellCount; cell++)
        {
            if (_kb.Grid[cell] is not { IsVisible: true } key)
            {
                continue;
            }

            var cap = alternate ? key.Alternate : key.Plain;
            if (cap is { IsText: true } && (cap.Text == text || (cap.Text.Length == 1 && cap.UpperText == text && !alternate)))
            {
                found = cell;
                return true;
            }
        }

        found = -1;
        return false;
    }

    private bool LongPressFor(string text, out int cell, out int index)
    {
        for (cell = 0; cell < KeyGrid.CellCount; cell++)
        {
            if (_kb.Grid[cell] is not { IsVisible: true, LongPress: { } entries })
            {
                continue;
            }

            for (index = 1; index < entries.Count; index++)
            {
                if (entries[index].IsText && entries[index].Text == text)
                {
                    return true;
                }
            }
        }

        cell = -1;
        index = -1;
        return false;
    }

    private void KeyboardHoldCell(int cell, int releaseAfterMs)
    {
        var rect = _kb.Rects[cell];
        var start = KeyboardStep(600);
        _lastInput = LastInput.Touch;
        _kb.TouchDown(KeyboardCommandTouch, rect.CenterX, rect.CenterY, start);
        _kb.Frame(start);
        var deadline = _kb.NextDeadline;
        if (deadline != long.MaxValue)
        {
            _kbClockFloor = Math.Max(_kbClockFloor, deadline);
            _kb.Tick(deadline);
        }

        if (releaseAfterMs <= 0)
        {
            _kb.TouchUp(KeyboardCommandTouch, KeyboardNow);
        }
    }

    private void RegisterKeyboardCommands(IpcMethodRegistry methods)
    {
        _report.Register(methods, "lunawm/keyboard", "keyboard [{state}]",
            Info("Raise, hide or toggle the on-screen keyboard by hand, or print its KEYBOARD line.",
                """{"type":"object","properties":{"state":{"type":"string","enum":["show","hide","toggle"]}}}"""),
            (ref IpcParams p, IpcReply _) =>
            {
                var state = p.TryGetString("state", out var given) ? given : "";
                switch (state)
                {
                    case "show":
                        RaiseKeyboard();
                        break;
                    case "hide":
                        HideKeyboard("command");
                        break;
                    case "toggle":
                        ToggleKeyboard();
                        break;
                    default:
                        ReportKeyboard();
                        break;
                }
            });

        _report.Register(methods, "lunawm/keyboard-tap", "keyboard-tap {key}",
            Info("Press and release one keyboard key by its cap: a letter, shift, space, backspace, return, sym, hide, lang, tab, .com."),
            (ref IpcParams p, IpcReply reply) =>
            {
                var key = p.GetString("key");
                if (!p.Failed && !KeyboardTap(FindKeyboardCell(key), 100))
                {
                    reply.Error(IpcErrorCodes.NotFound, _kbView is null ? "the keyboard is not shown" : $"no key {key}");
                }
            });

        _report.Register(methods, "lunawm/keyboard-type", "keyboard-type {text...}",
            Info("Tap each character's key in turn through the keyboard engine, with Shift, the symbol page and long-press lists as needed."),
            (ref IpcParams p, IpcReply reply) =>
            {
                var text = p.GetString("text");
                if (p.Failed)
                {
                    return;
                }

                if (_kbView is null)
                {
                    reply.Error(IpcErrorCodes.InvalidParams, "the keyboard is not shown");
                    return;
                }

                foreach (var c in text)
                {
                    if (!KeyboardTypeCharacter(c))
                    {
                        reply.Error(IpcErrorCodes.NotFound, $"no key types '{c}'");
                        return;
                    }
                }
            });

        _report.Register(methods, "lunawm/keyboard-hold", "keyboard-hold {key} {ms:int}",
            Info("Press a keyboard key, hold it for ms milliseconds of keyboard time, and release it. A long press opens the key's list."),
            (ref IpcParams p, IpcReply reply) =>
            {
                var key = p.GetString("key");
                var ms = (int)p.GetInt("ms");
                if (p.Failed)
                {
                    return;
                }

                var cell = FindKeyboardCell(key);
                if (cell < 0 || _kbView is null)
                {
                    reply.Error(IpcErrorCodes.NotFound, $"no key {key}");
                    return;
                }

                var rect = _kb.Rects[cell];
                var start = KeyboardStep(600);
                _lastInput = LastInput.Touch;
                _kb.TouchDown(KeyboardCommandTouch, rect.CenterX, rect.CenterY, start);
                _kb.Frame(start);
                var end = start + Math.Max(0, ms);
                while (_kb.NextDeadline <= end)
                {
                    _kb.Tick(_kb.NextDeadline);
                }

                _kbClockFloor = Math.Max(_kbClockFloor, end);
                _kb.TouchUp(KeyboardCommandTouch, end);
                if (_kb.IsLongPressOpen)
                {
                    _report.Line($"LONGPRESS {string.Join(',', _kb.LongPressEntries!.Select(static e => e.IsText ? e.Text : e.Label ?? e.Name))}");
                }
            });

        _report.Register(methods, "lunawm/keyboard-choose", "keyboard-choose {index:int}",
            Info("Choose an entry of the open long-press list, counted from 0."),
            (ref IpcParams p, IpcReply reply) =>
            {
                var index = (int)p.GetInt("index");
                if (p.Failed)
                {
                    return;
                }

                if (!_kb.IsLongPressOpen || index < 0 || index >= _kb.LongPressEntries!.Count)
                {
                    reply.Error(IpcErrorCodes.InvalidParams, "no such long-press entry");
                    return;
                }

                _kb.ChooseLongPress(index, KeyboardStep(100));
            });

        _report.Register(methods, "lunawm/keyboard-candidate", "keyboard-candidate {index:int}",
            Info("Tap candidate N in the keyboard's candidate bar, counted from 0, as fcitx5 last listed them."),
            (ref IpcParams p, IpcReply reply) =>
            {
                var index = (int)p.GetInt("index");
                if (p.Failed)
                {
                    return;
                }

                if (index < 0 || index >= _kbModel.Candidates.Count)
                {
                    reply.Error(IpcErrorCodes.InvalidParams, "no such candidate");
                    return;
                }

                ChooseCandidate(index);
            });

        _report.Register(methods, "lunawm/keyboard-trace", "keyboard-trace {word}",
            Info("Run a synthetic trace through the word's key centers, through the engine and the decoder, and commit what the decoder finds."),
            (ref IpcParams p, IpcReply reply) =>
            {
                var word = p.GetString("word");
                if (!p.Failed && !TraceWord(word))
                {
                    reply.Error(IpcErrorCodes.InvalidParams, _kbView is null ? "the keyboard is not shown" : "trace typing is off here, or the word has a letter the layout lacks");
                }
            });

        _report.Register(methods, "lunawm/keyboard-size", "keyboard-size {size}",
            Info("Set the keyboard size as the Hide key's list does: xs, s, m or l.",
                """{"type":"object","properties":{"size":{"type":"string","enum":["xs","s","m","l"]}},"required":["size"]}"""),
            (ref IpcParams p, IpcReply _) =>
            {
                var size = p.GetString("size");
                if (!p.Failed)
                {
                    ChooseKeyboardSize(KeyboardConfig.ParseSize(size, _kbSize));
                }
            });

        _report.Register(methods, "lunawm/keyboard-layout", "keyboard-layout {id}",
            Info("Switch the keyboard to a layout in [keyboard] layouts, by id."),
            (ref IpcParams p, IpcReply reply) =>
            {
                var id = p.GetString("id");
                if (p.Failed)
                {
                    return;
                }

                var index = _kbLayouts.FindIndex(layout => layout.Id == id);
                if (index < 0)
                {
                    reply.Error(IpcErrorCodes.NotFound, $"no layout {id} in [keyboard] layouts");
                    return;
                }

                SelectKeyboardLayout(index);
            });
    }

    private string KeyboardMenuLabel => KeyboardShown ? "Hide Keyboard" : "Show Keyboard";
}
