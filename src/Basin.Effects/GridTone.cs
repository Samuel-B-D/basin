using Basin.Scene;

namespace Basin.Effects;

internal struct GridTone
{
    private RenderColor _color;
    private float _alpha;
    private float _scale;
    private GridLights _lights;
    private GridEffects _effects;
    private bool _lit;

    public static GridTone Of(in RenderColor color, float alpha, float scale, in GridLights lights, in GridEffects effects)
    {
        var tone = new GridTone
        {
            _color = color,
            _alpha = alpha,
            _scale = scale,
            _lights = lights,
            _effects = effects,
            _lit = lights.IsLit,
        };
        return tone;
    }

    public readonly bool Segments => _lit || _effects.Density;

    public readonly bool Halo => _effects.DrawsHalo;

    public readonly bool Shades => Segments || Halo;

    public bool HaloPart { get; set; }

    public readonly bool Density => _effects.Density;

    public readonly float HaloWidth => _effects.GlowRadius * MathF.Sqrt(MathF.PI);

    public readonly GridEffects Effects => _effects;

    public readonly float DensityOf(double spacing) => _effects.DensityOf(spacing);

    public readonly void Tones(float x, float y, float density, out RenderColor line, out RenderColor glow)
    {
        var weight = _lit ? _lights.At(x, y) : 0f;
        var lit = weight > 0f ? Mix(_color, _lights.Color, weight) : _color;
        var alpha = (_alpha + ((1f - _alpha) * weight)) * _scale * density;
        line = new RenderColor(lit.R * alpha, lit.G * alpha, lit.B * alpha, lit.A * alpha);
        if (!Halo)
        {
            glow = RenderColor.Transparent;
            return;
        }

        var strength = alpha * _effects.Glow * (1f + (weight * _lights.GlowBoost));
        var r = Math.Min(1f, lit.R * strength);
        var g = Math.Min(1f, lit.G * strength);
        var b = Math.Min(1f, lit.B * strength);
        glow = new RenderColor(r, g, b, Math.Max(r, Math.Max(g, b)));
    }

    public static RenderColor Mix(in RenderColor from, in RenderColor to, float t) => new(
        from.R + ((to.R - from.R) * t),
        from.G + ((to.G - from.G) * t),
        from.B + ((to.B - from.B) * t),
        from.A + ((to.A - from.A) * t));

    public static int Pieces(double length, bool segments) =>
        segments ? Math.Max(1, (int)Math.Ceiling(Math.Abs(length) / GridQuad.SegmentWidth)) : 1;
}
