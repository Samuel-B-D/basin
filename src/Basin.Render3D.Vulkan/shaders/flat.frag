#version 450

layout(std140, set = 0, binding = 0) uniform Draw
{
    mat4 mvp;
    mat4 model;
    vec4 tint;
    vec4 uvRect;
    vec4 emissive;
    vec4 flags;
    vec4 material;
} draw;

layout(location = 0) out vec4 o_color;

void main()
{
    o_color = draw.tint;
}
