#version 450

layout(location = 0) in vec3 a_position;
layout(location = 1) in vec3 a_normal;
layout(location = 2) in vec2 a_uv;

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

layout(location = 0) out vec3 v_world;
layout(location = 1) out vec3 v_normal;
layout(location = 2) out vec2 v_uv;

void main()
{
    vec4 world = draw.model * vec4(a_position, 1.0);
    v_world = world.xyz;
    v_normal = mat3(draw.model) * a_normal;
    v_uv = draw.uvRect.xy + (a_uv * draw.uvRect.zw);
    gl_Position = draw.mvp * vec4(a_position, 1.0);
}
