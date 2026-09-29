namespace Basin.Effects;

public abstract class GridGlowTwin
{
    private readonly V[] _u;
    private readonly int _lights;
    private readonly int _scalars;

    protected GridGlowTwin(ReadOnlySpan<PixelShaderUniformValue> uniforms, int count, int lights, int scalars)
    {
        _u = new V[count];
        _lights = lights;
        _scalars = scalars;
        for (var i = 0; i < count; i++)
        {
            var value = uniforms[i];
            _u[i] = new V(value.X, value.Y, value.Z, value.W);
        }
    }

    protected readonly record struct V(double X, double Y, double Z, double W);

    protected V U(int i) => _u[i];

    protected V S(int i) => _u[_scalars + i];

    protected V L(int i) => _u[_lights + i];

    public abstract (double R, double G, double B) At(double px, double py);

    protected double Stride(double scale)
    {
        var stride = 1.0;
        if (S(5).W <= 0)
        {
            return 1.0;
        }

        for (var i = 0; i < 20; i++)
        {
            if (scale * S(0).X * S(0).W * stride < S(5).W)
            {
                stride *= 2.0;
            }
        }

        return stride;
    }

    private static double Falloff(double t)
    {
        if (t >= 1.0)
        {
            return 0.0;
        }

        t = Math.Max(t, 0.0);
        return (1.0 - t) * (1.0 - t) * (1.0 + (2.0 * t));
    }

    private double BoxLight(V box, double strength, double px, double py)
    {
        if (strength <= 0)
        {
            return 1.0;
        }

        var dx = Math.Max(Math.Max(box.X - px, px - (box.X + box.Z)), 0.0);
        var dy = Math.Max(Math.Max(box.Y - py, py - (box.Y + box.W)), 0.0);
        return 1.0 - (strength * Falloff(Math.Sqrt((dx * dx) + (dy * dy)) / S(5).X));
    }

    public double Weight(double px, double py)
    {
        if (S(5).X <= 0)
        {
            return 0.0;
        }

        double[] strengths = [S(9).Z, S(9).W, S(10).X, S(10).Y, S(10).Z, S(10).W, S(11).X, S(11).Y, S(11).Z, S(11).W];
        var dark = 1.0;
        for (var i = 0; i < 10; i++)
        {
            dark *= BoxLight(L(i), strengths[i], px, py);
        }

        if (S(6).Z > 0)
        {
            var dx = px - S(6).X;
            var dy = py - S(6).Y;
            dark *= 1.0 - (S(6).Z * Falloff(Math.Sqrt((dx * dx) + (dy * dy)) / S(5).X));
        }

        return Math.Clamp(1.0 - dark, 0.0, 1.0);
    }

    private static double Smooth(double v)
    {
        if (v <= 0.0001)
        {
            return 0.0;
        }

        var t = Math.Min(v, 1.0);
        return t * t * (3.0 - (2.0 * t));
    }

    protected double Reveal(double px, double py, bool vertical)
    {
        var style = S(7).X;
        if (style < 0.5)
        {
            return 1.0;
        }

        var progress = S(7).Y;
        if (progress <= 0)
        {
            return 0.0;
        }

        var ox = S(7).Z;
        var oy = S(7).W;
        var feather = S(8).X;
        var left = ox - S(8).Y;
        var right = S(8).Y + S(8).W - ox;
        var top = oy - S(8).Z;
        var bottom = S(8).Z + S(9).X - oy;
        if (style < 1.5)
        {
            var fx = Math.Max(left, right);
            var fy = Math.Max(top, bottom);
            var radius = progress * (Math.Sqrt((fx * fx) + (fy * fy)) + feather);
            var dx = px - ox;
            var dy = py - oy;
            return Smooth((radius - Math.Sqrt((dx * dx) + (dy * dy))) / feather);
        }

        if (style < 2.5)
        {
            var across = vertical ? Math.Abs(px - ox) : Math.Abs(py - oy);
            var spread = Math.Max(1.0, vertical ? Math.Max(left, right) : Math.Max(top, bottom));
            var along = Math.Max(1.0, vertical ? Math.Max(top, bottom) : Math.Max(left, right));
            var start = 0.4 * Math.Min(1.0, across / spread);
            var local = Math.Clamp((progress - start) / (1.0 - start), 0.0, 1.0);
            var reach = local * (along + feather);
            return Smooth((reach - (vertical ? Math.Abs(py - oy) : Math.Abs(px - ox))) / feather);
        }

        var nearest = Math.Min(Math.Min(left, right), Math.Min(top, bottom));
        var extent = nearest == left || nearest == right ? S(8).W : S(9).X;
        var front = progress * (extent + feather);
        var depth = nearest == left ? px - S(8).Y
            : nearest == right ? S(8).Y + S(8).W - px
            : nearest == top ? py - S(8).Z
            : S(8).Z + S(9).X - py;
        return Smooth((front - depth) / feather);
    }

    protected (double R, double G, double B) Glow(double px, double py, double d, double spacing, bool vertical, bool density)
    {
        var fade = density ? Math.Clamp(spacing / S(4).Z, S(4).W, 1.0) : 1.0;
        var weight = Weight(px, py);
        var lineAlpha = (S(5).Z + ((1.0 - S(5).Z) * weight)) * fade;
        var g = Math.Exp(-(d / S(4).Y) * (d / S(4).Y));
        var strength = S(4).X * (1.0 + (weight * S(5).Y)) * lineAlpha * Reveal(px, py, vertical) * g;
        var low = S(2);
        var high = S(3);
        return (
            (low.X + ((high.X - low.X) * weight)) * strength,
            (low.Y + ((high.Y - low.Y) * weight)) * strength,
            (low.Z + ((high.Z - low.Z) * weight)) * strength);
    }
}
