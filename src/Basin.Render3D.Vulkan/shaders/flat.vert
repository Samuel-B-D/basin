#version 450

layout(location = 0) in vec3 a_position;

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

void main()
{
    vec4 position = draw.mvp * vec4(a_position, 1.0);
    if (draw.flags.w > 0.5)
    {
        position.z = position.w * 0.9999999;
    }

    gl_Position = position;
}
