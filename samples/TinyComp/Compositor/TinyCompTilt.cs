using System.Globalization;
using Basin;
using Basin.Kinematics;
using Basin.Physics;
using Basin.Seat;

namespace TinyComp;

internal sealed partial class TinyComp
{
    private const string TiltName = "tilt";
    private const double TiltStiffness = 300;
    private const double TiltEpsilon = 0.0005;

    private readonly Dictionary<IGrabTarget, WindowTilt> _tilts = [];
    private readonly List<IGrabTarget> _tiltsDone = [];

    private sealed class WindowTilt
    {
        public double Angle { get; set; }

        public double Target { get; set; }

        public SpringHandoff Spring;

        public bool Moving { get; set; }

        public Fling Spin;

        public bool Spinning { get; set; }
    }

    private const double SpinFriction = 2.5;
    private const double SpinStop = 0.05;
    private const double SpinThreshold = 0.3;

    private readonly TwistGesture _twistGesture = new();
    private readonly TwistGesture _windowTwistGesture = new() { Fingers = 3 };
    private readonly HoldDragGesture _holdDragGesture = new() { Fingers = 2 };
    private double _holdTurnBase;
    private double _holdTurn;
    private bool _holdTurning;
    private IGrabTarget? _twisting;
    private VelocityTracker _padTwist;
    private double _padTwistTotal;
    private uint _padTwistStart;

    internal double AngleOf(IGrabTarget window) =>
        _physicsBindings.TryGetValue(window, out var binding) && binding.Body.IsValid
            ? binding.Angle
            : _tilts.TryGetValue(window, out var tilt) ? tilt.Angle : 0;

    internal bool IsHeldTilt(IGrabTarget window) => _tilts.ContainsKey(window);

    internal bool IsSpinning(IGrabTarget window) =>
        (_tilts.TryGetValue(window, out var tilt) && tilt.Spinning) ||
        (_physicsBindings.TryGetValue(window, out var binding) && binding.Body is { IsValid: true } body &&
            Math.Abs(body.AngularVelocity) > SpinStop);

    internal void RotateWindow(IGrabTarget window, double radians, bool relative)
    {
        if (window.EffectTree is not { IsDestroyed: false })
        {
            return;
        }

        var current = AngleOf(window);
        var target = Math.IEEERemainder(relative ? current + radians : radians, Math.Tau);
        if (Math.Abs(target) < 1e-9)
        {
            target = 0;
        }

        if (!_tilts.TryGetValue(window, out var tilt))
        {
            tilt = new WindowTilt { Angle = current };
            _tilts[window] = tilt;
        }

        StopSpin(window, report: false);
        tilt.Target = target;
        tilt.Spring = new SpringHandoff(TiltStiffness, 1.0) { Epsilon = TiltEpsilon };
        tilt.Spring.Start(current, 0, 0, 0, current + Math.IEEERemainder(target - current, Math.Tau), 0);
        tilt.Moving = true;
        if (_physicsBindings.TryGetValue(window, out var binding) && binding.Body.IsValid)
        {
            var body = binding.Body;
            body.Upright = false;
        }

        _report.Line(target == 0
            ? $"UPRIGHT {NameOf(window)}"
            : string.Create(CultureInfo.InvariantCulture, $"ROTATE {NameOf(window)} angle={target * 180 / Math.PI:F1}"));
        ScheduleEffectRepaint();
    }

    private void ApplyTouchSettings()
    {
        if (_config.WindowTwistFingers > 0 && !_windowTwistGesture.IsClaimed)
        {
            _windowTwistGesture.Fingers = _config.WindowTwistFingers;
        }

        _holdDragGesture.HoldMillis = (uint)_config.HoldMillis;
    }

    internal void UprightWindow(IGrabTarget window) => RotateWindow(window, 0, relative: false);

    internal void StraightenForFill(IGrabTarget window)
    {
        if (AngleOf(window) != 0 || IsSpinning(window))
        {
            UprightWindow(window);
        }
    }

    private bool StepTilts(in FrameTick tick)
    {
        if (_tilts.Count == 0)
        {
            return false;
        }

        var running = false;
        _tiltsDone.Clear();
        foreach (var (window, tilt) in _tilts)
        {
            if (window.EffectTree is not { IsDestroyed: false })
            {
                _tiltsDone.Add(window);
                continue;
            }

            if (tilt.Spinning)
            {
                var spinning = tilt.Spin.Step(tick);
                SetAngle(window, tilt, Math.IEEERemainder(tilt.Spin.X, Math.Tau));
                tilt.Target = tilt.Angle;
                tilt.Spinning = spinning;
                running |= spinning;
                continue;
            }

            if (!tilt.Moving)
            {
                if (!IsBound(window))
                {
                    ApplyTilt(window, tilt.Angle);
                }

                continue;
            }

            var moving = tilt.Spring.Step(tick);
            var angle = moving ? tilt.Spring.X : tilt.Target;
            SetAngle(window, tilt, angle);
            if (moving)
            {
                running = true;
                continue;
            }

            tilt.Moving = false;
            if (tilt.Target == 0)
            {
                _tiltsDone.Add(window);
            }
        }

        foreach (var window in _tiltsDone)
        {
            ForgetTilt(window);
        }

        return running;
    }

