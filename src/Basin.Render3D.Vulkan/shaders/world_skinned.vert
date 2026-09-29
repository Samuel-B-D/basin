#version 450

layout(location = 0) in vec3 a_position;
layout(location = 1) in vec3 a_normal;
layout(location = 2) in vec2 a_uv;
layout(location = 3) in vec4 a_joints;
layout(location = 4) in vec4 a_weights;

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

layout(std140, set = 0, binding = 3) uniform Joints
{
    mat4 joints[48];
} skin;

layout(location = 0) out vec3 v_world;
layout(location = 1) out vec3 v_normal;
layout(location = 2) out vec2 v_uv;

void main()
{
    mat4 blend =
        (a_weights.x * skin.joints[int(a_joints.x)]) +
        (a_weights.y * skin.joints[int(a_joints.y)]) +
        (a_weights.z * skin.joints[int(a_joints.z)]) +
        (a_weights.w * skin.joints[int(a_joints.w)]);
    vec4 local = blend * vec4(a_position, 1.0);
    vec4 world = draw.model * local;
    v_world = world.xyz;
    v_normal = mat3(draw.model) * (mat3(blend) * a_normal);
    v_uv = draw.uvRect.xy + (a_uv * draw.uvRect.zw);
    gl_Position = draw.mvp * local;
}
