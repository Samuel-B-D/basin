using System.Runtime.CompilerServices;

namespace Basin.Effects;

public struct GridLights
{
    public const int Capacity = 10;

    public const float DefaultGlowBoost = 2f;

    [InlineArray(Capacity)]
    private struct Slots
    {
        private GridLight _element0;
    }

    private Slots _slots;
    private int _count;

    public GridLights()
    {
        Radius = 160f;
        Color = new RenderColor(0xe8 / 255f, 0xec / 255f, 1f, 1f);
        GlowBoost = DefaultGlowBoost;
    }

    public readonly int Count => _count;

    public float Radius { get; set; }

    public RenderColor Color { get; set; }

    public float GlowBoost { get; set; }

    public float PointerX { get; private set; }

    public float PointerY { get; private set; }

    public float PointerStrength { get; private set; }

    public readonly GridLight this[int index] => index >= 0 && index < _count ? _slots[index] : default;

    public readonly bool IsLit
    {
        get
        {
            if (Radius <= 0f)
            {
                return false;
            }

            if (PointerStrength > 0f)
            {
                return true;
            }

            for (var i = 0; i < _count; i++)
            {
                if (_slots[i].Strength > 0f)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public void Set(int index, in Box box, float strength) =>
        Set(index, box.X, box.Y, box.Width, box.Height, strength);

    public void Set(int index, float x, float y, float width, float height, float strength)
    {
        if (index < 0 || index >= Capacity)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        _slots[index] = new GridLight(x, y, Math.Max(0f, width), Math.Max(0f, height), Math.Clamp(strength, 0f, 1f));
        _count = Math.Max(_count, index + 1);
    }

    public void SetPointer(double x, double y, float strength)
    {
        PointerX = (float)x;
        PointerY = (float)y;
        PointerStrength = Math.Clamp(strength, 0f, 1f);
    }

    public void Clear()
    {
        _slots = default;
        _count = 0;
        PointerX = 0f;
        PointerY = 0f;
        PointerStrength = 0f;
    }

    public readonly float At(double x, double y)
    {
        if (Radius <= 0f)
        {
            return 0f;
        }

        var dark = 1.0;
        for (var i = 0; i < _count; i++)
        {
            var light = _slots[i];
            if (light.Strength <= 0f)
            {
                continue;
            }

            var dx = Math.Max(Math.Max(light.X - x, x - light.Right), 0.0);
            var dy = Math.Max(Math.Max(light.Y - y, y - light.Bottom), 0.0);
            dark *= 1.0 - (light.Strength * Falloff(Math.Sqrt((dx * dx) + (dy * dy)) / Radius));
        }

        if (PointerStrength > 0f)
        {
            var dx = x - PointerX;
            var dy = y - PointerY;
            dark *= 1.0 - (PointerStrength * Falloff(Math.Sqrt((dx * dx) + (dy * dy)) / Radius));
        }

        return (float)Math.Clamp(1.0 - dark, 0.0, 1.0);
    }

    public static double Falloff(double t)
    {
        if (t >= 1.0)
        {
            return 0.0;
        }

        t = Math.Max(0.0, t);
        return (1.0 - t) * (1.0 - t) * (1.0 + (2.0 * t));
    }
}