    private WindowTilt Hold(IGrabTarget window)
    {
        if (!_tilts.TryGetValue(window, out var tilt))
        {
            tilt = new WindowTilt { Angle = AngleOf(window) };
            _tilts[window] = tilt;
        }

        if (_physicsBindings.TryGetValue(window, out var binding) && binding.Body.IsValid)
        {
            var body = binding.Body;
            body.Upright = false;
        }

        return tilt;
    }

    internal bool BeginTwist(IGrabTarget window)
    {
        if (window.EffectTree is not { IsDestroyed: false } || _twisting is not null)
        {
            return false;
        }

        StopSpin(window, report: false);
        var tilt = Hold(window);
        tilt.Moving = false;
        tilt.Angle = AngleOf(window);
        tilt.Target = tilt.Angle;
        _twisting = window;
        _report.Line($"TWIST {NameOf(window)} begin");
        return true;
    }

    internal void TwistBy(double radians)
    {
        if (_twisting is not { } window || !_tilts.TryGetValue(window, out var tilt))
        {
            return;
        }

        var angle = Math.IEEERemainder(AngleOf(window) + radians, Math.Tau);
        SetAngle(window, tilt, angle);
        tilt.Target = angle;
        ScheduleEffectRepaint();
    }

    private void DragToTurned(double x, double y, double angle)
    {
        if (_mode != DragMode.Move || _grabWindow is not { } window)
        {
            return;
        }

        var tilt = Hold(window);
        tilt.Moving = false;
        tilt.Spinning = false;
        tilt.Angle = angle;
        tilt.Target = angle;
        var (canvasX, canvasY) = ToCanvasPointAt(x, y, window);
        if (_physicsBindings.TryGetValue(window, out var binding) && binding.Body is { IsValid: true, IsDragging: true } body &&
            binding.World is { } world)
        {
            _throwTracker.Add(canvasX, canvasY, InputNanos());
            world.Drag(body, canvasX, canvasY, angle);
            if (WriteBack(window, binding, body.Pose))
            {
                _popupPlacer.PlaceAll();
            }

            ScheduleEffectRepaint();
            return;
        }

        var box = CanvasBoxOf(window);
        var centerX = box.X - window.X + (box.Width / 2.0);
        var centerY = box.Y - window.Y + (box.Height / 2.0);
        var turn = angle - _holdTurnBase;
        var cos = Math.Cos(turn);
        var sin = Math.Sin(turn);
        var armX = _grabX - centerX;
        var armY = _grabY - centerY;
        var beforeX = window.X;
        var beforeY = window.Y;
        window.MoveTo(
            (int)Math.Round(canvasX - ((cos * armX) - (sin * armY)) - centerX),
            (int)Math.Round(canvasY - ((sin * armX) + (cos * armY)) - centerY));
        ApplyTilt(window, angle);
        _effects.OnMoved(window.X - beforeX, window.Y - beforeY);
        ScheduleEffectRepaint();
    }

    private void SpinAfterTurn(IGrabTarget window, double radiansPerSecond)
    {
        if (!_config.RotateSpin || Math.Abs(radiansPerSecond) < SpinThreshold)
        {
            return;
        }

        if (_physicsBindings.TryGetValue(window, out var binding) && binding.Body is { IsValid: true } body &&
            body.Kind == PhysicsBodyKind.Dynamic && !body.AngularLocked)
        {
            body.AngularVelocity = radiansPerSecond;
            ScheduleEffectRepaint();
            return;
        }

        if (_tilts.TryGetValue(window, out var tilt))
        {
            tilt.Spin = new Fling(SpinFriction, SpinStop);
            tilt.Spin.Start(tilt.Angle, 0, radiansPerSecond, 0);
            tilt.Spinning = tilt.Spin.IsMoving;
            ScheduleEffectRepaint();
        }
    }

