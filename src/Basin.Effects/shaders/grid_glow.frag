#version 450
layout(std140, set = 0, binding = 0) uniform Basin {
    vec2 u_size;
    float u_alpha;
    vec4 w0a;
    vec4 w0b;
    vec4 w1a;
    vec4 w1b;
    vec4 w2a;
    vec4 w2b;
    vec4 w3a;
    vec4 w3b;
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

bool gw_on(vec4 a) {
    return a.y > 0.0 && a.z > 0.0;
}

bool gw_has(vec4 a, float dir, float x) {
    return gw_on(a) && dir * (x - a.x) > 0.0;
}

bool gw_inside(vec4 a, float dir, float x) {
    return !gw_on(a) || dir * (x - a.x) < 0.0;
}

float gw_dist(vec4 a, vec4 b, float u) {
    float p1 = b.x + 1.0;
    return a.z * (a.w * u + (1.0 - a.w) * (1.0 - pow(max(1.0 - u, 0.0), p1)) / p1);
}

float gw_scale_u(vec4 a, vec4 b, float u) {
    if (u <= 0.0) {
        return 1.0;
    }

    if (u >= 1.0) {
        return a.w;
    }

    return a.w + (1.0 - a.w) * pow(1.0 - u, b.x);
}

float gw_scale(vec4 a, vec4 b, float dir, float x) {
    return gw_on(a) ? gw_scale_u(a, b, dir * (x - a.x) / a.z) : 1.0;
}

float gw_to_screen(vec4 a, vec4 b, float dir, float x) {
    if (!gw_on(a)) {
        return x;
    }

    float u = dir * (x - a.x) / a.z;
    if (u <= 0.0) {
        return x;
    }

    if (u >= 1.0) {
        return a.x + dir * (a.y + a.w * (u - 1.0) * a.z);
    }

    return a.x + dir * gw_dist(a, b, u);
}

float gw_to_canvas(vec4 a, vec4 b, float dir, float x) {
    if (!gw_on(a)) {
        return x;
    }

    float d = dir * (x - a.x);
    if (d <= 0.0) {
        return x;
    }

    if (d >= a.y) {
        return a.x + dir * a.z + dir * (d - a.y) / a.w;
    }

    float u = clamp(d / a.z, 0.0, 1.0);
    for (int i = 0; i < 12; i++) {
        u = clamp(u - (gw_dist(a, b, u) - d) / (a.z * gw_scale_u(a, b, u)), 0.0, 1.0);
    }

    return a.x + dir * u * a.z;
}

float gw_fan_t(vec4 a, vec4 b, float t) {
    if (b.w > 0.5) {
        if (t <= 0.0) {
            return 1.0;
        }

        if (t >= 1.0) {
            return a.w;
        }

        float bump = 16.0 * t * t * (1.0 - t) * (1.0 - t);
        return 1.0 + (a.w - 1.0) * smoothstep(0.0, 1.0, t) + b.y * bump;
    }

    return 1.0 + b.y * smoothstep(0.0, 1.0, t);
}

float gw_fan(vec4 a, vec4 b, float dir, float x) {
    if (!gw_on(a) || (b.y == 0.0 && b.w < 0.5)) {
        return 1.0;
    }

    float u = dir * (x - a.x) / a.z;
    if (u <= 0.0) {
        return 1.0;
    }

    if (u >= 1.0) {
        return b.w > 0.5 ? a.w : 1.0 + b.y;
    }

    return gw_fan_t(a, b, gw_dist(a, b, u) / a.y);
}

float gw_fan_screen(vec4 a, vec4 b, float dir, float x) {
    if (!gw_on(a) || (b.y == 0.0 && b.w < 0.5)) {
        return 1.0;
    }

    float d = dir * (x - a.x);
    if (d <= 0.0) {
        return 1.0;
    }

    if (d >= a.y) {
        return b.w > 0.5 ? a.w : 1.0 + b.y;
    }

    return gw_fan_t(a, b, d / a.y);
}

float gw_depth(vec4 a, vec4 b, float dir, float x) {
    if (!gw_on(a)) {
        return 0.0;
    }

    float u = dir * (x - a.x) / a.z;
    if (u <= 0.0) {
        return 0.0;
    }

    return u >= 1.0 ? 1.0 : gw_dist(a, b, u) / a.y;
}

float gw_depth_screen(vec4 a, float dir, float x) {
    return gw_on(a) ? clamp(dir * (x - a.x) / a.y, 0.0, 1.0) : 0.0;
}

int g_side_at(float x) {
    return gw_has(w0a, -1.0, x) ? 1 : gw_has(w1a, 1.0, x) ? 2 : 0;
}

int g_end_at(float y) {
    return gw_has(w2a, -1.0, y) ? 3 : gw_has(w3a, 1.0, y) ? 4 : 0;
}

void g_warp(int k, out vec4 a, out vec4 b, out float dir) {
    a = k == 1 ? w0a : k == 2 ? w1a : k == 3 ? w2a : w3a;
    b = k == 1 ? w0b : k == 2 ? w1b : k == 3 ? w2b : w3b;
    dir = k == 1 || k == 3 ? -1.0 : 1.0;
}

float g_row_fan(float center, float fan, float depth) {
    fan = min(fan, s1.z);
    if (gw_on(w2a) && center > w2a.x) {
        fan = min(fan, 1.0 + 0.5 * w2a.y * depth / (center - w2a.x));
    }

    if (gw_on(w3a) && w3a.x > center) {
        fan = min(fan, 1.0 + 0.5 * w3a.y * depth / (w3a.x - center));
    }

    return fan;
}

float g_column_fan(float center, float fan, float depth) {
    fan = min(fan, s1.w);
    if (gw_on(w0a) && center > w0a.x) {
        fan = min(fan, 1.0 + 0.5 * w0a.y * depth / (center - w0a.x));
    }

    if (gw_on(w1a) && w1a.x > center) {
        fan = min(fan, 1.0 + 0.5 * w1a.y * depth / (w1a.x - center));
    }

    return fan;
}

float g_radius(float level) {
    return s1.y > 0.5 ? s1.x * (1.0 - clamp(level, 0.0, 1.0)) : s1.x;
}

vec4 g_ring(vec4 sa, vec4 sb, float sd, vec4 ea, vec4 eb, float ed, float depth) {
    float sideCanvas = sa.x + sd * depth * sa.z;
    float edgeX = gw_to_screen(sa, sb, sd, sideCanvas);
    float edgeY = sb.z + (ea.x - sb.z) * g_row_fan(sb.z, gw_fan(sa, sb, sd, sideCanvas), gw_depth(sa, sb, sd, sideCanvas));
    float endCanvas = ea.x + ed * depth * ea.z;
    float rimY = gw_to_screen(ea, eb, ed, endCanvas);
    float rimX = eb.z + (sa.x - eb.z) * g_column_fan(eb.z, gw_fan(ea, eb, ed, endCanvas), gw_depth(ea, eb, ed, endCanvas));
    return vec4(edgeX, edgeY, rimX, rimY);
}

float g_outside(vec4 sa, vec4 sb, float sd, vec4 ea, vec4 eb, float ed, vec2 p, float depth) {
    vec4 ring = g_ring(sa, sb, sd, ea, eb, ed, depth);
    float spanY = ring.y - ring.w;
    float spanX = ring.z - ring.x;
    if (spanX == 0.0 || spanY == 0.0) {
        return 1.0;
    }

    float toEdge = (p.y - ring.w) / spanY;
    float toRim = (p.x - ring.x) / spanX;
    if (toEdge < 0.0 || toRim < 0.0) {
        return 1.0;
    }

    float radius = g_radius(depth);
    if (toEdge >= radius || toRim >= radius) {
        return -1.0;
    }

    float dx = toRim - radius;
    float dy = toEdge - radius;
    return dx * dx + dy * dy - radius * radius;
}

vec2 g_corner(vec4 sa, vec4 sb, float sd, vec4 ea, vec4 eb, float ed, vec2 p) {
    float depth = 0.0;
    if (s1.x <= 0.0) {
        float across = sd * (gw_to_canvas(sa, sb, sd, p.x) - sa.x) / sa.z;
        float along = ed * (gw_to_canvas(ea, eb, ed, p.y) - ea.x) / ea.z;
        depth = max(0.0, max(across, along));
    } else {
        float low = 0.0;
        float high = 1.0;
        bool grow = true;
        for (int i = 0; i < 6; i++) {
            if (grow && g_outside(sa, sb, sd, ea, eb, ed, p, high) > 0.0) {
                low = high;
                high = high * 2.0;
            } else {
                grow = false;
            }
        }

        for (int i = 0; i < 20; i++) {
            float middle = 0.5 * (low + high);
            if (g_outside(sa, sb, sd, ea, eb, ed, p, middle) > 0.0) {
                low = middle;
            } else {
                high = middle;
            }
        }

        depth = high;
    }

    vec4 ring = g_ring(sa, sb, sd, ea, eb, ed, depth);
    float spanY = ring.y - ring.w;
    float spanX = ring.z - ring.x;
    float toEdge = spanY != 0.0 ? clamp((p.y - ring.w) / spanY, 0.0, 1.0) : 0.0;
    float toRim = spanX != 0.0 ? clamp((p.x - ring.x) / spanX, 0.0, 1.0) : 0.0;
    float radius = g_radius(depth);
    float straight = 1.0 - radius;
    float ux = 0.0;
    float uy = 0.0;
    if (toRim <= toEdge && toEdge >= radius) {
        ux = depth;
        uy = (1.0 - toEdge) * depth;
    } else if (toRim >= radius) {
        ux = (1.0 - toRim) * depth;
        uy = depth;
    } else {
        float angle = atan(1.0 - toEdge / radius, 1.0 - toRim / radius);
        ux = depth * (straight + radius * cos(angle));
        uy = depth * (straight + radius * sin(angle));
    }

    return vec2(sa.x + sd * ux * sa.z, ea.x + ed * uy * ea.z);
}

vec2 g_unwarp(vec2 p) {
    vec4 sa = w0a;
    vec4 sb = w0b;
    float sd = -1.0;
    vec4 ea = w2a;
    vec4 eb = w2b;
    float ed = -1.0;
    int side = g_side_at(p.x);
    int end = g_end_at(p.y);
    if (mod(s6.w, 2.0) >= 1.0) {
        float x = p.x;
        float y = p.y;
        if (side > 0) {
            g_warp(side, sa, sb, sd);
            x = gw_to_canvas(sa, sb, sd, p.x);
        }

        if (end > 0) {
            g_warp(end, ea, eb, ed);
            y = gw_to_canvas(ea, eb, ed, p.y);
        }

        return vec2(x, y);
    }

    float cy = p.y;
    if (side > 0) {
        g_warp(side, sa, sb, sd);
        cy = sb.z + (p.y - sb.z) / g_row_fan(sb.z, gw_fan_screen(sa, sb, sd, p.x), gw_depth_screen(sa, sd, p.x));
        if (g_end_at(cy) == 0) {
            return vec2(gw_to_canvas(sa, sb, sd, p.x), cy);
        }
    }

    float cx = p.x;
    if (end > 0) {
        g_warp(end, ea, eb, ed);
        float fan = g_column_fan(eb.z, gw_fan_screen(ea, eb, ed, p.y), gw_depth_screen(ea, ed, p.y));
        cx = eb.z + (p.x - eb.z) / fan;
        if (g_side_at(cx) == 0) {
            return vec2(cx, gw_to_canvas(ea, eb, ed, p.y));
        }
    }

    if (side == 0) {
        side = g_side_at(cx);
    }

    if (end == 0) {
        end = g_end_at(cy);
    }

    if (side == 0 || end == 0) {
        float x = cx;
        float y = cy;
        if (side > 0) {
            g_warp(side, sa, sb, sd);
            x = gw_to_canvas(sa, sb, sd, p.x);
        }

        if (end > 0) {
            g_warp(end, ea, eb, ed);
            y = gw_to_canvas(ea, eb, ed, p.y);
        }

        return vec2(x, y);
    }

    g_warp(side, sa, sb, sd);
    g_warp(end, ea, eb, ed);
    return g_corner(sa, sb, sd, ea, eb, ed, p);
}

vec4 grid_glow(vec2 coord, vec2 size) {
    vec2 p = s8.yz + coord * vec2(s8.w, s9.x) / size;
    vec2 w = s0.yz + (p - s0.yz) / s0.x;
    vec2 c = g_unwarp(w);
    bool separable = mod(s6.w, 2.0) >= 1.0;
    bool desktop = mod(floor(s6.w / 2.0), 2.0) >= 1.0;
    bool seams = mod(floor(s6.w / 4.0), 2.0) >= 1.0;
    bool density = mod(floor(s6.w / 8.0), 2.0) >= 1.0;
    int side = g_side_at(c.x);
    int end = g_end_at(c.y);
    float sideScale = min(gw_scale(w0a, w0b, -1.0, c.x), gw_scale(w1a, w1b, 1.0, c.x));
    float endScale = min(gw_scale(w2a, w2b, -1.0, c.y), gw_scale(w3a, w3b, 1.0, c.y));
    vec4 a = w0a;
    vec4 b = w0b;
    float dir = -1.0;
    float rowFan = 1.0;
    bool shelf = false;
    float shelfScale = 1.0;
    if (side > 0) {
        g_warp(side, a, b, dir);
        if (!separable) {
            rowFan = g_row_fan(b.z, gw_fan(a, b, dir, c.x), gw_depth(a, b, dir, c.x));
        }

        shelf = b.w > 0.5 && dir * (c.x - a.x) / a.z >= 1.0;
        shelfScale = a.w;
    }

    float columnFan = 1.0;
    if (end > 0) {
        g_warp(end, a, b, dir);
        if (!separable) {
            columnFan = g_column_fan(b.z, gw_fan(a, b, dir, c.y), gw_depth(a, b, dir, c.y));
        }
    }

    float cell = s0.w;
    float strideX = side > 0 ? g_stride(sideScale) : 1.0;
    float strideY = end > 0 ? g_stride(endScale) : 1.0;
    if (shelf) {
        strideY = max(strideY, g_stride(shelfScale));
    }

    float stepX = cell * strideX;
    float stepY = cell * strideY;
    float column = floor(c.x / stepX + 0.5) * stepX;
    float row = floor(c.y / stepY + 0.5) * stepY;
    float unitX = s0.x * sideScale * columnFan;
    float unitY = s0.x * endScale * rowFan;
    bool flatX = gw_inside(w0a, -1.0, c.x) && gw_inside(w1a, 1.0, c.x);
    bool flatY = gw_inside(w2a, -1.0, c.y) && gw_inside(w3a, 1.0, c.y);
    bool hideColumn = !desktop && gw_inside(w0a, -1.0, column) && gw_inside(w1a, 1.0, column) && flatY;
    bool hideRow = !desktop && gw_inside(w2a, -1.0, row) && gw_inside(w3a, 1.0, row) && flatX;
    float big = 1000000.0;
    float dColumn = hideColumn ? big : max(0.0, abs((c.x - column) * unitX - 0.5) - 0.5);
    float dRow = hideRow ? big : max(0.0, abs((c.y - row) * unitY - 0.5) - 0.5);
    if (seams && !desktop) {
        float topSeam = gw_on(w2a) ? w2a.x : s8.z;
        float bottomSeam = gw_on(w3a) ? w3a.x : s8.z + s9.x;
        if (w.y >= topSeam && w.y <= bottomSeam) {
            if (gw_on(w0a)) {
                dColumn = min(dColumn, max(0.0, abs((w.x - (w0a.x - 1.0)) * s0.x - 0.5) - 0.5));
            }

            if (gw_on(w1a)) {
                dColumn = min(dColumn, max(0.0, abs((w.x - w1a.x) * s0.x - 0.5) - 0.5));
            }
        }
    }

    bool vertical = dColumn <= dRow;
    float d = min(dColumn, dRow);
    if (d >= big) {
        return vec4(0.0);
    }

    float spacingX = cell * strideX * unitX;
    float spacingY = cell * strideY * unitY;
    float spacing = side > 0 && end > 0 ? min(spacingX, spacingY) : vertical ? spacingX : spacingY;
    return g_glow(p, d, spacing, vertical, density);
}

vec3 srgb_to_linear(vec3 c) {
    return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void main() {
    vec4 glow = grid_glow(v_uv * u_size, u_size);
    color = vec4(srgb_to_linear(clamp(glow.rgb, 0.0, 1.0)), 0.0) * u_alpha;
}
