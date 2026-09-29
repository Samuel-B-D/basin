using Basin;
using Basin.Effects;
using Basin.Kinematics;

namespace TinyComp;

internal sealed class GridLightFeed
{
    public const int WindowSlots = 8;

    public const int FadeSlots = 2;

    private const int Slots = WindowSlots + FadeSlots;

    private const int PointerSpring = Slots;

    private const double Rest = 0.002;

    private readonly object?[] _keys = new object?[Slots];
    private readonly Box[] _boxes = new Box[Slots];
    private readonly SpringMotion[] _springs = new SpringMotion[Slots + 1];
    private readonly object?[] _wanted = new object?[WindowSlots];
    private readonly Box[] _wantedBoxes = new Box[WindowSlots];
    private readonly float[] _wantedStrengths = new float[WindowSlots];
    private int _wantedCount;
    private double _pointerX;
    private double _pointerY;
    private float _pointerTarget;
    private TickDelta _delta;
    private GridLights _lights = new();
    private bool _empty = true;

    public GridLights Lights => _lights;

    public bool Moving { get; private set; }

    public int Lit
    {
        get
        {
            var lit = 0;
            for (var i = 0; i < WindowSlots; i++)
            {
                lit += _keys[i] is not null && _springs[i].Position > Rest ? 1 : 0;
            }

            return lit;
        }
    }

    public int Fading
    {
        get
        {
            var fading = 0;
            for (var i = WindowSlots; i < Slots; i++)
            {
                fading += _keys[i] is not null ? 1 : 0;
            }

            return fading;
        }
    }

    public float PointerStrength => (float)Math.Clamp(_springs[PointerSpring].Position, 0.0, 1.0);

    public object? KeyAt(int slot) => slot >= 0 && slot < Slots ? _keys[slot] : null;

    public void Begin() => _wantedCount = 0;

    public bool Want(object window, in Box box, float strength)
    {
        if (_wantedCount >= WindowSlots)
        {
            return false;
        }

        if (strength <= 0f || IndexOfWanted(window) >= 0)
        {
            return true;
        }

        _wanted[_wantedCount] = window;
        _wantedBoxes[_wantedCount] = box;
        _wantedStrengths[_wantedCount] = strength;
        _wantedCount++;
        return _wantedCount < WindowSlots;
    }

    public void Pointer(double x, double y, float strength)
    {
        _pointerX = x;
        _pointerY = y;
        _pointerTarget = strength;
    }

    public bool Clear()
    {
        _delta.Reset();
        Moving = false;
        if (_empty)
        {
            return false;
        }

        Array.Clear(_keys);
        Array.Clear(_springs);
        Array.Clear(_wanted);
        _wantedCount = 0;
        _pointerTarget = 0f;
        var radius = _lights.Radius;
        var color = _lights.Color;
        var boost = _lights.GlowBoost;
        _lights.Clear();
        _lights.Radius = radius;
        _lights.Color = color;
        _lights.GlowBoost = boost;
        _empty = true;
        return true;
    }

    public bool Step(in FrameTick tick, double spring, double damping, float radius, in RenderColor color, float boost)
    {
        var millis = _delta.Advance(tick) / 1_000_000.0;
        var changed = _lights.Radius != radius || _lights.Color != color || _lights.GlowBoost != boost;
        for (var slot = 0; slot < WindowSlots; slot++)
        {
            if (_keys[slot] is { } key && IndexOfWanted(key) < 0)
            {
                ToFade(slot);
                changed = true;
            }
        }

        for (var i = 0; i < _wantedCount; i++)
        {
            var key = _wanted[i]!;
            var slot = SlotOf(key);
            if (slot < 0 || slot >= WindowSlots)
            {
                var free = FreeWindowSlot();
                if (slot >= WindowSlots)
                {
                    _springs[free] = _springs[slot];
                    _keys[slot] = null;
                }
                else
                {
                    _springs[free] = default;
                }

                _keys[free] = key;
                slot = free;
                changed = true;
            }

            changed |= _boxes[slot] != _wantedBoxes[i];
            _boxes[slot] = _wantedBoxes[i];
            _springs[slot].Anchor = _wantedStrengths[i];
        }

        for (var slot = WindowSlots; slot < Slots; slot++)
        {
            _springs[slot].Anchor = 0;
        }

        _springs[PointerSpring].Anchor = _pointerTarget;
        changed |= PointerStrength > 0f && ((float)_pointerX != _lights.PointerX || (float)_pointerY != _lights.PointerY);
        var moving = false;
        for (var i = 0; i <= Slots; i++)
        {
            ref var motion = ref _springs[i];
            if (i < Slots && _keys[i] is null)
            {
                continue;
            }

            motion.SpringConstant = spring;
            motion.DampingRatio = damping;
            motion.Epsilon = Rest;
            if (!motion.IsMoving)
            {
                continue;
            }

            motion.Advance(millis);
            changed = true;
            if (motion.IsMoving)
            {
                moving = true;
            }
            else
            {
                motion.SetPosition(motion.Anchor);
            }
        }

        for (var slot = WindowSlots; slot < Slots; slot++)
        {
            if (_keys[slot] is not null && !_springs[slot].IsMoving && _springs[slot].Position <= Rest)
            {
                _keys[slot] = null;
                changed = true;
            }
        }

        Moving = moving;
        if (!changed)
        {
            return false;
        }

        _lights.Radius = radius;
        _lights.Color = color;
        _lights.GlowBoost = boost;
        var any = false;
        for (var slot = 0; slot < Slots; slot++)
        {
            var strength = _keys[slot] is null ? 0f : (float)Math.Clamp(_springs[slot].Position, 0.0, 1.0);
            _lights.Set(slot, _boxes[slot], strength);
            any |= strength > 0f;
        }

        _lights.SetPointer(_pointerX, _pointerY, PointerStrength);
        _empty = !any && PointerStrength <= 0f;
        return true;
    }

    private void ToFade(int slot)
    {
        var target = WindowSlots;
        var weakest = double.PositiveInfinity;
        for (var fade = WindowSlots; fade < Slots; fade++)
        {
            if (_keys[fade] is null)
            {
                target = fade;
                break;
            }

            if (_springs[fade].Position < weakest)
            {
                weakest = _springs[fade].Position;
                target = fade;
            }
        }

        _keys[target] = _keys[slot];
        _boxes[target] = _boxes[slot];
        _springs[target] = _springs[slot];
        _springs[target].Anchor = 0;
        _keys[slot] = null;
        _springs[slot] = default;
    }

    private int FreeWindowSlot()
    {
        for (var slot = 0; slot < WindowSlots; slot++)
        {
            if (_keys[slot] is null)
            {
                return slot;
            }
        }

        return WindowSlots - 1;
    }

    private int SlotOf(object key)
    {
        for (var slot = 0; slot < Slots; slot++)
        {
            if (ReferenceEquals(_keys[slot], key))
            {
                return slot;
            }
        }

        return -1;
    }

    private int IndexOfWanted(object key)
    {
        for (var i = 0; i < _wantedCount; i++)
        {
            if (ReferenceEquals(_wanted[i], key))
            {
                return i;
            }
        }

        return -1;
    }
}