    internal void EndTwist(bool cancelled, double radiansPerSecond)
    {
        if (_twisting is not { } window)
        {
            return;
        }

        _twisting = null;
        if (!_tilts.TryGetValue(window, out var tilt))
        {
            return;
        }

        var spin = !cancelled && _config.RotateSpin && Math.Abs(radiansPerSecond) >= SpinThreshold ? radiansPerSecond : 0;
        _report.Line(string.Create(
            CultureInfo.InvariantCulture,
            $"TWIST {NameOf(window)} angle={tilt.Angle * 180 / Math.PI:F1} spin={spin * 180 / Math.PI:F0}"));
        if (spin == 0)
        {
            return;
        }

        if (_physicsBindings.TryGetValue(window, out var binding) && binding.Body is { IsValid: true } body &&
            body.Kind == PhysicsBodyKind.Dynamic && !body.AngularLocked)
        {
            body.AngularVelocity = spin;
            ScheduleEffectRepaint();
            return;
        }

        tilt.Spin = new Fling(SpinFriction, SpinStop);
        tilt.Spin.Start(tilt.Angle, 0, spin, 0);
        tilt.Spinning = tilt.Spin.IsMoving;
        ScheduleEffectRepaint();
    }

    internal bool StopSpin(IGrabTarget window, bool report = true)
    {
        var stopped = false;
        if (_tilts.TryGetValue(window, out var tilt) && tilt.Spinning)
        {
            tilt.Spinning = false;
            tilt.Target = tilt.Angle;
            stopped = true;
        }

        if (_physicsBindings.TryGetValue(window, out var binding) && binding.Body is { IsValid: true } body &&
            body.Kind != PhysicsBodyKind.Static && Math.Abs(body.AngularVelocity) > 0)
        {
            body.AngularVelocity = 0;
            stopped = true;
        }

        if (report)
        {
            _report.Line(string.Create(
                CultureInfo.InvariantCulture,
                $"STOPSPIN {NameOf(window)}{(stopped ? string.Empty : " idle")} angle={AngleOf(window) * 180 / Math.PI:F1}"));
        }

        return stopped;
    }

    private IGrabTarget? WindowAt(double x, double y)
    {
        if (_scene.NodeAt(x, y) is { Node: { } node } && FindFrame(node) is { } frame)
        {
            return frame.Owner;
        }

        if (_scene.SurfaceAt(x, y) is not { Surface: { } surface })
        {
            return null;
        }

        foreach (var window in _windows)
        {
            if (window.Owns(surface))
            {
                return window;
            }
        }

        foreach (var xwindow in _xwindows)
        {
            if (xwindow.Framable && xwindow.XWin.Surface == surface)
            {
                return xwindow;
            }
        }

        return null;
    }

    private bool OnFrameAt(double x, double y) =>
        _scene.NodeAt(x, y) is { Node: { } node } && FindFrame(node) is not null;

    private bool BeginPadTwist(uint time)
    {
        if (!IsAltDown() || _twisting is not null || WindowAt(_cursorX, _cursorY) is not { } window || !BeginTwist(window))
        {
            return false;
        }

        _padTwist.Reset();
        _padTwistTotal = 0;
        _padTwistStart = time;
        _padTwist.Add(0, 0, 0);
        return true;
    }

    private bool UpdatePadTwist(uint time, double degrees)
    {
        if (_twisting is null)
        {
            return false;
        }

        var radians = degrees * Math.PI / 180;
        _padTwistTotal += radians;
        _padTwist.Add(_padTwistTotal, 0, (long)(uint)(time - _padTwistStart) * 1_000_000);
        TwistBy(radians);
        return true;
    }

    private bool EndPadTwist(uint time, bool cancelled)
    {
        if (_twisting is null)
        {
            return false;
        }

        EndTwist(cancelled, _padTwist.Velocity((long)(uint)(time - _padTwistStart) * 1_000_000).X);
        return true;
    }

    private sealed class TwistHandler(TinyComp comp) : Basin.Seat.ITwistHandler
    {
        public bool Accepts(double x, double y) => comp._config.TitlebarTwist && comp.OnFrameAt(x, y);

        public bool Begin(double anchorX, double anchorY, uint timeMs) =>
            comp.WindowAt(anchorX, anchorY) is { } window && comp.BeginTwist(window);

        public void Update(double radians, uint timeMs) => comp.TwistBy(radians);

        public void End(bool cancelled, double radiansPerSecond, uint timeMs) => comp.EndTwist(cancelled, radiansPerSecond);
    }

    private sealed class HoldDragHandler(TinyComp comp) : Basin.Seat.IHoldDragHandler
    {
        public bool Accepts(double x, double y) => comp._config.HoldMove && comp.WindowAt(x, y) is not null;

