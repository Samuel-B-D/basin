#version 450
layout(push_constant) uniform Push {
    float srcScale;
    float opacity;
    float intensity;
    float reserved;
    vec2 halfpixelTexels;
    vec2 reserved2;
    vec4 colorMatrix0;
    vec4 colorMatrix1;
    vec4 colorMatrix2;
    vec4 box;
    vec4 cornerRadius;
    vec4 frost;
} pc;
layout(set = 0, binding = 0) uniform sampler2D src;
layout(set = 1, binding = 0) uniform sampler2D noiseTex;
layout(set = 2, binding = 0) uniform sampler2D plainTex;
layout(location = 0) out vec4 color;
const float W[16] = float[16](
    0.02800, 0.04784, 0.05251, 0.03851,
    0.04784, 0.08168, 0.09191, 0.06534,
    0.05251, 0.09101, 0.10268, 0.07235,
    0.03851, 0.06534, 0.07235, 0.05251);
const float O[4] = float[4](-1.824, -0.608, 0.608, 1.824);
vec2 g_lo;
vec2 g_hi;
vec4 tap(sampler2D s, vec2 uv) { return texture(s, clamp(uv, g_lo, g_hi)); }
vec3 decode(vec3 c) {
    return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}
float basin_rounded_box(vec2 position, vec2 center, vec2 extents, vec4 radius) {
    vec2 p = position - center;
    float r = p.x > 0.0
        ? (p.y < 0.0 ? radius.y : radius.w)
        : (p.y < 0.0 ? radius.x : radius.z);
    vec2 q = abs(p) - extents + vec2(r);
    return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
}
void main() {
    vec2 texSize = vec2(textureSize(src, 0));
    vec2 uv = gl_FragCoord.xy * pc.srcScale / texSize;
    g_lo = (pc.box.xy - pc.box.zw) / (texSize * pc.reserved) + (0.5 / texSize);
    g_hi = (pc.box.xy + pc.box.zw) / (texSize * pc.reserved) - (0.5 / texSize);
    vec4 sum = vec4(0.0);
    for (int r = 0; r < 4; r++) {
        for (int c = 0; c < 4; c++) {
            sum += W[r * 4 + c] * tap(src, uv + vec2(O[c], O[r]) / texSize);
        }
    }
    vec3 blurred = pc.reserved2.x > 0.5 ? decode(clamp(sum.rgb, 0.0, 1.0)) : sum.rgb;
    float coverage = pc.opacity;
    if (pc.cornerRadius != vec4(0.0)) {
        float f = basin_rounded_box(gl_FragCoord.xy, pc.box.xy, pc.box.zw, pc.cornerRadius);
        float df = fwidth(f);
        coverage *= 1.0 - clamp(0.5 + f / df, 0.0, 1.0);
    }
    vec3 plain = texture(plainTex, gl_FragCoord.xy / vec2(textureSize(plainTex, 0))).rgb;
    color = vec4(mix(plain, blurred, coverage), 1.0);
}
