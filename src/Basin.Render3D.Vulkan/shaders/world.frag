#version 450

layout(location = 0) in vec3 v_world;
layout(location = 1) in vec3 v_normal;
layout(location = 2) in vec2 v_uv;

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

layout(std140, set = 0, binding = 1) uniform Scene
{
    vec4 ambient;
    vec4 eye;
    ivec4 counts;
    vec4 lightPosition[8];
    vec4 lightColor[8];
} scene;

layout(set = 0, binding = 2) uniform sampler2D u_texture;

layout(location = 0) out vec4 o_color;

void main()
{
    vec4 base = draw.flags.y > 0.5 ? texture(u_texture, v_uv) : vec4(1.0);
    if (draw.flags.z > 0.5)
    {
        base.a = 1.0;
    }

    vec3 color = base.rgb * draw.tint.rgb;
    if (draw.flags.x > 0.5)
    {
        vec3 n = normalize(v_normal);
        if (!gl_FrontFacing)
        {
            n = -n;
        }

        vec3 toEye = normalize(scene.eye.xyz - v_world);
        vec3 light = scene.ambient.rgb;
        for (int i = 0; i < 8; i++)
        {
            if (i >= scene.counts.x)
            {
                break;
            }

            vec3 offset = scene.lightPosition[i].xyz - v_world;
            float distance = length(offset);
            vec3 l = offset / max(distance, 0.0001);
            float falloff = clamp(1.0 - (distance / scene.lightPosition[i].w), 0.0, 1.0);
            float diffuse = max(dot(n, l), 0.0);
            vec3 halfway = normalize(l + toEye);
            float specular = pow(max(dot(n, halfway), 0.0), max(draw.material.y, 1.0)) * draw.material.x * step(0.0001, diffuse);
            light += scene.lightColor[i].rgb * falloff * (diffuse + specular);
        }

        color *= light;
    }

    color += draw.emissive.rgb * base.a;
    o_color = vec4(color * draw.tint.a, base.a * draw.tint.a);
}
