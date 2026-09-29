namespace Basin.Effects;

internal static class GridGlowCommon
{
    public const int Lights = GridLights.Capacity;

    public const int Scalars = 12;

    public const float Far = 1e30f;

    public const string Chunk = """
        float g_stride(float scale) {
            float stride = 1.0;
            if (s5.w <= 0.0) {
                return 1.0;
            }

            for (int i = 0; i < 20; i++) {
                if (scale * s0.x * s0.w * stride < s5.w) {
                    stride = stride * 2.0;
                }
            }

            return stride;
        }

        float g_falloff(float t) {
            if (t >= 1.0) {
                return 0.0;
            }

            t = max(t, 0.0);
            return (1.0 - t) * (1.0 - t) * (1.0 + 2.0 * t);
        }

        float g_box_light(vec4 box, float strength, vec2 p) {
            if (strength <= 0.0) {
                return 1.0;
            }

            float dx = max(max(box.x - p.x, p.x - (box.x + box.z)), 0.0);
            float dy = max(max(box.y - p.y, p.y - (box.y + box.w)), 0.0);
            return 1.0 - strength * g_falloff(sqrt(dx * dx + dy * dy) / s5.x);
        }

        float g_weight(vec2 p) {
            if (s5.x <= 0.0) {
                return 0.0;
            }

            float dark = g_box_light(l0, s9.z, p) * g_box_light(l1, s9.w, p);
            dark = dark * g_box_light(l2, s10.x, p) * g_box_light(l3, s10.y, p);
            dark = dark * g_box_light(l4, s10.z, p) * g_box_light(l5, s10.w, p);
            dark = dark * g_box_light(l6, s11.x, p) * g_box_light(l7, s11.y, p);
            dark = dark * g_box_light(l8, s11.z, p) * g_box_light(l9, s11.w, p);
            if (s6.z > 0.0) {
                dark = dark * (1.0 - s6.z * g_falloff(length(p - s6.xy) / s5.x));
            }

            return clamp(1.0 - dark, 0.0, 1.0);
        }

        float g_smooth(float v) {
            if (v <= 0.0001) {
                return 0.0;
            }

            float t = min(v, 1.0);
            return t * t * (3.0 - 2.0 * t);
        }

        float g_reveal(vec2 p, bool vertical) {
            float style = s7.x;
            if (style < 0.5) {
                return 1.0;
            }

            float progress = s7.y;
            if (progress <= 0.0) {
                return 0.0;
            }

            vec2 origin = s7.zw;
            float feather = s8.x;
            float left = origin.x - s8.y;
            float right = s8.y + s8.w - origin.x;
            float top = origin.y - s8.z;
            float bottom = s8.z + s9.x - origin.y;
            if (style < 1.5) {
                float fx = max(left, right);
                float fy = max(top, bottom);
                float radius = progress * (sqrt(fx * fx + fy * fy) + feather);
                return g_smooth((radius - length(p - origin)) / feather);
            }

            if (style < 2.5) {
                float across = vertical ? abs(p.x - origin.x) : abs(p.y - origin.y);
                float spread = max(1.0, vertical ? max(left, right) : max(top, bottom));
                float along = max(1.0, vertical ? max(top, bottom) : max(left, right));
                float start = 0.4 * min(1.0, across / spread);
                float local = clamp((progress - start) / (1.0 - start), 0.0, 1.0);
                float reach = local * (along + feather);
                return g_smooth((reach - (vertical ? abs(p.y - origin.y) : abs(p.x - origin.x))) / feather);
            }

            float nearest = min(min(left, right), min(top, bottom));
            float extent = nearest == left || nearest == right ? s8.w : s9.x;
            float front = progress * (extent + feather);
            float depth = nearest == left ? p.x - s8.y
                : nearest == right ? s8.y + s8.w - p.x
                : nearest == top ? p.y - s8.z
                : s8.z + s9.x - p.y;
            return g_smooth((front - depth) / feather);
        }

        vec4 g_glow(vec2 p, float d, float spacing, bool vertical, bool density) {
            float fade = density ? clamp(spacing / s4.z, s4.w, 1.0) : 1.0;
            float weight = g_weight(p);
            float lineAlpha = (s5.z + (1.0 - s5.z) * weight) * fade;
            float g = exp(-(d / s4.y) * (d / s4.y));
            float strength = s4.x * (1.0 + weight * s5.y) * lineAlpha * g_reveal(p, vertical) * g;
            vec4 tone = mix(s2, s3, weight);
            return vec4(tone.rgb * strength, 0.0);
        }
        """;

    public static void Pack(
        in GridLights lights, in GridEffects effects, in GridReveal reveal, in RenderColor color, float alpha, in Box bounds,
        double minSpacing, int flags, in PixelShaderUniformValue s0, in PixelShaderUniformValue s1,
        Span<PixelShaderUniformValue> lightsInto, Span<PixelShaderUniformValue> into)
    {
        for (var i = 0; i < Lights; i++)
        {
            var light = lights[i];
            lightsInto[i] = new PixelShaderUniformValue(light.X, light.Y, light.Width, light.Height);
        }

        var mask = GridRevealMask.Of(reveal, bounds);
        var style = !GridRevealMask.ClipsFor(reveal) ? 0f : reveal.Style switch
        {
            GridRevealStyle.Ripple => 1f,
            GridRevealStyle.Draw => 2f,
            _ => 3f,
        };
        var originX = double.IsFinite(reveal.OriginX) ? reveal.OriginX : bounds.X + (bounds.Width / 2.0);
        var originY = double.IsFinite(reveal.OriginY) ? reveal.OriginY : bounds.Y + (bounds.Height / 2.0);
        var feather = double.IsFinite(reveal.Feather) ? Math.Max(1e-3, reveal.Feather) : 1e-3;
        var progress = float.IsNaN(reveal.Progress) ? 0f : Math.Max(0f, reveal.Progress);
        var highlight = lights.Color;
        into[0] = s0;
        into[1] = s1;
        into[2] = new(color.R, color.G, color.B, color.A);
        into[3] = new(highlight.R, highlight.G, highlight.B, highlight.A);
        into[4] = new(effects.Glow, Math.Max(1e-3f, effects.GlowRadius), Math.Max(1e-3f, effects.DensitySpacing), Math.Clamp(effects.DensityFloor, 0f, 1f));
        into[5] = new(lights.IsLit ? lights.Radius : 0f, lights.GlowBoost, alpha * mask.Scale, (float)minSpacing);
        into[6] = new(lights.PointerX, lights.PointerY, lights.PointerStrength, flags);
        into[7] = new(style, progress, (float)originX, (float)originY);
        into[8] = new((float)feather, bounds.X, bounds.Y, bounds.Width);
        into[9] = new(bounds.Height, 0f, lights[0].Strength, lights[1].Strength);
        into[10] = new(lights[2].Strength, lights[3].Strength, lights[4].Strength, lights[5].Strength);
        into[11] = new(lights[6].Strength, lights[7].Strength, lights[8].Strength, lights[9].Strength);
    }
}
