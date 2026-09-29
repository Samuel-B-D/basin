namespace Basin.Render3D.Gl;

internal static class GlWorldShaders
{
    public const string LitVertex = """
        #version 300 es
        precision highp float;
        layout(location = 0) in vec3 a_position;
        layout(location = 1) in vec3 a_normal;
        layout(location = 2) in vec2 a_uv;
        uniform mat4 u_mvp;
        uniform mat4 u_model;
        uniform vec4 u_uvRect;
        out vec3 v_world;
        out vec3 v_normal;
        out vec2 v_uv;
        void main()
        {
            vec4 world = u_model * vec4(a_position, 1.0);
            v_world = world.xyz;
            v_normal = mat3(u_model) * a_normal;
            v_uv = u_uvRect.xy + (a_uv * u_uvRect.zw);
            gl_Position = u_mvp * vec4(a_position, 1.0);
        }
        """;

    public const string SkinnedVertex = """
        #version 300 es
        precision highp float;
        layout(location = 0) in vec3 a_position;
        layout(location = 1) in vec3 a_normal;
        layout(location = 2) in vec2 a_uv;
        layout(location = 3) in vec4 a_joints;
        layout(location = 4) in vec4 a_weights;
        uniform mat4 u_mvp;
        uniform mat4 u_model;
        uniform vec4 u_uvRect;
        uniform mat4 u_joints[48];
        out vec3 v_world;
        out vec3 v_normal;
        out vec2 v_uv;
        void main()
        {
            mat4 skin =
                (a_weights.x * u_joints[int(a_joints.x)]) +
                (a_weights.y * u_joints[int(a_joints.y)]) +
                (a_weights.z * u_joints[int(a_joints.z)]) +
                (a_weights.w * u_joints[int(a_joints.w)]);
            vec4 local = skin * vec4(a_position, 1.0);
            vec4 world = u_model * local;
            v_world = world.xyz;
            v_normal = mat3(u_model) * (mat3(skin) * a_normal);
            v_uv = u_uvRect.xy + (a_uv * u_uvRect.zw);
            gl_Position = u_mvp * local;
        }
        """;

    public const string LitFragment = """
        #version 300 es
        precision highp float;
        in vec3 v_world;
        in vec3 v_normal;
        in vec2 v_uv;
        uniform sampler2D u_texture;
        uniform vec4 u_tint;
        uniform float u_textured;
        uniform float u_lit;
        uniform float u_forceOpaque;
        uniform vec3 u_ambient;
        uniform vec3 u_eye;
        uniform vec3 u_emissive;
        uniform vec2 u_specular;
        uniform int u_lightCount;
        uniform vec4 u_lightPosition[8];
        uniform vec4 u_lightColor[8];
        out vec4 o_color;
        void main()
        {
            vec4 base = u_textured > 0.5 ? texture(u_texture, v_uv) : vec4(1.0);
            if (u_forceOpaque > 0.5)
            {
                base.a = 1.0;
            }

            vec3 color = base.rgb * u_tint.rgb;
            if (u_lit > 0.5)
            {
                vec3 n = normalize(v_normal);
                if (!gl_FrontFacing)
                {
                    n = -n;
                }

                vec3 toEye = normalize(u_eye - v_world);
                vec3 light = u_ambient;
                for (int i = 0; i < 8; i++)
                {
                    if (i >= u_lightCount)
                    {
                        break;
                    }

                    vec3 offset = u_lightPosition[i].xyz - v_world;
                    float distance = length(offset);
                    vec3 l = offset / max(distance, 0.0001);
                    float falloff = clamp(1.0 - (distance / u_lightPosition[i].w), 0.0, 1.0);
                    float diffuse = max(dot(n, l), 0.0);
                    vec3 halfway = normalize(l + toEye);
                    float specular = pow(max(dot(n, halfway), 0.0), max(u_specular.y, 1.0)) * u_specular.x * step(0.0001, diffuse);
                    light += u_lightColor[i].rgb * falloff * (diffuse + specular);
                }

                color *= light;
            }

            color += u_emissive * base.a;
            o_color = vec4(color * u_tint.a, base.a * u_tint.a);
        }
        """;

    public const string FlatVertex = """
        #version 300 es
        precision highp float;
        layout(location = 0) in vec3 a_position;
        uniform mat4 u_mvp;
        uniform float u_far;
        void main()
        {
            vec4 position = u_mvp * vec4(a_position, 1.0);
            if (u_far > 0.5)
            {
                position.z = position.w * 0.9999999;
            }

            gl_Position = position;
        }
        """;

    public const string FlatFragment = """
        #version 300 es
        precision highp float;
        uniform vec4 u_color;
        out vec4 o_color;
        void main()
        {
            o_color = u_color;
        }
        """;

    public const string CopyVertex = """
        #version 300 es
        precision highp float;
        out vec2 v_uv;
        void main()
        {
            vec2 corner = vec2(float(gl_VertexID & 1), float((gl_VertexID >> 1) & 1));
            v_uv = corner;
            gl_Position = vec4((corner * 2.0) - 1.0, 0.0, 1.0);
        }
        """;

    public const string CopyFragment = """
        #version 300 es
        precision highp float;
        in vec2 v_uv;
        uniform sampler2D u_texture;
        uniform vec4 u_uvRect;
        uniform float u_forceOpaque;
        out vec4 o_color;
        void main()
        {
            vec4 color = texture(u_texture, u_uvRect.xy + (v_uv * u_uvRect.zw));
            if (u_forceOpaque > 0.5)
            {
                color.a = 1.0;
            }

            o_color = color;
        }
        """;
}
