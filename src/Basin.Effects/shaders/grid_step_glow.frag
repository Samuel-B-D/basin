#version 450
layout(std140, set = 0, binding = 0) uniform Basin {
    vec2 u_size;
    float u_alpha;
    vec4 m0;
    vec4 m1;
    vec4 m2;
    vec4 m3;
    vec4 m4;
    vec4 b0;
    vec4 b1;
    vec4 l0;
    vec4 l1;
    vec4 l2;
    vec4 l3;
    vec4 l4;
    vec4 l5;
    vec4 l6;
    vec4 l7;
    vec4 l8;
    vec4 l9;
    vec4 s0;
    vec4 s1;
    vec4 s2;
    vec4 s3;
    vec4 s4;
    vec4 s5;
    vec4 s6;
    vec4 s7;
    vec4 s8;
    vec4 s9;
    vec4 s10;
    vec4 s11;
};
layout(location = 0) in vec2 v_uv;
layout(location = 0) out vec4 color;

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

bool st_has(float bits, float bit) {
    return mod(floor(bits / bit), 2.0) >= 1.0;
}

float st_wall_width(int k) {
    float bit = k == 1 ? 1.0 : k == 2 ? 2.0 : k == 3 ? 4.0 : 8.0;
    if (!st_has(m4.x, bit)) {
        return 0.0;
    }

    return k == 1 ? m1.x - m2.x
        : k == 2 ? (m2.x + m2.z) - (m1.x + m1.z)
        : k == 3 ? m1.y - m2.y
        : (m2.y + m2.w) - (m1.y + m1.w);
}

vec4 st_bulge(int k) {
    return k == 1 ? vec4(b0.xy, 0.0, 0.0) : k == 2 ? vec4(b0.zw, 0.0, 0.0) : k == 3 ? vec4(b1.xy, 0.0, 0.0) : vec4(b1.zw, 0.0, 0.0);
}

vec4 grid_step_glow(vec2 coord, vec2 size) {
    vec2 p = s8.yz + coord * vec2(s8.w, s9.x) / size;
    bool lines = st_has(s6.w, 1.0);
    bool shelfLines = st_has(s6.w, 2.0);
    bool desktopLines = st_has(s6.w, 4.0);
    bool density = st_has(s6.w, 8.0);
    bool wallLines = st_has(s6.w, 16.0);
    if (!lines || p.x < m3.x || p.x >= m3.x + m3.z || p.y < m3.y || p.y >= m3.y + m3.w) {
        return vec4(0.0);
    }

    float across = -1000000.0;
    float along = -1000000.0;
    int horizontal = 0;
    int vertical = 0;
    float width = st_wall_width(1);
    if (width > 0.0 && -(p.x - m1.x) / width > across) {
        across = -(p.x - m1.x) / width;
        horizontal = 1;
    }

    width = st_wall_width(2);
    if (width > 0.0 && (p.x - (m1.x + m1.z)) / width > across) {
        across = (p.x - (m1.x + m1.z)) / width;
        horizontal = 2;
    }

    width = st_wall_width(3);
    if (width > 0.0 && -(p.y - m1.y) / width > along) {
        along = -(p.y - m1.y) / width;
        vertical = 3;
    }

    width = st_wall_width(4);
    if (width > 0.0 && (p.y - (m1.y + m1.w)) / width > along) {
        along = (p.y - (m1.y + m1.w)) / width;
        vertical = 4;
    }

    float depth = max(across, along);
    int wall = across >= along ? horizontal : vertical;
    bool plane = horizontal == 0 && vertical == 0 || depth <= 0.0;
    float cell = s0.w;
    if (plane || depth >= 1.0) {
        float scale = m0.z;
        if (!plane) {
            if (across >= 1.0 && along >= 1.0) {
                bool h = horizontal == 1 ? st_has(m4.y, 1.0) : st_has(m4.y, 2.0);
                bool v = vertical == 3 ? st_has(m4.y, 4.0) : st_has(m4.y, 8.0);
                if (!(h && v)) {
                    return vec4(0.0);
                }
            } else {
                float bit = wall == 1 ? 1.0 : wall == 2 ? 2.0 : wall == 3 ? 4.0 : 8.0;
                if (!st_has(m4.y, bit)) {
                    return vec4(0.0);
                }
            }

            if (!shelfLines) {
                return vec4(0.0);
            }

            scale = m0.w;
        } else if (!desktopLines) {
            return vec4(0.0);
        }

        float step = cell * g_stride(scale);
        vec2 c = m0.xy + (p - m0.xy) / scale;
        vec2 nearest = floor(c / step + 0.5) * step;
        float dColumn = max(0.0, abs((c.x - nearest.x) * scale - 0.5) - 0.5);
        float dRow = max(0.0, abs((c.y - nearest.y) * scale - 0.5) - 0.5);
        return g_glow(p, min(dColumn, dRow), step * scale, dColumn <= dRow, density);
    }

    if (!wallLines || wall == 0) {
        return vec4(0.0);
    }

    width = st_wall_width(wall);
    float s = depth;
    float q = clamp(floor(s * 4.0 + 0.5), 1.0, 3.0) / 4.0;
    float dQuarter = max(0.0, abs(s - q) * width - 0.5);
    bool sideWall = wall == 1 || wall == 2;
    float a = sideWall ? p.y : p.x;
    float innerStart = sideWall ? m1.y : m1.x;
    float innerLength = sideWall ? m1.w : m1.z;
    float outerStart = sideWall ? m2.y : m2.x;
    float outerLength = sideWall ? m2.w : m2.z;
    vec4 bulge = st_bulge(wall);
    float k = clamp(bulge.y, 0.0, 0.7) * 4.0 * s * (1.0 - s);
    float span = (1.0 - s) * innerLength + s * outerLength;
    float spanStart = (1.0 - s) * innerStart + s * outerStart;
    float stretch = 1.0;
    if (k > 0.0 && span > 0.0) {
        float bent = a;
        for (int i = 0; i < 8; i++) {
            float fraction = clamp((a - spanStart) / span, 0.0, 1.0);
            float envelope = 4.0 * fraction * (1.0 - fraction);
            float slope = 4.0 * (1.0 - 2.0 * fraction) / span;
            a = a - (a + k * envelope * (bulge.x - a) - bent) / (1.0 + k * (slope * (bulge.x - a) - envelope));
        }

        float at = clamp((a - spanStart) / span, 0.0, 1.0);
        stretch = 1.0 + k * (4.0 * (1.0 - 2.0 * at) / span * (bulge.x - a) - 4.0 * at * (1.0 - at));
    }

    float t = (a - spanStart) / span;
    float center = sideWall ? m0.y : m0.x;
    float canvas = center + (innerStart + t * innerLength - center) / m0.z;
    float step = cell * g_stride(m0.z);
    float nearest = floor(canvas / step + 0.5) * step;
    float fan = innerLength > 0.0 ? span / innerLength : 1.0;
    float unit = m0.z * fan * stretch;
    float dCross = max(0.0, abs(canvas - nearest) * unit - 0.5);
    if (dQuarter <= dCross) {
        return g_glow(p, dQuarter, width / 4.0, sideWall, density);
    }

    return g_glow(p, dCross, step * unit, !sideWall, density);
}

vec3 srgb_to_linear(vec3 c) {
    return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void main() {
    vec4 glow = grid_step_glow(v_uv * u_size, u_size);
    color = vec4(srgb_to_linear(clamp(glow.rgb, 0.0, 1.0)), 0.0) * u_alpha;
}