        public bool Begin(double x, double y, uint timeMs)
        {
            if (comp._mode != DragMode.None || comp._twisting is not null || comp.WindowAt(x, y) is not { } window)
            {
                return false;
            }

            comp._lastMotionMillis = timeMs;
            comp.FocusFrameOwner(window);
            comp.StopSpin(window, report: false);
            comp._holdTurnBase = comp.AngleOf(window);
            comp._holdTurn = 0;
            comp._holdTurning = false;
            comp.BeginMoveAt(window, x, y);
            comp._report.Line($"HOLDMOVE {NameOf(window)} begin");
            return true;
        }

        public void Update(double x, double y, double radians, uint timeMs)
        {
            comp._lastMotionMillis = timeMs;
            if (radians != 0 && comp._config.HoldMoveTurn)
            {
                comp._holdTurn += radians;
                comp._holdTurning = true;
            }

            if (comp._holdTurning)
            {
                comp.DragToTurned(x, y, Math.IEEERemainder(comp._holdTurnBase + comp._holdTurn, Math.Tau));
            }
            else
            {
                _ = comp.DragTo(x, y);
            }
        }

        public void End(bool cancelled, double radiansPerSecond, uint timeMs)
        {
            if (!cancelled)
            {
                comp._lastMotionMillis = timeMs;
            }

            var window = comp._grabWindow;
            comp.EndTouchDrag();
            var angle = window is null ? 0 : comp.AngleOf(window);
            if (!cancelled && comp._holdTurning && window is not null)
            {
                comp.SpinAfterTurn(window, radiansPerSecond);
            }

            comp._holdTurning = false;
            comp._report.Line(string.Create(
                System.Globalization.CultureInfo.InvariantCulture, $"HOLDMOVE end angle={angle * 180 / Math.PI:F1}"));
        }
    }

    private sealed class WindowTwistHandler(TinyComp comp) : Basin.Seat.ITwistHandler
    {
        public bool Accepts(double x, double y) => comp._config.WindowTwistFingers > 0 && comp.WindowAt(x, y) is not null;

        public bool Begin(double anchorX, double anchorY, uint timeMs) =>
            comp.WindowAt(anchorX, anchorY) is { } window && comp.BeginTwist(window);

        public void Update(double radians, uint timeMs) => comp.TwistBy(radians);

        public void End(bool cancelled, double radiansPerSecond, uint timeMs) => comp.EndTwist(cancelled, radiansPerSecond);
    }

    private bool IsBound(IGrabTarget window) =>
        _physicsBindings.TryGetValue(window, out var binding) && binding.Body.IsValid;

    private void SetAngle(IGrabTarget window, WindowTilt tilt, double angle)
    {
        tilt.Angle = angle;
        if (_physicsBindings.TryGetValue(window, out var binding) && binding.Body.IsValid)
        {
            var body = binding.Body;
            var pose = body.Current;
            body.Teleport(pose.X, pose.Y, angle);
            if (WriteBack(window, binding, body.Pose))
            {
                _popupPlacer.PlaceAll();
            }

            return;
        }

        ApplyTilt(window, angle);
    }

    private void ApplyTilt(IGrabTarget window, double angle)
    {
        if (window.EffectTree is not { IsDestroyed: false } tree)
        {
            return;
        }

        var box = CanvasBoxOf(window);
        var pose = new PhysicsPose(
            0, 0, angle, box.X - window.X + (box.Width / 2.0), box.Y - window.Y + (box.Height / 2.0));
        if (PhysicsTransforms.Apply(_effects.StackFor(tree), pose, TiltName))
        {
            _popupPlacer.PlaceAll();
        }
    }

    private void ForgetTilt(IGrabTarget window)
    {
        if (ReferenceEquals(_twisting, window))
        {
            return;
        }

        if (!_tilts.Remove(window))
        {
            return;
        }

        if (window.EffectTree is { IsDestroyed: false } tree && _effects.StackFor(tree).Remove(TiltName))
        {
            _popupPlacer.PlaceAll();
        }

        if (_physicsBindings.TryGetValue(window, out var binding) && binding.Body.IsValid)
        {
            var body = binding.Body;
            body.Upright = _config.Physics.Upright;
        }
    }

    private void HandTiltToBody(IGrabTarget window, PhysicsBinding binding)
    {
        if (!_tilts.TryGetValue(window, out var tilt))
        {
            return;
        }

        if (window.EffectTree is { IsDestroyed: false } tree)
        {
            _ = _effects.StackFor(tree).Remove(TiltName);
        }

        var body = binding.Body;
        body.Upright = false;
        var pose = body.Current;
        body.Teleport(pose.X, pose.Y, tilt.Angle);
        _ = WriteBack(window, binding, body.Pose);
    }

    private void TakeTiltFromBody(IGrabTarget window, double angle)
    {
        if (!_tilts.TryGetValue(window, out var tilt))
        {
            return;
        }

        tilt.Angle = angle;
        ApplyTilt(window, angle);
    }
}
