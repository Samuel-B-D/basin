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
layout(location = 0) out vec4 color;
vec2 g_lo;
vec2 g_hi;
vec4 tap(sampler2D s, vec2 uv) { return texture(s, clamp(uv, g_lo, g_hi)); }
vec3 encode(vec3 c) {
    c = clamp(c, 0.0, 1.0);
    return mix(c * 12.92, 1.055 * pow(c, vec3(1.0 / 2.4)) - 0.055, step(0.0031308, c));
}
vec4 encoded(vec4 c) { return pc.reserved2.x > 0.5 ? vec4(encode(c.rgb), c.a) : c; }
void main() {
    vec2 texSize = vec2(textureSize(src, 0));
    vec2 uv = gl_FragCoord.xy * pc.srcScale / texSize;
    vec2 h = 0.5 / texSize;
    g_lo = (pc.box.xy - pc.box.zw) / (texSize * pc.reserved) + (0.5 / texSize);
    g_hi = (pc.box.xy + pc.box.zw) / (texSize * pc.reserved) - (0.5 / texSize);
    color = (encoded(tap(src, uv + vec2(-h.x, -h.y))) + encoded(tap(src, uv + vec2(h.x, -h.y))) +
             encoded(tap(src, uv + vec2(-h.x, h.y))) + encoded(tap(src, uv + vec2(h.x, h.y)))) * 0.25;
}
