#version 450

layout(location = 0) in vec2 v_uv;

layout(set = 0, binding = 0) uniform sampler2D u_texture;

layout(push_constant) uniform Copy
{
    vec4 uvRect;
    vec4 flags;
} copy;

layout(location = 0) out vec4 o_color;

void main()
{
    vec4 color = texture(u_texture, copy.uvRect.xy + (v_uv * copy.uvRect.zw));
    if (copy.flags.x > 0.5)
    {
        color.a = 1.0;
    }

    o_color = color;
}
